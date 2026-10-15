namespace DAQuester.Core;

static partial class ObservedJourney
{
    static async Task DismissDonnanReportAsync(QuestContext c, QuestDefinition recipe, CancellationToken ct)
    {
        if (c.Job.DonnanAssignment is not { Report: { } report } receipt) return;
        var journey = JourneyFor(c, recipe);
        var assignment = journey.SingleOrDefault(t => t.Id == "donnan-next-task");
        if (c.Job.Quest != DanaanEvidence.Id || c.Job.DanaanOptions is not { Avatar: "cail", SuppliedMoldRiteAccepted: true } ||
            assignment is null || !DanaanDonnanRecovery.Valid(receipt, assignment) || !DanaanDonnanRecovery.IsReport(report) ||
            c.Job.Route is not { Task: var index } || index < 0 || index >= journey.Length || journey[index].Id != "kyros-request" ||
            c.Job.Pending is { Kind: not "donnan-report-close" })
            throw new QuestBlockedException("Donnan assignment cleanup does not match the saved Kyros continuation.");
        var current = c.Session.Snapshot;
        if (current.Dialog is not null)
        {
            if (!current.Connected || current.Map != 348 || current.Generation != report.Generation ||
                !DanaanDonnanRecovery.Same(current.Dialog, report))
                throw new QuestBlockedException("Another popup is open; Donnan's recorded assignment stays preserved.");
            if (c.Job.Pending is not null)
                throw new QuestBlockedException("Donnan report close is not confirmed; no close repeated.");
            await c.BeginAsync("donnan-report-close", report.EntityId, observedDialog: report);
            await c.Session.ExecuteAsync(new("close", Dialog: report, ExpectedGeneration: report.Generation,
                ExpectedDialogRevision: report.Revision, ExpectedPageText: report.Text), ct);
            if (c.Session.Snapshot.Dialog is not null)
                throw new QuestBlockedException("Donnan report close is not confirmed; no quest choice repeated.");
        }
        await c.SaveAsync(c.Job with { Pending = null, DonnanAssignment = receipt with { Report = null } });
    }
}
