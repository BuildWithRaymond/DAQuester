namespace DAQuester.Core;

static partial class ObservedJourney
{
    const string GuideQuestion = "Come to give guidance to a new Aisling who is choosing his or her Craft?";
    const string RespectQuestion = "Will you be respectful of the monastery?";
    static bool IsArrival(ObservedQuestTask task, CharacterSnapshot s) => s.Map is { } map && map != task.Map && s.Position is not null &&
        (task.ArrivalMap == map || task.ArrivalMaps.Contains(map));
    static bool InPath(int? map) => map is 394 or 395 or 396 or 397 or 399 or 342 or 343 or 344 or 345 or 346 or 347 or 348;
    static bool IsAccessDialog(CharacterSnapshot s, ObservedQuestTask task) =>
        task.ApproachStrategy == "cedar-grove" && s.Map == 5220 && s.Dialog is { EntityType: 4, Text: RespectQuestion } ||
        task.ApproachStrategy == "path-hall" && s.Map == 393 && s.Dialog is { Name: "Aoife", Text: GuideQuestion };

    static async Task MoveAsync(QuestContext c, int map, Tile tile, CancellationToken ct)
    {
        var s = c.Session.Snapshot;
        if (s.Map == map && s.Position == tile)
            return;
        NpcTravelCheckpoint? redirect = null;
        void Interrupted(string reason)
        {
            if (c.Job.Quest == DanaanEvidence.Id && c.Job.Pending is null && c.Job.Route?.Baseline is null && reason == DanaanTransport.RedirectReason &&
                c.Session is INpcConnectionHandoff handoff && handoff.SubmittedNpcTravel is { } submitted &&
                string.Equals(submitted.Character, c.Job.Character, StringComparison.OrdinalIgnoreCase))
                redirect = submitted;
        }
        c.Session.Interrupted += Interrupted;
        try
        {
            await c.Session.ExecuteAsync(new("navigate", map, tile, ExpectedGeneration: s.Generation), ct);
        }
        catch
        {
            if (c.Job.Quest == DanaanEvidence.Id && c.Job.Pending is null && c.Job.Route?.Baseline is null &&
                (redirect ?? (c.Session as INpcConnectionHandoff)?.SubmittedNpcTravel) is { } submitted)
                await c.SaveAsync(c.Job with
                {
                    Route = c.Job.Route! with
                    {
                        TravelRedirect = redirect,
                        UnconfirmedTravel = redirect is null ? submitted : null
                    }
                });
            throw;
        }
        finally { c.Session.Interrupted -= Interrupted; }
        if (c.Session.Snapshot.Map != map || c.Session.Snapshot.Position != tile)
            throw new QuestBlockedException("Travel did not establish its actual destination; inspect the landing before continuing.");
    }
    static async Task EnterAsync(QuestContext c, int source, Tile approach, Tile door, int destination, Tile inward, CancellationToken ct)
    {
        // Entry includes leaving the trigger immediately. An acknowledged landing
        // is not a stable place to pause or open the next NPC conversation.
        if (c.Session.Snapshot.Map != source)
            throw new QuestBlockedException("Doorway source map changed; inspect it before moving.");
        await MoveAsync(c, source, approach, ct);
        await c.Session.ExecuteAsync(new("enter", destination, door, InwardTarget: inward, ExpectedGeneration: c.Session.Snapshot.Generation), ct);
        if (c.Session.Snapshot.Map != destination || c.Session.Snapshot.Position != inward)
            throw new QuestBlockedException("Doorway arrival or inward receipt differs from the observed route.");
    }
    static async Task PrepareTravelAsync(QuestContext c, ObservedQuestTask task, CancellationToken ct)
    {
        // Settle any submitted fare or access consent before another approach.
        // A timeout supplies no permission to buy passage or select Yes again.
        var current = c.Session.Snapshot;
        if (DanaanTransport.CanResumeTravel(c.Job, current))
            await c.SaveAsync(c.Job with
            {
                Route = c.Job.Route! with
                {
                    TravelRedirect = null,
                    UnconfirmedTravel = null
                },
                Reason = "Recorded NPC redirect and own arrival reconciled"
            });
        else if (c.Job.Route?.TravelRedirect is not null)
            throw new QuestBlockedException("Expected travel redirect destination is not confirmed; no fare is repeated.");
        if (c.Job.Route?.UnconfirmedTravel is { } unknown)
        {
            if (!current.Connected || current.Generation != unknown.Generation || current.Map != unknown.Map || current.Position is null || current.Revision <= unknown.Revision)
                throw new QuestBlockedException(unknown.Npc + " travel still needs its actual arrival receipt; no fare is repeated.");
            await c.SaveAsync(c.Job with
            {
                Route = c.Job.Route! with
                {
                    UnconfirmedTravel = null
                }
            });
        }
        if (c.Job.Pending is { } p && p.Kind.StartsWith("observed-access:", StringComparison.Ordinal))
        {
            int arrival = int.Parse(p.Kind["observed-access:".Length..], System.Globalization.CultureInfo.InvariantCulture);
            if (c.Session.Snapshot.Map != arrival && !(arrival == 394 && InPath(c.Session.Snapshot.Map)))
                throw new QuestBlockedException("Access consent was submitted but arrival is uncertain; no second consent sent.");
            if (c.Session.Snapshot.Map == arrival)
            {
                var landing = c.Session.Snapshot.Position ?? throw new QuestBlockedException("Access arrival position is still unknown.");
                var away = arrival == 394 ? new Tile(landing.X - 1, landing.Y) : new Tile(landing.X, landing.Y - 1);
                if (c.Session is not ICombatMap map || !map.IsWalkable(away) || map.IsWarp(away))
                    throw new QuestBlockedException("Access arrival has no validated adjacent away tile; consent remains committed.");
                await AwayAsync(c, away, arrival, ct);
            }
            await c.SaveAsync(c.Job with
            {
                Pending = null
            });
        }
        if (c.Session.Snapshot.Map == 348 && task.Map != 348)
        {
            await EnterAsync(c, 348, new(10, 5), new(11, 5), 344, new(2, 6), ct);
            await MoveAsync(c, 344, new(6, 8), ct);
        }
        if (c.Session.Snapshot.Map is 177 or 205 or 204 or 169 && task.ApproachStrategy != "inn-room7")
            await ExitInnAsync(c, ct);
        if (c.Session.Snapshot.Map == 195 && task.Map != 195)
            await EnterAsync(c, 195, new(8, 5), new(9, 5), 185, new(9, 29), ct);
        switch (task.ApproachStrategy)
        {
            case "inn-room7":
                await Room7Async(c, ct);
                break;
            case "coliseum":
                await ColiseumAsync(c, ct);
                break;
            case "path-hall":
                await PathAccessAsync(c, ct);
                break;
            case "cedar-grove":
                await CedarAsync(c, ct);
                break;
            case "coast-bar":
                if (c.Session.Snapshot.Map != 195)
                {
                    await MoveAsync(c, 185, new(8, 29), ct);
                    await EnterAsync(c, 185, new(8, 29), new(7, 29), 195, new(7, 5), ct);
                }
                break;
        }
        if (task.Approach is { } target)
        {
            try
            {
                await MoveAsync(c, task.Map, target, ct);
            }
            catch (QuestBlockedException) when (task.Map == 345 && c.Session.Snapshot.Map == 395 && c.Session.Snapshot.Dialog is null)
            {
                // Actual 395 stairs bypass the recorded approach failure. No
                // Pattern Walker dialogue is selected, read or advanced here.
                await EnterAsync(c, 395, new(6, 2), new(6, 1), 396, new(6, 9), ct);
                await MoveAsync(c, 345, target, ct);
            }
        }
    }
    static async Task Room7Async(QuestContext c, CancellationToken ct)
    {
        if (c.Session.Snapshot.Map == 177)
        {
            await MoveAsync(c, 177, new(2, 3), ct);
            return;
        }
        if (c.Session.Snapshot.Map is not (169 or 204 or 205))
        {
            await MoveAsync(c, 502, new(58, 39), ct);
            await EnterAsync(c, 502, new(58, 39), new(58, 38), 169, new(5, 9), ct);
        }
        if (c.Session.Snapshot.Map == 169)
            await EnterAsync(c, 169, new(1, 10), new(0, 10), 204, new(3, 12), ct);
        if (c.Session.Snapshot.Map == 204)
            await EnterAsync(c, 204, new(3, 1), new(3, 0), 205, new(3, 3), ct);
        if (c.Session.Snapshot.Map == 205)
            await EnterAsync(c, 205, new(4, 1), new(4, 0), 177, new(4, 5), ct);
        await MoveAsync(c, 177, new(2, 3), ct); // One (4,5) ack alone did not prevent pullback.
    }
    static async Task ExitInnAsync(QuestContext c, CancellationToken ct)
    {
        if (c.Session.Snapshot.Map == 177)
        {
            await EnterAsync(c, 177, new(4, 6), new(4, 7), 205, new(4, 2), ct);
            await MoveAsync(c, 205, new(2, 3), ct);
        }
        if (c.Session.Snapshot.Map == 205)
            await EnterAsync(c, 205, new(2, 3), new(2, 4), 204, new(3, 2), ct);
        if (c.Session.Snapshot.Map == 204)
            await EnterAsync(c, 204, new(3, 13), new(4, 13), 169, new(2, 10), ct);
        if (c.Session.Snapshot.Map == 169)
            await EnterAsync(c, 169, new(6, 10), new(6, 11), 502, new(59, 40), ct);
    }
    static async Task ColiseumAsync(QuestContext c, CancellationToken ct)
    {
        if (c.Session.Snapshot.Map == 5232)
            return;
        if (c.Session.Snapshot.Map != 5231)
        {
            // Recorded world arrivals point into the lower component; this live
            // route actually arrived upstairs. Reconcile that precise mismatch.
            try
            {
                await MoveAsync(c, 5231, new(2, 5), ct);
            }
            catch (QuestBlockedException) when (c.Session.Snapshot.Map == 5231 && c.Session.Snapshot.Position is { Y: <= 1 }) { }
        }
        if (c.Session.Snapshot.Position is not { Y: <= 1 })
            throw new QuestBlockedException("Coliseum Threshold lower component has no observed safe link upstairs. Inspect the actual arrival; no portal is invented.");
        await EnterAsync(c, 5231, new(0, 1), new(0, 0), 5232, new(4, 11), ct);
    }
    static async Task SelectAccessAsync(QuestContext c, Dialog d, string choice, int arrival, CancellationToken ct)
    {
        await c.BeginAsync("observed-access:" + arrival, d.EntityId, observedDialog: d);
        await c.Session.ExecuteAsync(new("choose", Text: choice, Dialog: d, ExpectedGeneration: d.Generation, ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
        if (c.Session.Snapshot.Map != arrival || c.Session.Snapshot.Position is null)
            await c.Session.WaitAsync(s => s.Generation == d.Generation && s.Map == arrival && s.Position is not null, d.Revision, ProofTimeout, ct);
        await c.SaveAsync(c.Job with
        {
            Pending = null
        });
    }
    static async Task PathAccessAsync(QuestContext c, CancellationToken ct)
    {
        if (InPath(c.Session.Snapshot.Map))
            return;
        await MoveAsync(c, 393, new(3, 5), ct);
        var d = c.Session.Snapshot.Dialog;
        if (d is not { Name: "Aoife", Text: GuideQuestion })
        {
            await c.Session.ExecuteAsync(new("open", Text: "Aoife", ExpectedGeneration: c.Session.Snapshot.Generation), ct);
            d = c.Session.Snapshot.Dialog;
            if (d is not { Name: "Aoife", IsPursuit: false } || !d.Choices.Any(x => x.Text == "Guide Reception"))
                throw new QuestBlockedException("Aoife has no current Guide Reception option.");
            await c.Session.ExecuteAsync(new("choose", Text: "Guide Reception", Dialog: d, ExpectedGeneration: d.Generation, ExpectedDialogRevision: d.Revision, ExpectedPageText: d.Text), ct);
            if (c.Session.Snapshot.Dialog is { } same && same.Revision <= d.Revision)
                await c.Session.WaitAsync(s => s.Generation != d.Generation || s.Map != 393 || s.Dialog is null || s.Dialog.Revision > d.Revision, d.Revision, ProofTimeout, ct);
            d = c.Session.Snapshot.Dialog;
        }
        if (d is not { Name: "Aoife", Text: GuideQuestion } || !d.Choices.Any(x => x.Text == "Yes"))
            throw new QuestBlockedException("Aoife's guidance confirmation changed; no dedication option selected.");
        await SelectAccessAsync(c, d, "Yes", 394, ct);
        var landing = c.Session.Snapshot.Position!;
        var away = new Tile(landing.X - 1, landing.Y);
        if (c.Session is not ICombatMap map || !map.IsWalkable(away) || map.IsWarp(away))
            throw new QuestBlockedException("Sitting Room arrival has no validated adjacent away tile.");
        await AwayAsync(c, away, 394, ct);
    }
    static async Task CedarAsync(QuestContext c, CancellationToken ct)
    {
        if (c.Session.Snapshot.Map == 5210)
            return;
        var s = c.Session.Snapshot;
        if (s.Map != 5220 || s.Dialog is not { EntityType: 4, Text: RespectQuestion })
        {
            await MoveAsync(c, 5220, new(1, 7), ct);
            var before = c.Session.Snapshot;
            await c.Session.ExecuteAsync(new("step", 5220, new(0, 7), ExpectedGeneration: c.Session.Snapshot.Generation), ct);
            if (c.Session.Snapshot.Dialog is null)
                await c.Session.WaitAsync(s => s.Generation != before.Generation || s.Map != 5220 || s.Dialog is not null, before.Revision, ProofTimeout, ct);
        }
        var d = c.Session.Snapshot.Dialog;
        if (d is not { EntityType: 4, Text: RespectQuestion } || !d.Choices.Any(x => x.Text == "Yes"))
            throw new QuestBlockedException("Fresh monastery respect menu is missing; no guessed reactor selection sent.");
        await SelectAccessAsync(c, d, "Yes", 5210, ct);
        var landing = c.Session.Snapshot.Position!;
        await AwayAsync(c, new(landing.X, landing.Y - 1), 5210, ct);
    }
}
