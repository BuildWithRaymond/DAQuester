namespace DAQuester.Core;

public sealed class ShinewoodFaerieActions : IQuestAction
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    public async Task ExecuteAsync(QuestContext c, QuestStep step, CancellationToken ct)
    {
        if (!c.Session.Snapshot.Connected || !string.Equals(c.Session.Snapshot.Name, c.Job.Character, StringComparison.OrdinalIgnoreCase))
            throw new QuestBlockedException("Connected quest character required before reconciling or continuing the Faerie visit.");
        switch (step.Action)
        {
            case "faerie-preflight":
                await Preflight(c, ct);
                break;
            case "faerie-visit":
                await Visit(c, ct);
                break;
            case "faerie-return":
                await Return(c, ct);
                break;
            default:
                throw new QuestBlockedException("Unsupported Faerie action.");
        }
    }

    static void Gate(QuestContext c)
    {
        var e = ShinewoodFaerieEvidence.Eligibility(c.Session.Snapshot, c.Job);
        if (e.Kind != EligibilityKind.Eligible)
            throw new QuestBlockedException(e.Reason);
    }

    static async Task Preflight(QuestContext c, CancellationToken ct)
    {
        Gate(c);
        if (c.Job.Pending is not null)
            throw new QuestBlockedException("Saved Faerie submission needs reconciliation before a new visit.");
        if (c.Session.Snapshot.Class is null)
            await c.Session.ExecuteAsync(new("inspect-self"), ct);
        var goals = ShinewoodFaerieEvidence.Goals(c.Session.Snapshot, c.Job);
        if (goals.Length > 0 || ShinewoodFaerieEvidence.InForest(c.Session.Snapshot.Map))
        {
            ShinewoodFaerieEvidence.RequireEscape(c.Session.Snapshot, c.Job);
            if (ShinewoodFaerieEvidence.Escape(c.Session.Snapshot) is null)
                await c.Session.ExecuteAsync(new("check-faerie-return"), ct);
        }

        await c.SaveAsync(c.Job with
        {
            FaerieGoals = goals,
            FaerieUnavailable = [],
            FaerieCurrentGoal = null,
            Reason = "Visit the Faerie for a pet and eligible missing spells"
        });
    }

    static bool Satisfied(QuestContext c, string goal) => goal == ShinewoodFaerieEvidence.Pet ? ShinewoodFaerieEvidence.HasPet(c.Session.Snapshot) || c.Job.FaerieReceipts.Any(r => r.Goal == goal) : ShinewoodFaerieEvidence.HasSpell(c.Session.Snapshot, goal);
    static bool KnownClosed(CharacterSnapshot s, QuestJob job) => s.Dialog is { IsPursuit: false } d && job.FaerieClosedDialog is { IsPursuit: false } closed && d.Generation == s.Generation && d.Generation == closed.Generation && d.Revision == closed.Revision && d.EntityId == closed.EntityId && d.EntityType == closed.EntityType;
    static async Task Visit(QuestContext c, CancellationToken ct)
    {
        if (c.Job.Pending is not null)
            await Reconcile(c, ct);
        foreach (var goal in c.Job.FaerieGoals)
        {
            ct.ThrowIfCancellationRequested();
            if (Satisfied(c, goal) || c.Job.FaerieUnavailable.Contains(goal, StringComparer.OrdinalIgnoreCase))
                continue;
            Gate(c);
            ShinewoodFaerieEvidence.RequireEscape(c.Session.Snapshot, c.Job);
            if (c.Session.Snapshot.Dialog is not null && !KnownClosed(c.Session.Snapshot, c.Job))
                throw new QuestBlockedException("A popup is open. It is preserved; review it before visiting the Faerie.");
            await c.Session.ExecuteAsync(new("visit", ShinewoodFaerieEvidence.Map, ShinewoodFaerieEvidence.Anchor, Text: "Faerie"), ct);
            var s = c.Session.Snapshot;
            var npc = s.Entities.Values.Where(ShinewoodFaerieEvidence.FaerieEntity).ToArray();
            if (s.Map != ShinewoodFaerieEvidence.Map || npc.Length > 1 || npc.Length == 0 && s.Position != ShinewoodFaerieEvidence.Approach)
                throw new QuestBlockedException("A unique visible Faerie or her verified approach tile is required before saying Hello.");
            await c.SaveAsync(c.Job with
            {
                FaerieCurrentGoal = goal
            });
            await c.BeginAsync("faerie-hello", npc.SingleOrDefault()?.Id, "Greeting the Faerie for " + goal);
            await c.Session.ExecuteAsync(new("say", Text: "Hello"), ct);
            await Reply(c, ct);
            await Reconcile(c, ct);
        }

        if (c.Job.FaerieGoals.Any(g => !Satisfied(c, g) && !c.Job.FaerieUnavailable.Contains(g, StringComparer.OrdinalIgnoreCase)))
            throw new QuestBlockedException("Faerie rewards remain unconfirmed. Reconcile saved progress.");
    }

    static bool FreshPage(CharacterSnapshot s, Transaction t) => s.Generation == t.Generation && s.Map == ShinewoodFaerieEvidence.Map && s.Dialog is { } d && d.Generation == s.Generation && d.Revision > t.Revision && d.Revision <= s.Revision && (d.EntityId == t.EntityId || t.Kind == "faerie-hello" && d.EntityType == 4 && ShinewoodFaerieEvidence.RecognizedFaeriePage(d)) && d.EntityType is 1 or 4 && (t.EntityType is null || t.EntityType == d.EntityType) && d.Prompt is null && (d.Name.Equals("Faerie", StringComparison.OrdinalIgnoreCase) || d.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase) || d.Name.Length == 0);
    static Task Reply(QuestContext c, CancellationToken ct)
    {
        var t = c.Job.Pending!;
        var goal = c.Job.FaerieCurrentGoal;
        return c.Session.WaitAsync(s => FreshPage(s, t) || t.Kind == "faerie-goal" && goal is not null && s.Dialog is null && Reward(c, t, goal, s), t.Revision, Timeout, ct);
    }

    static bool Reward(QuestContext c, Transaction t, string goal, CharacterSnapshot? observed = null)
    {
        var s = observed ?? c.Session.Snapshot;
        if (!s.Connected || s.Name != c.Job.Character)
            return false;
        if (goal == ShinewoodFaerieEvidence.Pet)
            return t.InventoryBaselineKnown && t.InventoryCounts.GetValueOrDefault(goal) == 0 && s.InventoryKnown && ShinewoodFaerieEvidence.HasPet(s);
        return t.SpellsBaselineKnown && !t.Spells.Any(x => ShinewoodFaerieEvidence.SpellNameEquals(x, goal)) && ShinewoodFaerieEvidence.HasSpell(s, goal);
    }

    static async Task Receipt(QuestContext c, Transaction t, string goal)
    {
        if (!Reward(c, t, goal) || c.Job.FaerieReceipts.Any(r => r.Goal == goal && r.VerifiedAt >= t.Started))
            return;
        var s = c.Session.Snapshot;
        if (s.Generation == t.Generation && s.Revision <= t.Revision)
            return;
        await c.SaveAsync(c.Job with
        {
            FaerieReceipts = [.. c.Job.FaerieReceipts, new(goal, s.Generation, s.Revision, DateTimeOffset.UtcNow, s.Dialog)],
            Rewards = goal == ShinewoodFaerieEvidence.Pet ? new(c.Job.Rewards)
            {
                [goal] = 1
            } : c.Job.Rewards
        });
    }

    static bool Refused(Dialog d, string goal) => goal != ShinewoodFaerieEvidence.Pet && d.Choices.Length == 0 && (d.Text.Contains("not eligible", StringComparison.OrdinalIgnoreCase) || d.Text.Contains("must be", StringComparison.OrdinalIgnoreCase) || d.Text.Contains("only pure", StringComparison.OrdinalIgnoreCase) || d.Text.Contains("not ready", StringComparison.OrdinalIgnoreCase));
    static bool SavedPetWait(QuestJob job, Transaction t, Dialog? d) => job.FaerieCurrentGoal == ShinewoodFaerieEvidence.Pet && t.Kind == "faerie-goal" && t.TerminalSeen && d is not null && ShinewoodFaerieEvidence.PetWaitPage(d) && d.Generation == t.Generation && d.EntityId == t.EntityId && d.EntityType == t.EntityType && d.Revision == t.TerminalRevision && d.Revision > t.Revision;
    static async Task ResolvePetWait(QuestContext c, Dialog d, CancellationToken ct)
    {
        var t = c.Job.Pending!;
        await c.SaveAsync(c.Job with
        {
            FaeriePetWait = d,
            FaerieUnavailable = c.Job.FaerieUnavailable.Append(ShinewoodFaerieEvidence.Pet).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            CompletionDialog = d,
            Pending = t with
            {
                TerminalSeen = true,
                TerminalRevision = d.Revision
            },
            Reason = "No pet bonded; the Faerie asks you to wait one deoch"
        });
        var current = c.Session.Snapshot;
        if (current.Generation == d.Generation && current.Map == ShinewoodFaerieEvidence.Map && current.Dialog is { } open && open.Generation == d.Generation && open.EntityId == d.EntityId && open.EntityType == d.EntityType && open.Revision == d.Revision)
        {
            await CloseTerminal(c, open, ct);
            return;
        }

        // A saved, bound refusal remains evidence after a manual close or return.
        // Do not touch a different popup or repeat either adoption or teleport.
        await c.SaveAsync(c.Job with
        {
            Pending = null,
            FaerieCurrentGoal = null
        });
    }

    static async Task Reconcile(QuestContext c, CancellationToken ct)
    {
        var goal = c.Job.FaerieCurrentGoal ?? throw new QuestBlockedException("Saved Faerie goal missing.");
        for (int page = 0; page < 32; page++)
        {
            ct.ThrowIfCancellationRequested();
            var t = c.Job.Pending!;
            var s = c.Session.Snapshot;
            if (!t.Kind.StartsWith("faerie-", StringComparison.Ordinal) || t.Kind.StartsWith("faerie-return", StringComparison.Ordinal))
                throw new QuestBlockedException("Unexpected saved Faerie transaction.");
            await Receipt(c, t, goal);
            bool acquired = c.Job.FaerieReceipts.Any(r => r.Goal == goal && r.VerifiedAt >= t.Started) && Satisfied(c, goal);
            if (!acquired && SavedPetWait(c.Job, t, c.Job.CompletionDialog))
            {
                await ResolvePetWait(c, c.Job.CompletionDialog!, ct);
                return;
            }

            if (t.TerminalSeen && s.Generation == t.Generation && s.Map == ShinewoodFaerieEvidence.Map && s.Dialog is { } closed && closed.Revision == t.TerminalRevision && closed.EntityId == t.EntityId)
            {
                await c.Session.ExecuteAsync(new("close", Dialog: closed), ct);
                s = c.Session.Snapshot;
            }

            if (s.Dialog is null && acquired)
            {
                await c.SaveAsync(c.Job with
                {
                    Pending = null,
                    FaerieCurrentGoal = null
                });
                return;
            }

            if (!FreshPage(s, t))
                throw new QuestBlockedException("Faerie reply or reward is unconfirmed. Saved submission will not be repeated; reconcile fresh inventory/spells.");
            var d = s.Dialog!;
            if (t.Kind == "faerie-hello" && t.EntityType is null)
            {
                t = t with
                {
                    EntityId = d.EntityId,
                    EntityType = d.EntityType
                };
                await c.SaveAsync(c.Job with
                {
                    Pending = t
                });
            }

            if (!acquired && goal == ShinewoodFaerieEvidence.Pet && t.Kind == "faerie-goal" && ShinewoodFaerieEvidence.PetWaitPage(d))
            {
                await ResolvePetWait(c, d, ct);
                return;
            }

            if (goal != ShinewoodFaerieEvidence.Pet && t.Kind == "faerie-spell-menu" && ShinewoodFaerieEvidence.NoncasterPage(d))
            {
                await c.SaveAsync(c.Job with
                {
                    FaerieUnavailable = c.Job.FaerieUnavailable.Concat(c.Job.FaerieGoals.Where(g => g != ShinewoodFaerieEvidence.Pet)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                });
                await CloseTerminal(c, d, ct);
                return;
            }

            var goalRule = ShinewoodFaerieEvidence.SpellRules.FirstOrDefault(r => r.Name == goal);
            bool recognizedSpellMenu = t.Kind == "faerie-spell-menu" && (d.Choices.Length > 0 ? d.Choices.All(x => ShinewoodFaerieEvidence.SpellRules.Any(r => ShinewoodFaerieEvidence.SpellChoice(x.Text, r.Name))) : d.Text.Contains("spell", StringComparison.OrdinalIgnoreCase));
            if (!acquired && !d.HasNext && goalRule is not null && ShinewoodFaerieEvidence.Eligible(s, goalRule) is null && recognizedSpellMenu && !d.Choices.Any(x => ShinewoodFaerieEvidence.SpellChoice(x.Text, goal)))
            {
                await c.SaveAsync(c.Job with
                {
                    FaerieUnavailable = [.. c.Job.FaerieUnavailable, goal]
                });
                await CloseTerminal(c, d, ct);
                return;
            }

            if (Refused(d, goal))
            {
                await c.SaveAsync(c.Job with
                {
                    FaerieUnavailable = [.. c.Job.FaerieUnavailable, goal],
                    CompletionDialog = d,
                    Pending = t with
                    {
                        TerminalSeen = true,
                        TerminalRevision = d.Revision
                    }
                });
                await c.Session.ExecuteAsync(new("close", Dialog: d), ct);
                await c.SaveAsync(c.Job with
                {
                    Pending = null,
                    FaerieCurrentGoal = null,
                    FaerieClosedDialog = d.IsPursuit ? null : d
                });
                return;
            }

            if (d.Choices.Length > 0)
            {
                if (acquired)
                {
                    await CloseTerminal(c, d, ct);
                    return;
                }

                var choices = d.Choices.Where(x => goal == ShinewoodFaerieEvidence.Pet ? ShinewoodFaerieEvidence.PetChoice(x.Text) : ShinewoodFaerieEvidence.SpellChoice(x.Text, goal)).ToArray();
                bool branch = false;
                if (choices.Length == 0 && goal != ShinewoodFaerieEvidence.Pet)
                {
                    choices = d.Choices.Where(x => ShinewoodFaerieEvidence.SpellMenu(x.Text)).ToArray();
                    branch = true;
                }

                if (choices.Length != 1)
                {
                    var rule = ShinewoodFaerieEvidence.SpellRules.FirstOrDefault(r => r.Name == goal);
                    if (choices.Length == 0 && rule is not null && ShinewoodFaerieEvidence.Eligible(s, rule) is null && t.Kind == "faerie-spell-menu" && d.Choices.All(x => ShinewoodFaerieEvidence.SpellRules.Any(r => ShinewoodFaerieEvidence.SpellChoice(x.Text, r.Name))))
                    {
                        await c.SaveAsync(c.Job with
                        {
                            FaerieUnavailable = [.. c.Job.FaerieUnavailable, goal]
                        });
                        await CloseTerminal(c, d, ct);
                        return;
                    }

                    throw new QuestBlockedException("Expected Faerie pet/spell choice is missing or ambiguous. Popup preserved for review.");
                }

                // Never select another reward from a pending consequential submission.
                if (t.Kind == "faerie-goal")
                    throw new QuestBlockedException("Submitted Faerie reward is still missing; its choice will not be repeated.");
                await c.BeginAsync(branch ? "faerie-spell-menu" : "faerie-goal", d.EntityId, "Requesting " + goal, d);
                // The saved page identifies the request; only a newer page can be its reply.
                await c.SaveAsync(c.Job with
                {
                    Pending = c.Job.Pending! with
                    {
                        Revision = s.Revision
                    }
                });
                await c.Session.ExecuteAsync(new("choose", Text: choices[0].Text, Dialog: d), ct);
                await Reply(c, ct);
                continue;
            }

            bool related = d.Text.Contains(goal, StringComparison.OrdinalIgnoreCase) || d.Text.Contains("faerie", StringComparison.OrdinalIgnoreCase) || goal != ShinewoodFaerieEvidence.Pet && (d.Text.Contains("spell", StringComparison.OrdinalIgnoreCase) || d.Text.Contains("learn", StringComparison.OrdinalIgnoreCase));
            if (!related)
                throw new QuestBlockedException("Unfamiliar Faerie page preserved for review.");
            if (d.HasNext)
            {
                await c.SaveAsync(c.Job with
                {
                    CompletionDialog = d,
                    Pending = t with
                    {
                        Revision = s.Revision
                    }
                });
                await c.Session.ExecuteAsync(new("next", Dialog: d), ct);
                if (c.Session.Snapshot.Dialog is null)
                {
                    await Receipt(c, c.Job.Pending!, goal);
                    if (Satisfied(c, goal) && c.Job.FaerieReceipts.Any(r => r.Goal == goal && r.VerifiedAt >= t.Started))
                    {
                        await c.SaveAsync(c.Job with
                        {
                            Pending = null,
                            FaerieCurrentGoal = null
                        });
                        return;
                    }
                }

                await Reply(c, ct);
                continue;
            }

            if (acquired)
            {
                await CloseTerminal(c, d, ct);
                return;
            }

            await c.SaveAsync(c.Job with
            {
                CompletionDialog = d,
                Pending = t with
                {
                    TerminalSeen = true,
                    TerminalRevision = d.Revision
                }
            });
            throw new QuestBlockedException("Faerie reward not yet confirmed by inventory/spellbook. Reply preserved for reconciliation.");
        }

        throw new QuestBlockedException("Faerie conversation exceeded its page limit. Saved submission requires review.");
    }

    static async Task CloseTerminal(QuestContext c, Dialog d, CancellationToken ct)
    {
        await c.SaveAsync(c.Job with
        {
            CompletionDialog = d,
            Pending = c.Job.Pending! with
            {
                TerminalSeen = true,
                TerminalRevision = d.Revision
            }
        });
        await c.Session.ExecuteAsync(new("close", Dialog: d), ct);
        await c.SaveAsync(c.Job with
        {
            Pending = null,
            FaerieCurrentGoal = null,
            FaerieClosedDialog = d.IsPursuit ? null : d
        });
    }

    static async Task Return(QuestContext c, CancellationToken ct, int resourceChanges = 0)
    {
        var s = c.Session.Snapshot;
        if (c.Job.Pending is { } t)
        {
            if (!t.Kind.StartsWith("faerie-return", StringComparison.Ordinal))
                throw new QuestBlockedException("Faerie acquisition is awaiting reconciliation before return.");
            if (!ShinewoodFaerieEvidence.SafeArrival(s))
                throw new QuestBlockedException("Return arrival unconfirmed. Saved escape will not be repeated; Resume checks the actual landing.");
            await Finish(c, t.Kind["faerie-return:".Length..]);
            return;
        }

        if (ShinewoodFaerieEvidence.SafeArrival(s))
        {
            await Finish(c, "Already in town");
            return;
        }

        if (s.Dialog is not null && !KnownClosed(s, c.Job))
            throw new QuestBlockedException("A popup is open. Review it before leaving Shinewood.");
        Gate(c);
        ShinewoodFaerieEvidence.RequireEscape(s, c.Job);
        var escape = ShinewoodFaerieEvidence.Escape(s);
        if (escape is not null)
        {
            await c.BeginAsync("faerie-return:" + escape.Name, reason: "Returning with " + escape.Name);
            try
            {
                await c.Session.ExecuteAsync(escape.Kind == "use-item" ? new("use-item", Amount: escape.Slot, ExpectedResource: escape.Name) : new("spell", Slot: escape.Slot, ExpectedResource: escape.Name), ct);
            }
            catch (ResourceUnavailableException)
            {
                // This exception guarantees no packet was written. A new escape is safe.
                await c.SaveAsync(c.Job with
                {
                    Pending = null
                });
                if (resourceChanges >= 3)
                    throw new QuestBlockedException("Escape supplies keep changing. Prepare a return method and Resume.");
                await Return(c, ct, resourceChanges + 1);
                return;
            }

            await c.Session.WaitAsync(ShinewoodFaerieEvidence.SafeArrival, s.Revision, Timeout, ct);
            await Finish(c, escape.Name);
            return;
        }

        await c.Session.ExecuteAsync(new("check-faerie-return"), ct);
        for (int leg = 0; leg < ShinewoodFaerieEvidence.ReturnExits.Length; leg++)
        {
            s = c.Session.Snapshot;
            if (!ShinewoodFaerieEvidence.InForest(s.Map))
                break;
            var exit = ShinewoodFaerieEvidence.ReturnExits.SingleOrDefault(e => e.SourceMap == s.Map);
            if (exit is null)
            {
                if (s.Map == 542)
                    break;
                throw new QuestBlockedException("No documented walking return from this Shinewood map.");
            }

            await c.SaveAsync(c.Job with
            {
                Reason = "Walking out of Shinewood; town songs or dachaidh provide a faster escape"
            });
            await c.Session.ExecuteAsync(new("walk-exit", exit.Map, exit.From), ct);
            if (c.Session.Snapshot.Map != exit.Map || c.Session.Snapshot.Position is null)
                throw new QuestBlockedException("Walking return requires the actual expected map and landing.");
        }

        await c.Session.ExecuteAsync(new("navigate", 500, new(20, 20), PreservePopup: true), ct);
        if (!ShinewoodFaerieEvidence.SafeArrival(c.Session.Snapshot))
            throw new QuestBlockedException("Walking return has not reached town.");
        await Finish(c, "Walked back to Mileth");
    }

    static Task Finish(QuestContext c, string method)
    {
        var s = c.Session.Snapshot;
        if (!ShinewoodFaerieEvidence.SafeArrival(s))
            throw new QuestBlockedException("Verified town arrival required.");
        bool pet = Satisfied(c, ShinewoodFaerieEvidence.Pet);
        bool wait = c.Job.FaerieUnavailable.Contains(ShinewoodFaerieEvidence.Pet) && c.Job.FaeriePetWait is { } refusal && ShinewoodFaerieEvidence.PetWaitPage(refusal);
        if (!pet && !wait)
            throw new QuestBlockedException("Pet outcome remains unconfirmed; reconcile acquisition before completing this visit.");
        string learned = string.Join(", ", c.Job.FaerieGoals.Where(g => g != ShinewoodFaerieEvidence.Pet && Satisfied(c, g)));
        return c.SaveAsync(c.Job with
        {
            Pending = null,
            CompletedAt = DateTimeOffset.UtcNow,
            Repeat = false,
            NextRun = null,
            FaerieReturn = new(method, s.Generation, s.Revision, s.Map!.Value, s.Position!, DateTimeOffset.UtcNow),
            FaeriePetWait = pet ? null : c.Job.FaeriePetWait,
            CompletionText = (pet ? "Pet Faerie verified" : "No Pet Faerie this visit; wait one deoch before trying again") + (learned.Length > 0 ? "; learned/known: " + learned : "") + "; " + method,
            Reason = "Faerie visit and return verified"
        });
    }
}
