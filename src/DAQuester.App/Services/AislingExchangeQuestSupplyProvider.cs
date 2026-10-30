using DAQuester.Core;

namespace DAQuester.App.Services;

public sealed class AislingExchangeQuestSupplyProvider : IQuestSupplyProvider, IQuestSupplyReturnProvider
{
    private readonly AislingExchangeClient client;
    private readonly string character;
    private readonly string serverProfile;
    private readonly Func<bool> enabled;
    private readonly Func<CancellationToken, Task>? ensurePaired;

    public AislingExchangeQuestSupplyProvider(AislingExchangeClient client, string character, string serverProfile, Func<bool> enabled,
        Func<CancellationToken, Task>? ensurePaired = null)
    {
        this.client = client;
        this.character = character;
        this.serverProfile = serverProfile;
        this.enabled = enabled;
        this.ensurePaired = ensurePaired;
    }

    public bool Enabled => enabled();

    public async Task<QuestSupplyQuote?> QuoteAsync(QuestSupplyDemand demand, CancellationToken cancellation)
    {
        RequireSubject(demand.Character, demand.ServerProfile, demand.JourneyId);
        if (!Enabled)
            return null;
        if (ensurePaired is not null)
            await ensurePaired(cancellation);
        var quote = await client.QuoteAsync(new(demand.Character, demand.ServerProfile, demand.JourneyId.ToString("D"),
            demand.Items.Select(item => new AislingExchangeSupplyLine(item.Item, item.Quantity)).ToArray()), cancellation);
        if (quote.Items.Count == 0)
            return null;
        return new(quote.Id, quote.Character, quote.ServerProfile, quote.ExpiresAt,
            quote.Items.Select(item => new QuestSupplyLine(item.Item, item.Quantity)).ToArray());
    }

    public async Task<QuestSupplyRequestStatus> SubmitAsync(QuestSupplySubmission submission, CancellationToken cancellation)
    {
        RequireSubject(submission.Character, submission.ServerProfile, submission.JourneyId);
        if (!Enabled)
            throw new QuestBlockedException("AislingExchange supplies are disabled; the saved request remains available for reconciliation.");
        if (ensurePaired is not null)
            await ensurePaired(cancellation);
        return Map(await client.SubmitAsync(new(submission.ClientOperationId, submission.JourneyId.ToString("D"), submission.Character,
            submission.ServerProfile, submission.QuoteId, submission.Items.Select(item => new AislingExchangeSupplyLine(item.Item, item.Quantity)).ToArray()), cancellation));
    }

    public async Task<QuestSupplyRequestStatus?> FindAsync(Guid clientOperationId, CancellationToken cancellation)
    {
        // Turning off future requests never discards an already submitted obligation.
        if (ensurePaired is not null)
            await ensurePaired(cancellation);
        var status = await client.FindAsync(clientOperationId, character, serverProfile, cancellation);
        return status is null ? null : Map(status);
    }

    public async Task<QuestSupplyRequestStatus> ReportReceiptsAsync(QuestSupplyUnitReport report, CancellationToken cancellation)
    {
        RequireReportSubject(report);
        if (ensurePaired is not null) await ensurePaired(cancellation);
        return Map(await client.ReportReceiptsAsync(Report(report), cancellation));
    }

    public async Task<bool> ReturnsAvailableAsync(CancellationToken cancellation)
    {
        if (!Enabled) return false;
        if (ensurePaired is not null) await ensurePaired(cancellation);
        return await client.ReturnsAvailableAsync(character, serverProfile, cancellation);
    }

    public async Task<QuestSupplyRequestStatus> RegisterReturnAsync(QuestSupplyReturnSubmission submission, CancellationToken cancellation)
    {
        RequireSubject(submission.Character, submission.ServerProfile, submission.JourneyId);
        if (!Enabled)
            throw new QuestBlockedException("AislingExchange supplies are disabled; existing returns remain available for reconciliation.");
        if (ensurePaired is not null) await ensurePaired(cancellation);
        return Map(await client.RegisterReturnAsync(new(submission.ClientOperationId, submission.JourneyId.ToString("D"),
            submission.Character, submission.ServerProfile, submission.SupplyRequestIds,
            submission.Items.Select(item => new AislingExchangeReturnLine(item.Item, item.Quantity, item.Origin)).ToArray()), cancellation));
    }

    public async Task<QuestSupplyRequestStatus?> FindReturnAsync(Guid clientOperationId, CancellationToken cancellation)
    {
        if (ensurePaired is not null) await ensurePaired(cancellation);
        var status = await client.FindReturnAsync(clientOperationId, character, serverProfile, cancellation);
        return status is null ? null : Map(status);
    }

    public async Task<QuestSupplyRequestStatus> ReportReturnSentAsync(QuestSupplyUnitReport report, CancellationToken cancellation)
    {
        RequireReportSubject(report);
        if (ensurePaired is not null) await ensurePaired(cancellation);
        return Map(await client.ReportReturnSentAsync(Report(report), cancellation));
    }

    void RequireReportSubject(QuestSupplyUnitReport report)
    {
        if (report.Character != character || report.ServerProfile != serverProfile)
            throw new QuestBlockedException("AislingExchange receipt identity does not match this character.");
    }

    static AislingExchangeUnitReport Report(QuestSupplyUnitReport report) => new(report.ClientOperationId, report.OperationClientId,
        report.OperationId, report.Character, report.ServerProfile, report.UnitIds);

    private void RequireSubject(string requestedCharacter, string requestedServer, Guid journey)
    {
        if (requestedCharacter != character || requestedServer != serverProfile || journey == Guid.Empty)
            throw new QuestBlockedException("AislingExchange supply identity does not match this journey.");
    }

    private static QuestSupplyRequestStatus Map(AislingExchangeRequestStatus status)
        => new(status.Id, status.ClientOperationId, status.Character, status.ServerProfile, status.State,
            status.Units.Select(unit => new QuestSupplyParcelUnit(unit.Id, unit.Item, unit.SourceAccepted, unit.Cancelled)
            {
                RecipientObserved = unit.RecipientObserved, Collected = unit.Collected,
                Banked = unit.Banked, ClientReported = unit.ClientReported
            }).ToArray());
}
