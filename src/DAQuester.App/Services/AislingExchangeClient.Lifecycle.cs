using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed partial class AislingExchangeClient
{
    public Task<AislingExchangeRequestStatus> ReportReceiptsAsync(AislingExchangeUnitReport report, CancellationToken cancellation = default)
        => ReportUnitsAsync(report, false, cancellation);

    public Task<AislingExchangeRequestStatus> ReportReturnSentAsync(AislingExchangeUnitReport report, CancellationToken cancellation = default)
        => ReportUnitsAsync(report, true, cancellation);

    async Task<AislingExchangeRequestStatus> ReportUnitsAsync(AislingExchangeUnitReport report, bool returns, CancellationToken cancellation)
    {
        if (report is null || report.ClientOperationId == Guid.Empty || report.OperationClientId == Guid.Empty
            || !ObjectId(report.OperationId) || report.UnitIds is null || report.UnitIds.Count is < 1 or > 100
            || report.UnitIds.Any(id => !ObjectId(id)) || report.UnitIds.Select(Guid.Parse).Distinct().Count() != report.UnitIds.Count)
            throw InvalidRequest();
        ValidateSubject(report.Character, report.ServerProfile);
        var credential = await CredentialAsync(report.Character, report.ServerProfile, cancellation);
        var root = RequiredRoute(returns ? routes.Returns : routes.Receipts);
        var endpoint = PairingEndpoint(root, Guid.Parse(report.OperationId).ToString("D"), returns ? "sent" : "receipts");
        var body = new UnitReportBody(report.ClientOperationId, report.UnitIds.Select(id => Guid.Parse(id).ToString("D")).ToArray());
        var status = (await SendAsync<AislingExchangeRequestStatus>(endpoint, body, credential, true, false, cancellation, report.ClientOperationId))!;
        ValidateStatus(status, report.OperationClientId, report.Character, report.ServerProfile, true, returns);
        if (returns && status.State == "submitted" || !SameObjectId(status.Id, report.OperationId) || report.UnitIds.Any(id => !status.Units.Any(unit =>
            SameObjectId(unit.Id, id) && unit.ClientReported && (returns || unit.SourceAccepted && !unit.Cancelled))))
            throw InvalidResponse(true);
        return status;
    }

    public async Task<bool> ReturnsAvailableAsync(string character, string serverProfile, CancellationToken cancellation = default)
    {
        ValidateSubject(character, serverProfile);
        var credential = await CredentialAsync(character, serverProfile, cancellation);
        var capability = (await SendAsync<CapabilitiesBody>(RequiredRoute(routes.Capabilities), null, credential, false, false, cancellation, method: HttpMethod.Get))!;
        if (capability.GameServer != serverProfile || capability.Receiver != "Exchanger"
            || capability.ReceiveModes is null || capability.ReceiveModes.Length > 2
            || capability.ReceiveModes.Distinct(StringComparer.Ordinal).Count() != capability.ReceiveModes.Length
            || capability.ReceiveModes.Any(mode => mode is not ("single-unit" or "receive-all"))
            || capability.Items is null || capability.Items.Length > 43
            || capability.Items.Any(item => item is null || !Item(item.Item) || item.MaxPerRequest is < 1 or > 100)
            || capability.Items.Select(item => item.Item).Distinct(StringComparer.Ordinal).Count() != capability.Items.Length)
            throw InvalidResponse();
        return capability.Ready && capability.ReturnSupported;
    }

    public async Task<AislingExchangeRequestStatus> RegisterReturnAsync(AislingExchangeReturnSubmission submission, CancellationToken cancellation = default)
    {
        if (submission is null || submission.ClientOperationId == Guid.Empty || !ObjectId(submission.JourneyId)
            || submission.SupplyRequestIds is null || submission.SupplyRequestIds.Count > 32
            || submission.SupplyRequestIds.Any(id => !ObjectId(id))
            || submission.SupplyRequestIds.Select(Guid.Parse).Distinct().Count() != submission.SupplyRequestIds.Count
            || submission.Items is null || submission.Items.Any(item => item is null || item.Origin is not ("exchanger_supply" or "journey_surplus"))
            || submission.Items.Any(item => item.Origin == "exchanger_supply") && submission.SupplyRequestIds.Count == 0)
            throw InvalidRequest();
        ValidateSubject(submission.Character, submission.ServerProfile);
        var items = submission.Items.Select(item => new AislingExchangeSupplyLine(item.Item, item.Quantity)).ToArray();
        ValidateLines(items);
        var credential = await CredentialAsync(submission.Character, submission.ServerProfile, cancellation);
        var status = (await SendAsync<AislingExchangeRequestStatus>(RequiredRoute(routes.Returns), submission, credential, true, false, cancellation, submission.ClientOperationId))!;
        ValidateStatus(status, submission.ClientOperationId, submission.Character, submission.ServerProfile, true, true);
        if (status.Units.Any(unit => !items.Any(line => line.Item == unit.Item))
            || status.Units.GroupBy(unit => unit.Item).Any(group => group.Count() > items.Single(line => line.Item == group.Key).Quantity))
            throw InvalidResponse(true);
        return status;
    }

    public async Task<AislingExchangeRequestStatus?> FindReturnAsync(Guid clientOperationId, string character, string serverProfile, CancellationToken cancellation = default)
    {
        ValidateSubject(character, serverProfile);
        if (clientOperationId == Guid.Empty) throw InvalidRequest();
        var credential = await CredentialAsync(character, serverProfile, cancellation);
        var status = await SendAsync<AislingExchangeRequestStatus>(routes.Find, new FindBody(clientOperationId), credential, false, true, cancellation);
        if (status is not null) ValidateStatus(status, clientOperationId, character, serverProfile, false, true);
        return status;
    }

    static bool ObjectId(string? id) => Guid.TryParseExact(id, "D", out var value) && value != Guid.Empty;
    static bool SameObjectId(string left, string right) => ObjectId(left) && ObjectId(right) && Guid.Parse(left) == Guid.Parse(right);
    static Uri RequiredRoute(Uri? route) => route ?? throw new AislingExchangeException("AislingExchange service configuration is incomplete.");
    sealed record UnitReportBody(Guid ClientOperationId, string[] UnitIds);
    sealed record CapabilitiesBody(string GameServer, string Receiver, bool Ready, bool ReturnSupported, string[] ReceiveModes, CapabilityItem[] Items);
    sealed record CapabilityItem(string Item, int MaxPerRequest);
}
