using System.Text.Json;
namespace AislingExchange.Broker;

public sealed partial class BrokerEngine
{
    private BrokerReply Worker(BrokerState s, string method, string route, JsonElement body, DateTimeOffset now)
    {
        if (method != "POST") throw new BrokerFault(404, "NOT_FOUND");
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (route == "/catalog")
        {
            var r = Wire.Read<CatalogInput>(body);
            if (r.GameServer != options.GameServer || r.Items is null || r.Items.Count > 43
                || r.Items.Select(i => i.Item).Distinct(StringComparer.Ordinal).Count() != r.Items.Count
                || r.ReceiveModes is null || r.ReceiveModes.Count > 2 || r.ReceiveModes.Distinct().Count() != r.ReceiveModes.Count
                || r.ReceiveModes.Any(m => m is not ("single-unit" or "receive-all"))
                || r.Items.Any(i => !options.CatalogLimits.TryGetValue(i.Item, out var cap) || i.MaxPerRequest < 1
                    || i.MaxPerRequest > cap || i.Available < 0 || i.Available > cap)) throw new BrokerFault(422, "INVALID_REQUEST");
            s.Catalog = new CatalogRecord { Ready = r.Ready, ReturnSupported = r.ReturnSupported, ReceiveModes = r.ReceiveModes, Items = r.Items, UpdatedAt = now };
            return Ok(new { state = "published" });
        }
        if (route == "/jobs/claim")
        {
            RequireEmpty(body);
            s.Jobs.RemoveAll(j => j.Kind == "register-pairing" && !s.Pairings.Any(p => p.Id == j.TargetId && p.ExpiresAt > now));
            var job = s.Jobs.Where(j => !j.Completed && j.LeaseExpiresAt <= now)
                .OrderByDescending(j => j.Kind == "cancel-uncommitted").ThenBy(j => j.CreatedAt).FirstOrDefault();
            if (job is null) return Ok(new { job = (object?)null });
            var token = Secret();
            job.LeaseTokenHash = Hash(token);
            job.LeaseExpiresAt = now.AddSeconds(30);
            job.Fence++;
            return Ok(new { job = new { id = job.Id, kind = job.Kind, targetId = job.TargetId, fence = job.Fence,
                leaseToken = token, leaseExpiresAt = job.LeaseExpiresAt, payload = JobPayload(s, job) } });
        }
        if (parts is ["jobs", var id, "result"] && Guid.TryParse(id, out var jobId))
        {
            var r = Wire.Read<JobResult>(body);
            var job = s.Jobs.SingleOrDefault(j => j.Id == jobId);
            if (job is null || job.Completed || job.LeaseExpiresAt <= now || job.Fence != r.Fence || job.LeaseTokenHash is null
                || !HashMatches(r.LeaseToken, job.LeaseTokenHash)) throw new BrokerFault(409, "STALE_LEASE");
            if (job.Kind == "register-pairing")
            {
                var p = s.Pairings.SingleOrDefault(p => p.Id == job.TargetId);
                if (r.State is not ("ready" or "rejected")) throw new BrokerFault(422, "INVALID_REQUEST");
                if (p is not null && p.ExpiresAt > now && p.State == "registering") p.State = r.State == "ready" ? "ready" : "expired";
            }
            else if (r.State != "accepted") throw new BrokerFault(422, "INVALID_REQUEST");
            job.Completed = true;
            return Ok(new { state = "acknowledged" });
        }
        if (parts is ["pairings", var statusPairingText, "status"] && Guid.TryParse(statusPairingText, out var statusPairingId))
        {
            RequireEmpty(body);
            var pairing = s.Pairings.SingleOrDefault(p => p.Id == statusPairingId) ?? throw new BrokerFault(404, "NOT_FOUND");
            return Ok(new { id = pairing.Id, character = pairing.Character, gameServer = pairing.GameServer,
                state = pairing.State is "observed" or "issued" ? "observed" : pairing.ExpiresAt <= now ? "expired" : pairing.State });
        }
        if (parts is ["pairings", var pairingIdText, "proof"] && Guid.TryParse(pairingIdText, out var pairingId))
        {
            var r = Wire.Read<PairingProof>(body);
            var p = s.Pairings.SingleOrDefault(p => p.Id == pairingId);
            if (p is null || p.State != "ready" || p.ExpiresAt <= now || r.GameServer != p.GameServer || !SameCharacter(r.Character, p.Character)
                || r.Nonce != p.Nonce || r.ObservedAt < p.CreatedAt || r.ObservedAt > now.AddSeconds(5) || r.ObservedAt < now.AddMinutes(-5))
                throw new BrokerFault(409, "PAIRING_PROOF_REJECTED");
            p.State = "observed";
            p.ProofObservedAt = r.ObservedAt;
            return Ok(new { state = "observed" });
        }
        if (parts is ["operations", var operationText, "status"] && Guid.TryParse(operationText, out var operationId))
        {
            var operation = s.Operations.SingleOrDefault(o => o.Id == operationId) ?? throw new BrokerFault(404, "NOT_FOUND");
            ApplyUpdate(operation, Wire.Read<OperationUpdate>(body));
            return View(operation);
        }
        throw new BrokerFault(404, "NOT_FOUND");
    }
    private void ApplyUpdate(OperationRecord operation, OperationUpdate r)
    {
        if (r.Sequence == operation.Sequence && r.State == operation.State && r.ErrorCode == operation.ErrorCode
            && r.Units is not null && r.Units.Count == operation.Units.Count && r.Units.Select(u => u.Id).Distinct().Count() == r.Units.Count
            && r.Units.All(change => operation.Units.Any(unit => unit.Id == change.Id && unit.SourceAccepted == change.SourceAccepted
                && unit.RecipientObserved == change.RecipientObserved && unit.Cancelled == change.Cancelled
                && unit.Collected == change.Collected && unit.Banked == change.Banked))) return;
        string[] states = operation.Kind == "supply"
            ? ["reserved", "processing", "partial", "sent", "received", "cancelled", "rejected", "review"]
            : ["registered", "awaiting_player_send", "awaiting_exchanger_receive", "collecting", "banking", "complete", "partial", "review", "cancelled", "rejected"];
        if (!states.Contains(r.State) || r.Sequence <= operation.Sequence || Terminal(operation) || r.ErrorCode is not null && !ErrorCodes.Contains(r.ErrorCode)
            || r.Units is null || r.Units.Count != operation.Units.Count || r.Units.Select(u => u.Id).Distinct().Count() != r.Units.Count)
            throw new BrokerFault(409, "INVALID_TRANSITION");
        foreach (var change in r.Units)
        {
            var unit = operation.Units.SingleOrDefault(u => u.Id == change.Id) ?? throw new BrokerFault(422, "INVALID_REQUEST");
            if (unit.SourceAccepted && !change.SourceAccepted || unit.RecipientObserved && !change.RecipientObserved || unit.Cancelled && !change.Cancelled
                || unit.Collected && !change.Collected || unit.Banked && !change.Banked
                || change.Cancelled && (change.SourceAccepted || change.RecipientObserved || change.Collected || change.Banked)
                || change.RecipientObserved && !change.SourceAccepted || change.Banked && !change.Collected
                || operation.Kind == "supply" && (change.Collected || change.Banked)) throw new BrokerFault(409, "INVALID_TRANSITION");
            unit.SourceAccepted = change.SourceAccepted;
            unit.RecipientObserved = change.RecipientObserved;
            unit.Cancelled = change.Cancelled;
            unit.Collected = change.Collected;
            unit.Banked = change.Banked;
        }
        if (r.State == "received" && operation.Units.Any(u => !u.RecipientObserved && !u.Cancelled)
            || r.State == "sent" && operation.Units.Any(u => !u.SourceAccepted && !u.Cancelled)
            || r.State == "complete" && operation.Units.Any(u => !u.Banked && !u.Cancelled)
            || r.State == "cancelled" && operation.Units.Any(u => !u.Cancelled && !(operation.Kind == "supply" ? u.RecipientObserved : u.Banked))
            || r.State == "rejected" && operation.Units.Any(u => u.SourceAccepted || u.RecipientObserved || u.Collected || u.Banked)) throw new BrokerFault(409, "INVALID_TRANSITION");
        operation.Sequence = r.Sequence;
        operation.State = r.State;
        operation.ErrorCode = r.ErrorCode;
    }
    private static object JobPayload(BrokerState s, JobRecord job)
    {
        if (job.Kind == "register-pairing")
        {
            var p = s.Pairings.SingleOrDefault(p => p.Id == job.TargetId);
            return p is null ? new { id = job.TargetId, expired = true } : new
            {
                id = p.Id, installationId = p.InstallationId, clientOperationId = p.ClientOperationId, character = p.Character,
                gameServer = p.GameServer, verifierChallenge = p.Challenge, nonce = p.Nonce, receiver = "Exchanger", purpose = p.Purpose, expiresAt = p.ExpiresAt,
            };
        }
        var o = s.Operations.Single(o => o.Id == job.TargetId);
        return new { id = o.Id, o.ClientOperationId, o.JourneyId, o.BindingId, o.InstallationId, o.Character, o.GameServer,
            receiver = "Exchanger", o.Kind, o.Manifest, o.Units, o.CredentialExpiresAt, o.Revoked, o.CancellationRequested };
    }
}
