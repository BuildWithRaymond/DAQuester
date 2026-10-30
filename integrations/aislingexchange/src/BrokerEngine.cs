using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AislingExchange.Broker;

public sealed partial class BrokerEngine : IDisposable
{
    private const string PublicPrefix = "/api/quest-supplies/v1";
    private const string WorkerPrefix = "/worker/v1";
    private readonly StateStore store;
    private readonly BrokerOptions options;
    private readonly TimeProvider time;
    private static readonly HashSet<string> KnownItems = new(StringComparer.Ordinal)
    {
        "Rotten Apple", "Rotten Grapes", "Centipede's Gland", "Bat's Wing", "Scorpion's Sting", "Spider's Silk",
        "Raw Wax", "Viper's Gland", "Bee's Sting", "Raw Honey", "Royal Wax", "Mantis's Eye", "Kobold's Skull",
        "Mushroom", "Faerie's Wing", "Wolf's Fur", "Wolf's Teeth", "Goblin's Skull", "Frog's Leg", "Crab's Claw",
        "Gruesomefly Antenna", "Gruesomefly Wing", "Rock Cobbler's Scale", "Polyp Sac", "Spore Sac", "Ghast's Skull",
        "Gargoyle's Skull", "Gargoyle Fiend's Skull", "Leather Shield", "Wooden Shield", "Dirk", "Baguette", "Grapes",
        "Cherry", "Wine", "Loures Saber", "Wind Belt", "Sea Necklace", "Leather Greaves", "Oren Brandy", "Mold",
        "Succubus's Hair", "Ard Ioc Deum",
    };
    private static readonly HashSet<string> ErrorCodes = new(StringComparer.Ordinal)
    {
        "QUOTE_CHANGED", "RECONCILIATION_REQUIRED", "STOCK_UNAVAILABLE", "BUDGET_EXHAUSTED", "CHARACTER_NOT_ALLOWED",
        "UNSUPPORTED_ITEM", "INVALID_QUANTITY", "SERVICE_UNAVAILABLE", "CANCELLED", "CREDENTIAL_EXPIRED", "REVOKED",
    };
    public BrokerEngine(string directory, BrokerOptions options, TimeProvider? time = null)
    {
        if (!Identifier(options.GameServer) || options.WorkerTokenHash.Length != 64 || !options.WorkerTokenHash.All(Uri.IsHexDigit)
            || options.MaxPairings < 1 || options.MaxOperations < 1 || options.MaxQueuedJobs < 1 || options.MaxQuotes < 1
            || options.MaxUnitsPerOperation is < 1 or > 100
            || options.CatalogLimits.Any(i => !KnownItems.Contains(i.Key) || i.Value is < 1 or > 100))
            throw new ArgumentException("Invalid AislingExchange configuration.");
        this.options = options;
        this.time = time ?? TimeProvider.System;
        store = new StateStore(directory);
    }
    public BrokerReply Execute(string method, string path, JsonElement body = default, string? bearer = null, string? idempotencyKey = null)
    {
        try
        {
            if (path.Length > 256 || body.ValueKind != JsonValueKind.Undefined && Encoding.UTF8.GetByteCount(body.GetRawText()) > 16384)
                throw new BrokerFault(413, "REQUEST_TOO_LARGE");
            if (body.ValueKind != JsonValueKind.Undefined) Wire.RejectDuplicateMembers(body);
            return store.Update(s => Route(s, method, path, body, bearer, idempotencyKey));
        }
        catch (BrokerFault e) { return Error(e.Status, e.Code); }
        catch (JsonException) { return Error(400, "INVALID_REQUEST"); }
        catch (ArgumentException) { return Error(400, "INVALID_REQUEST"); }
        catch (IOException) { return Error(503, "SERVICE_UNAVAILABLE"); }
    }
    private BrokerReply Route(BrokerState s, string method, string path, JsonElement body, string? bearer, string? key)
    {
        var now = time.GetUtcNow();
        if (path.StartsWith(WorkerPrefix + "/", StringComparison.Ordinal))
        {
            if (!HashMatches(bearer, options.WorkerTokenHash)) throw new BrokerFault(401, "UNAUTHORIZED");
            return Worker(s, method, path[WorkerPrefix.Length..], body, now);
        }
        if (!path.StartsWith(PublicPrefix + "/", StringComparison.Ordinal)) throw new BrokerFault(404, "NOT_FOUND");
        var route = path[PublicPrefix.Length..];
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (method == "POST" && route == "/pairings") return StartPairing(s, Wire.Read<PairingStart>(body), now);
        if (method == "POST" && parts is ["pairings", var pairingId, var action] && Guid.TryParse(pairingId, out var pairing)
            && action is "status" or "complete" or "revoke") return AccessPairing(s, pairing, action, Wire.Read<PairingAccess>(body), now);
        if (method == "POST" && route == "/recoveries") return Recover(s, Authenticate(s, bearer, now, "recovery"), Wire.Read<RecoveryInput>(body), now);
        var credential = Authenticate(s, bearer, now);
        if (method == "POST" && route == "/credentials/revoke")
        {
            RequireEmpty(body);
            RevokeInstallation(s, credential.Subject, credential.InstallationId, now);
            return Ok(new { state = "revoked" });
        }
        if (method == "GET" && route == "/capabilities") return Ok(new
        {
            gameServer = options.GameServer, receiver = "Exchanger", ready = IsReady(s, now),
            returnSupported = s.Catalog.ReturnSupported && IsReady(s, now), receiveModes = s.Catalog.ReceiveModes,
            items = s.Catalog.Items.Select(i => new { i.Item, i.MaxPerRequest }),
        });
        if (method == "POST" && route == "/quotes") return Quote(s, credential, Wire.Read<QuoteInput>(body), now);
        if (method == "POST" && route == "/requests") return SubmitSupply(s, credential, Wire.Read<SupplyInput>(body), body, key, now);
        if (method == "POST" && route == "/returns") return SubmitReturn(s, credential, Wire.Read<ReturnInput>(body), body, key, now);
        if (method == "POST" && route == "/operations/find")
        {
            var find = Wire.Read<FindInput>(body);
            RequireId(find.ClientOperationId);
            var found = s.Operations.SingleOrDefault(o => o.ClientOperationId == find.ClientOperationId && Owns(s, credential, o));
            return found is null ? Error(404, "NOT_FOUND") : View(found);
        }
        if (method == "GET" && parts is [var group, var id] && group is "requests" or "returns" && Guid.TryParse(id, out var operationId))
            return View(FindOwned(s, credential, operationId, group));
        if (method == "POST" && parts is [var changeGroup, var changeId, var change] && changeGroup is "requests" or "returns"
            && Guid.TryParse(changeId, out var changedId)) return MutateOperation(s, credential, FindOwned(s, credential, changedId, changeGroup), change, body, key, now);
        throw new BrokerFault(404, "NOT_FOUND");
    }
    private BrokerReply StartPairing(BrokerState s, PairingStart r, DateTimeOffset now)
    {
        RequireId(r.InstallationId);
        RequireId(r.ClientOperationId);
        if (r.GameServer != options.GameServer || !CharacterName(r.Character) || !Base64Secret(r.VerifierChallenge)
            || r.Purpose is not ("quest-supplies" or "recovery")) throw new BrokerFault(400, "INVALID_REQUEST");
        var previous = s.Pairings.SingleOrDefault(p => p.InstallationId == r.InstallationId && p.ClientOperationId == r.ClientOperationId);
        if (previous is not null)
        {
            if (previous.Challenge != r.VerifierChallenge || previous.Character != r.Character || previous.GameServer != r.GameServer || previous.Purpose != r.Purpose)
                throw new BrokerFault(409, "IDEMPOTENCY_CONFLICT");
            return Reply(202, PairingView(previous, now, true));
        }
        s.Pairings.RemoveAll(p => p.ExpiresAt < now.AddHours(-1));
        s.Credentials.RemoveAll(c => c.ExpiresAt < now.AddHours(-1));
        if (s.Pairings.Count >= options.MaxPairings || s.Pairings.Count(p => p.GameServer == r.GameServer && SameCharacter(p.Character, r.Character)
            && p.ExpiresAt > now && p.State is "registering" or "ready" or "observed") >= 3) throw new BrokerFault(429, "RATE_LIMITED");
        var pairing = new PairingRecord
        {
            Id = Guid.NewGuid(), InstallationId = r.InstallationId, ClientOperationId = r.ClientOperationId, Character = r.Character,
            GameServer = r.GameServer, Challenge = r.VerifierChallenge, Purpose = r.Purpose, Nonce = "AEQ-" + Secret(), CreatedAt = now, ExpiresAt = now.AddMinutes(5),
        };
        AddJob(s, "register-pairing", pairing.Id, now);
        s.Pairings.Add(pairing);
        return Reply(202, PairingView(pairing, now, true));
    }
    private BrokerReply AccessPairing(BrokerState s, Guid id, string action, PairingAccess r, DateTimeOffset now)
    {
        var pairing = s.Pairings.SingleOrDefault(p => p.Id == id);
        if (pairing is null || !Base64Secret(r.Verifier) || !FixedEquals(Challenge(r.Verifier), pairing.Challenge)) throw new BrokerFault(401, "UNAUTHORIZED");
        if (action == "status") return Ok(PairingView(pairing, now));
        if (action == "revoke")
        {
            foreach (var credential in s.Credentials.Where(c => c.TokenHash == pairing.IssuedTokenHash)) credential.Revoked = true;
            foreach (var operation in s.Operations.Where(o => o.BindingId == pairing.Id && !Terminal(o)))
            {
                operation.Revoked = true;
                operation.CancellationRequested = true;
                if (!s.Jobs.Any(j => j.Kind == "cancel-uncommitted" && j.TargetId == operation.Id && !j.Completed))
                    AddJob(s, "cancel-uncommitted", operation.Id, now);
            }
            pairing.State = "reproof_required";
            return Ok(new { state = "revoked" });
        }
        if (pairing.ExpiresAt <= now) throw new BrokerFault(409, "PAIRING_EXPIRED");
        if (pairing.State is "issued" or "reproof_required")
        {
            foreach (var c in s.Credentials.Where(c => c.TokenHash == pairing.IssuedTokenHash)) c.Revoked = true;
            pairing.State = "reproof_required";
            return Error(409, "PAIRING_REPROOF_REQUIRED");
        }
        if (pairing.State != "observed") return Ok(new { state = "pending", credential = (object?)null });
        var token = Secret();
        var scope = pairing.Purpose == "recovery" ? "recovery" : "quest-supplies";
        if (pairing.ProofObservedAt is null) throw new BrokerFault(409, "PAIRING_REPROOF_REQUIRED");
        var expires = scope == "recovery" ? now.AddMinutes(5) : pairing.ProofObservedAt.Value.AddHours(1);
        var issued = new CredentialRecord
        {
            TokenHash = Hash(token), PairingId = pairing.Id, InstallationId = pairing.InstallationId, Subject = Subject(pairing.GameServer, pairing.Character),
            Character = pairing.Character, GameServer = pairing.GameServer, Scope = scope, ExpiresAt = expires,
        };
        s.Credentials.Add(issued);
        pairing.IssuedTokenHash = issued.TokenHash;
        pairing.State = "issued";
        return Ok(new { state = "paired", credential = new { character = issued.Character, gameServer = issued.GameServer, accessToken = token, scope, expiresAt = expires } });
    }
}
