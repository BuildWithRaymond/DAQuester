using System.Text.Json;
namespace AislingExchange.Broker;

public sealed partial class BrokerEngine
{
    private BrokerReply Quote(BrokerState s, CredentialRecord c, QuoteInput r, DateTimeOffset now)
    {
        MatchIdentity(c, r.Character, r.GameServer);
        RequireId(r.JourneyId);
        ValidateItems(r.Items, quoting: true);
        if (!IsReady(s, now)) throw new BrokerFault(503, "SERVICE_UNAVAILABLE");
        s.Quotes.RemoveAll(q => q.ExpiresAt <= now);
        if (s.Quotes.Count >= options.MaxQuotes) throw new BrokerFault(429, "RATE_LIMITED");
        var quote = new QuoteRecord
        {
            Id = Guid.NewGuid(), Subject = c.Subject, InstallationId = c.InstallationId, JourneyId = r.JourneyId, ExpiresAt = now.AddMinutes(2),
            Items = r.Items.Select(item =>
            {
                var stock = s.Catalog.Items.SingleOrDefault(i => i.Item == item.Item);
                return new ItemQuantity(item.Item, Math.Min(item.Quantity, stock is null ? 0 : Math.Min(stock.Available, stock.MaxPerRequest)));
            }).Where(item => item.Quantity > 0).ToList(),
        };
        s.Quotes.Add(quote);
        return Ok(new { id = quote.Id, character = c.Character, gameServer = c.GameServer, journeyId = quote.JourneyId,
            expiresAt = quote.ExpiresAt, items = quote.Items });
    }
    private BrokerReply SubmitSupply(BrokerState s, CredentialRecord c, SupplyInput r, JsonElement body, string? key, DateTimeOffset now)
    {
        var replay = Replay(s, c, "/requests", key, r.ClientOperationId, body);
        if (replay is not null) return View(replay, 202);
        MatchIdentity(c, r.Character, r.GameServer);
        RequireId(r.JourneyId);
        ValidateItems(r.Items);
        if (!IsReady(s, now)) throw new BrokerFault(503, "SERVICE_UNAVAILABLE");
        var quote = s.Quotes.SingleOrDefault(q => q.Id == r.QuoteId && q.Subject == c.Subject && q.InstallationId == c.InstallationId);
        if (quote is null || quote.ExpiresAt <= now || quote.AcceptedOperationId is not null || quote.JourneyId != r.JourneyId)
            throw new BrokerFault(409, "QUOTE_CHANGED");
        if (r.Items.Any(i => i.Quantity > (quote.Items.SingleOrDefault(q => q.Item == i.Item)?.Quantity ?? 0))) throw new BrokerFault(422, "INVALID_QUANTITY");
        if (s.Operations.Any(o => o.Kind == "supply" && o.Subject == c.Subject && !DispatchSettled(o))) throw new BrokerFault(409, "RECONCILIATION_REQUIRED");
        var operation = CreateOperation(s, c, r.ClientOperationId, r.JourneyId, "supply", r.Items, body, now);
        Remember(s, c, "/requests", key!, r.ClientOperationId, body, operation.Id);
        quote.AcceptedOperationId = operation.Id;
        return View(operation, 202);
    }
    private BrokerReply SubmitReturn(BrokerState s, CredentialRecord c, ReturnInput r, JsonElement body, string? key, DateTimeOffset now)
    {
        var replay = Replay(s, c, "/returns", key, r.ClientOperationId, body);
        if (replay is not null) return View(replay, 202);
        MatchIdentity(c, r.Character, r.GameServer);
        RequireId(r.JourneyId);
        if (r.Items is null || r.Items.Any(i => i.Origin is not ("exchanger_supply" or "journey_surplus")) || r.SupplyRequestIds is null
            || r.SupplyRequestIds.Count > 32 || r.SupplyRequestIds.Distinct().Count() != r.SupplyRequestIds.Count)
            throw new BrokerFault(400, "INVALID_REQUEST");
        var items = r.Items.Select(i => new ItemQuantity(i.Item, i.Quantity)).ToList();
        ValidateItems(items);
        if (!IsReady(s, now) || !s.Catalog.ReturnSupported) throw new BrokerFault(503, "SERVICE_UNAVAILABLE");
        foreach (var id in r.SupplyRequestIds)
            if (!s.Operations.Any(o => o.Id == id && o.Kind == "supply" && Owns(s, c, o))) throw new BrokerFault(404, "NOT_FOUND");
        if (r.Items.Any(i => i.Origin == "exchanger_supply") && r.SupplyRequestIds.Count == 0) throw new BrokerFault(422, "INVALID_REQUEST");
        var operation = CreateOperation(s, c, r.ClientOperationId, r.JourneyId, "return", items, body, now);
        Remember(s, c, "/returns", key!, r.ClientOperationId, body, operation.Id);
        return View(operation, 202);
    }
    private BrokerReply MutateOperation(BrokerState s, CredentialRecord c, OperationRecord operation, string action, JsonElement body, string? key, DateTimeOffset now)
    {
        var route = "/" + operation.Kind + "/" + operation.Id + "/" + action;
        if (action == "cancel")
        {
            var r = Wire.Read<FindInput>(body);
            var replay = Replay(s, c, route, key, r.ClientOperationId, body);
            if (replay is not null) return View(replay, 202);
            operation.CancellationRequested = true;
            AddJob(s, "cancel-uncommitted", operation.Id, now);
            Remember(s, c, route, key!, r.ClientOperationId, body, operation.Id);
            return View(operation, 202);
        }
        if (action != (operation.Kind == "supply" ? "receipts" : "sent")) throw new BrokerFault(404, "NOT_FOUND");
        var report = Wire.Read<ReportInput>(body);
        var previous = Replay(s, c, route, key, report.ClientOperationId, body);
        if (previous is not null) return View(previous, 202);
        if (report.UnitIds is null || report.UnitIds.Count is < 1 or > 100 || report.UnitIds.Distinct().Count() != report.UnitIds.Count
            || report.UnitIds.Any(id => !operation.Units.Any(u => u.Id == id))) throw new BrokerFault(422, "INVALID_REQUEST");
        if (operation.Kind == "return" && operation.State == "submitted") throw new BrokerFault(409, "RETURN_NOT_REGISTERED");
        foreach (var unit in operation.Units.Where(u => report.UnitIds.Contains(u.Id))) unit.ClientReported = true;
        AddJob(s, "reconcile-operation", operation.Id, now);
        Remember(s, c, route, key!, report.ClientOperationId, body, operation.Id);
        return View(operation, 202);
    }
    private BrokerReply Recover(BrokerState s, CredentialRecord grant, RecoveryInput r, DateTimeOffset now)
    {
        if (r.InstallationIds is null || r.InstallationIds.Count is < 1 or > 32 || r.InstallationIds.Any(id => id == Guid.Empty || id == grant.InstallationId)
            || r.InstallationIds.Distinct().Count() != r.InstallationIds.Count) throw new BrokerFault(400, "INVALID_REQUEST");
        var access = s.Recoveries.SingleOrDefault(a => a.Subject == grant.Subject && a.InstallationId == grant.InstallationId);
        if (access is null)
        {
            access = new RecoveryRecord { Subject = grant.Subject, InstallationId = grant.InstallationId };
            s.Recoveries.Add(access);
        }
        foreach (var id in r.InstallationIds)
        {
            if (!access.OriginalInstallations.Contains(id)) access.OriginalInstallations.Add(id);
            RevokeInstallation(s, grant.Subject, id, now);
        }
        grant.Revoked = true;
        return Ok(new { state = "recovered" });
    }
    private void RevokeInstallation(BrokerState s, string subject, Guid installation, DateTimeOffset now)
    {
        foreach (var c in s.Credentials.Where(c => c.Subject == subject && c.InstallationId == installation)) c.Revoked = true;
        foreach (var operation in s.Operations.Where(o => o.Subject == subject && o.InstallationId == installation && !Terminal(o)))
        {
            operation.Revoked = true;
            operation.CancellationRequested = true;
            if (!s.Jobs.Any(j => j.Kind == "cancel-uncommitted" && j.TargetId == operation.Id && !j.Completed))
                AddJob(s, "cancel-uncommitted", operation.Id, now);
        }
    }
    private OperationRecord CreateOperation(BrokerState s, CredentialRecord c, Guid clientId, Guid journeyId, string kind, List<ItemQuantity> items, JsonElement body, DateTimeOffset now)
    {
        if (s.Operations.Count >= options.MaxOperations || s.Mutations.Count >= options.MaxOperations * 8L) throw new BrokerFault(429, "HISTORY_CAPACITY_REACHED");
        var operation = new OperationRecord
        {
            Id = Guid.NewGuid(), ClientOperationId = clientId, JourneyId = journeyId, BindingId = c.PairingId,
            InstallationId = c.InstallationId, Subject = c.Subject, Character = c.Character, GameServer = c.GameServer,
            Kind = kind, SubmittedAt = now, CredentialExpiresAt = c.ExpiresAt, Manifest = body.Clone(),
            Units = items.SelectMany(i => Enumerable.Range(0, i.Quantity).Select(_ => new UnitRecord { Id = Guid.NewGuid(), Item = i.Item })).ToList(),
        };
        AddJob(s, kind == "supply" ? "reserve-supply" : "register-return", operation.Id, now);
        s.Operations.Add(operation);
        return operation;
    }
    private OperationRecord? Replay(BrokerState s, CredentialRecord c, string route, string? key, Guid clientId, JsonElement body)
    {
        RequireId(clientId);
        if (key is null || !Guid.TryParse(key, out var parsed) || parsed == Guid.Empty) throw new BrokerFault(400, "IDEMPOTENCY_KEY_REQUIRED");
        var records = s.Mutations.Where(m => m.Subject == c.Subject && HasInstallationAccess(s, c, m.InstallationId));
        var byKey = records.SingleOrDefault(m => m.Key == key);
        var byClient = records.SingleOrDefault(m => m.ClientOperationId == clientId);
        if (byKey is null && byClient is null) return null;
        var match = byKey ?? byClient!;
        if (match.BodyHash != BodyHash(body) || match.Route != route || match.ClientOperationId != clientId
            || byKey is not null && byClient is not null && byKey.TargetId != byClient.TargetId) throw new BrokerFault(409, "IDEMPOTENCY_CONFLICT");
        return s.Operations.Single(o => o.Id == match.TargetId);
    }
    private void Remember(BrokerState s, CredentialRecord c, string route, string key, Guid clientId, JsonElement body, Guid target)
    {
        if (s.Mutations.Count >= options.MaxOperations * 8L) throw new BrokerFault(429, "HISTORY_CAPACITY_REACHED");
        s.Mutations.Add(new MutationRecord { Subject = c.Subject, InstallationId = c.InstallationId, Route = route, Key = key,
            ClientOperationId = clientId, BodyHash = BodyHash(body), TargetId = target });
    }
    private void AddJob(BrokerState s, string kind, Guid target, DateTimeOffset now)
    {
        s.Jobs.RemoveAll(j => j.Completed);
        if (kind is "cancel-uncommitted" or "reconcile-operation" && s.Jobs.Any(j => j.Kind == kind && j.TargetId == target)) return;
        if (s.Jobs.Count >= options.MaxQueuedJobs) throw new BrokerFault(429, "RATE_LIMITED");
        s.Jobs.Add(new JobRecord { Id = Guid.NewGuid(), Kind = kind, TargetId = target, CreatedAt = now });
    }
    private void ValidateItems(List<ItemQuantity> items, bool quoting = false)
    {
        if (items is null || items.Count is < 1 or > 32 || items.Any(i => i is null)
            || items.Select(i => i.Item).Distinct(StringComparer.Ordinal).Count() != items.Count)
            throw new BrokerFault(422, "INVALID_REQUEST");
        long total = 0;
        foreach (var item in items)
        {
            if (!options.CatalogLimits.TryGetValue(item.Item, out var cap)) throw new BrokerFault(422, "UNSUPPORTED_ITEM");
            if (item.Quantity < 1 || item.Quantity > (quoting ? 100 : cap)) throw new BrokerFault(422, "INVALID_QUANTITY");
            total += item.Quantity;
        }
        if (total > options.MaxUnitsPerOperation) throw new BrokerFault(422, "INVALID_QUANTITY");
    }
}
