using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace DAQuester.Tests;

// Dialogues come from independent saved runs, rather than the recipe under test.
// Travel and carrying supplies early are synthetic; movement has native fixtures.
public class DanaanCapturedJourneyTests(ITestOutputHelper output)
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    static string Root => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    static QuestDefinition Recipe() => JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/cap-band-of-danaan.v1.json")), Json)!;
    static FileQuestJournal Journal() => new(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N")));

    static IEnumerable<Dialog> Dialogs(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.EnumerateObject().Any(p => p.Name.Equals("entityId", StringComparison.OrdinalIgnoreCase)) &&
                value.EnumerateObject().Any(p => p.Name.Equals("isPursuit", StringComparison.OrdinalIgnoreCase)))
                yield return value.Deserialize<Dialog>(Json)!;
            foreach (var property in value.EnumerateObject())
                foreach (var dialog in Dialogs(property.Value)) yield return dialog;
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                foreach (var dialog in Dialogs(item)) yield return dialog;
    }

    static Dialog[] CapturedPages(int first, int last, string npc)
    {
        using var capture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-lancelot-variation.json")));
        return capture.RootElement.GetProperty("Observations").EnumerateArray()
            .Where(o => o.GetProperty("Sequence").GetInt32() >= first && o.GetProperty("Sequence").GetInt32() <= last)
            .SelectMany(o => Dialogs(o.GetProperty("Result"))).Where(d => d.Name == npc)
            .DistinctBy(d => d.Revision).OrderBy(d => d.Revision).ToArray();
    }

    [Fact]
    public void Bounded_transcripts_from_every_saved_Danaan_run_preserve_executed_page_successors()
    {
        var recipe = Recipe();
        var fisk = JsonSerializer.Deserialize<QuestDefinition>(File.ReadAllText(Path.Combine(Root, "content/recipes/fisk-secret.v1.json")), Json)!;
        var tasks = recipe.Journey.Concat(recipe.JourneyVariants.Values.SelectMany(v => v.Journey)).Concat(fisk.Journey).ToArray();
        var gaps = new List<string>();
        int checkedEdges = 0;
        bool TextMatches(Dialog d, ObservedQuestPage p) => d.Name == p.Speaker &&
            (d.Text == p.Text || p.TextAlternatives.Contains(d.Text));
        foreach (string file in Directory.EnumerateFiles(Path.Combine(Root, "content/evidence/datester-7.41"), "*.json"))
        {
            if (!Path.GetFileName(file).StartsWith("danaan-", StringComparison.Ordinal) && !Path.GetFileName(file).StartsWith("fisk-", StringComparison.Ordinal)) continue;
            using var capture = JsonDocument.Parse(File.ReadAllText(file));
            if (!capture.RootElement.TryGetProperty("Observations", out var observations)) continue;
            int number = 0;
            foreach (var observation in observations.EnumerateArray())
            {
                number++;
                if (!observation.TryGetProperty("Result", out var result) || !result.TryGetProperty("discovery", out var discovery) ||
                    !discovery.TryGetProperty("pages", out var recorded)) continue;
                var pages = recorded.EnumerateArray().Select(p => p.Deserialize<Dialog>(Json)!).ToArray();
                string? selected = observation.TryGetProperty("Arguments", out var arguments) && arguments.TryGetProperty("text", out var text) ? text.GetString() : null;
                for (int i = 0; i + 1 < pages.Length; i++)
                {
                    var before = pages[i];
                    var after = pages[i + 1];
                    var candidates = tasks.SelectMany(t => t.Pages.Select((p, index) => (Task: t, Page: p, Index: index)))
                        .Where(p => p.Index + 1 < p.Task.Pages.Length && TextMatches(before, p.Page) &&
                            (p.Page.Kind == "next" && before.HasNext && before.Choices.Length == 0 ||
                             i == 0 && p.Page.Kind == "choose" && selected is not null &&
                             (selected == p.Page.Choice || p.Page.ChoiceAlternatives.Contains(selected)))).ToArray();
                    if (candidates.Length == 0) continue; // Unselected NPC topics and stock menus are separate from quest stages.
                    checkedEdges++;
                    if (!candidates.Any(p => TextMatches(after, p.Task.Pages[p.Index + 1])))
                        gaps.Add(Path.GetFileName(file) + " observation " + number + ": " + before.Name + " — " + before.Text + " → " + after.Text);
                }
            }
        }
        Assert.True(checkedEdges >= 100, "Too few captured page transitions audited: " + checkedEdges);
        Assert.True(gaps.Count == 0, string.Join(Environment.NewLine, gaps.Distinct()));
        output.WriteLine("Audited {0} executed Danaan/Fisk transcript transitions; no missing successors.", checkedEdges);
    }

    [Fact]
    public void Retained_Danaan_packets_preserve_informational_Next_successors()
    {
        var recipe = Recipe();
        var tasks = recipe.Journey.Concat(recipe.JourneyVariants.Values.SelectMany(v => v.Journey)).ToArray();
        int checkedEdges = 0, files = 0;
        var gaps = new List<string>();
        bool TextMatches(Dialog d, ObservedQuestPage p) => d.Name == p.Speaker && (d.Text == p.Text || p.TextAlternatives.Contains(d.Text));
        foreach (string file in Directory.EnumerateFiles(Path.Combine(Root, "content/evidence/datester-7.41"), "danaan-*.json"))
        {
            using var capture = JsonDocument.Parse(File.ReadAllText(file));
            if (!capture.RootElement.TryGetProperty("Events", out var events)) continue;
            files++;
            var tracker = new StateTracker(1);
            tracker.Identity("Synthetic", 42);
            Dialog? previous = null;
            foreach (var packet in events.EnumerateArray())
            {
                if (!packet.TryGetProperty("Opcode", out var opcode) || opcode.GetByte() is not (47 or 48) ||
                    packet.TryGetProperty("Direction", out var direction) && direction.GetString() != "S") continue;
                tracker.Observe(opcode.GetByte(), Convert.FromBase64String(packet.GetProperty("Payload").GetString()!));
                var next = tracker.Snapshot.Dialog;
                if (previous is { IsPursuit: true, HasNext: true, Choices.Length: 0, Prompt: null } &&
                    next is { IsPursuit: true } && next.EntityId == previous.EntityId && next.Name == previous.Name)
                {
                    var candidates = tasks.SelectMany(t => t.Pages.Select((p, index) => (Task: t, Page: p, Index: index)))
                        .Where(p => p.Page.Kind == "next" && p.Index + 1 < p.Task.Pages.Length && TextMatches(previous, p.Page)).ToArray();
                    if (candidates.Length > 0)
                    {
                        checkedEdges++;
                        if (!candidates.Any(p => TextMatches(next, p.Task.Pages[p.Index + 1])))
                            gaps.Add(Path.GetFileName(file) + ": " + previous.Name + " — " + previous.Text + " → " + next.Text);
                    }
                }
                previous = next;
            }
        }
        Assert.True(files >= 4 && checkedEdges >= 30, "Insufficient raw packet coverage: " + files + " files, " + checkedEdges + " transitions.");
        Assert.True(gaps.Count == 0, string.Join(Environment.NewLine, gaps.Distinct()));
        output.WriteLine("Audited {0} informational Next transitions across {1} raw Danaan captures; no missing successors.", checkedEdges, files);
    }

    [Fact]
    public void Every_retained_Danaan_mundane_pursuit_is_a_recipe_page_or_explicitly_accounted_context()
    {
        var recipe = Recipe();
        var tasks = recipe.Journey.Concat(recipe.JourneyVariants.Values.SelectMany(v => v.Journey)).ToArray();
        var actors = tasks.Select(t => t.Npc).ToHashSet();
        var known = tasks.SelectMany(t => t.Pages).SelectMany(p => new[] { p.Text }.Concat(p.TextAlternatives).Select(text => (p.Speaker, Text: text))).ToHashSet();
        // These exact recorded pages are accounted for, rather than silently
        // skipped by transition coverage. They select no new automatic branch.
        var context = new HashSet<(string, string)>
        {
            ("Kyros", "Welcome to the monastery."), // Greeting precedes the recorded Need Help? reply.
            ("Blanchefleur", "I long for the day I might leave these castle walls and run free. If only my dream were more than an shimmering illusion."),
            ("Blanchefleur", "Still, I delight in the outside fragrance and sight whenever I can."), // Unselected visit topic, closed in N9.
            ("Beggar", "Hurry up! I cannot tell you anything with an empty stomach! Hah hah!"), // N49 explicitly declines food; no handoff.
            ("Donnan", DanaanDonnanRecovery.NotFinished) // Current assignment refusal; never a successful stage receipt.
        };
        var seen = new HashSet<(string, string)>();
        var gaps = new List<string>();
        void Check(Dialog d, string file)
        {
            if (!d.IsPursuit || d.EntityType != 1 || !actors.Contains(d.Name)) return;
            var key = (d.Name, d.Text);
            seen.Add(key);
            if (!known.Contains(key) && !context.Contains(key)) gaps.Add(Path.GetFileName(file) + ": " + d.Name + " — " + d.Text);
        }
        foreach (string file in Directory.EnumerateFiles(Path.Combine(Root, "content/evidence/datester-7.41"), "danaan-*.json"))
        {
            using var capture = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var dialog in Dialogs(capture.RootElement)) Check(dialog, file);
            if (!capture.RootElement.TryGetProperty("Events", out var events)) continue;
            var tracker = new StateTracker(1);
            tracker.Identity("Synthetic", 42);
            foreach (var packet in events.EnumerateArray())
            {
                if (!packet.TryGetProperty("Opcode", out var opcode) || opcode.GetByte() is not (47 or 48) ||
                    packet.TryGetProperty("Direction", out var direction) && direction.GetString() != "S") continue;
                tracker.Observe(opcode.GetByte(), Convert.FromBase64String(packet.GetProperty("Payload").GetString()!));
                if (tracker.Snapshot.Dialog is { } d) Check(d, file);
            }
        }
        Assert.True(seen.Count >= 100, "Too few distinct captured pursuit pages: " + seen.Count);
        Assert.True(gaps.Count == 0, string.Join(Environment.NewLine, gaps.Distinct()));
        output.WriteLine("Accounted for {0} distinct Danaan mundane pursuit pages, including four context pages and Donnan's unfinished-assignment reply.", seen.Count);
    }

    static SimulatedSession Ready(int map, Tile position)
    {
        var s = new SimulatedSession();
        ((HashSet<string>)s.Capabilities).UnionWith(Recipe().Capabilities);
        s.Set(s.Snapshot with { Map = map, Position = position, Level = 99, Gold = 194851, Experience = 73908048,
            LegendsKnown = true, LegendsRevision = 1, MaxWeight = 287,
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(1, new(1, "Cherry", 2)).Add(2, new(2, "Grapes", 1)).Add(3, new(3, "Leather Greaves", 1)) });
        return s;
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("loures-orders", null)]
    [InlineData("loures-court", null)]
    [InlineData("blanchefleur-fiosachd", null)]
    [InlineData(null, "generation")]
    [InlineData(null, "revision")]
    [InlineData(null, "identity")]
    [InlineData(null, "text")]
    [InlineData(null, "next")]
    public async Task Paused_Donnan_Next_reconciles_only_bound_captured_referral_without_resubmitting(string? variant, string? gap)
    {
        var full = Recipe();
        var tasks = variant is null ? full.Journey : full.JourneyVariants[variant].Journey;
        int index = Array.FindIndex(tasks, t => t.Id == "donnan-next-task");
        var pages = CapturedPages(139, 140, "Donnan");
        Assert.Equal(3, pages.Length);
        var s = Ready(348, new(10, 4));
        Dialog Normalize(Dialog d, long revision) => d with { Generation = s.Snapshot.Generation, Revision = revision };
        var submitted = Normalize(pages[1], s.Snapshot.Revision);
        var reply = Normalize(pages[2], s.Snapshot.Revision + 1);
        reply = gap switch
        {
            "generation" => reply with { Generation = reply.Generation + 1 },
            "revision" => reply with { Revision = submitted.Revision },
            "identity" => reply with { EntityId = reply.EntityId + 1 },
            "text" => reply with { Text = "Synthetic unrecorded referral" },
            "next" => reply with { HasNext = false },
            _ => reply
        };
        s.Set(s.Snapshot with { Dialog = reply });
        var baseline = new Transaction("observed-stage", submitted.Generation, submitted.Revision - 1, s.Snapshot.Gold!.Value, 0, s.Snapshot.Experience!.Value, DateTimeOffset.UtcNow)
        { InventoryBaselineKnown = true };
        var j = Journal();
        await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, Step = "run", JourneyVariant = variant,
            DanaanOptions = new("cail", true), Pending = baseline with { Kind = "observed-page:donnan-next-task", EntityId = submitted.EntityId },
            Route = new() { Task = index, Page = 1, Baseline = baseline, BoundGeneration = submitted.Generation,
                BoundEntity = submitted.EntityId, SubmittedPage = submitted, LastReply = reply } });
        s.OnCommand = c =>
        {
            if (c.Kind == "next") s.Set(s.Snapshot with { Dialog = null });
            else throw new QuestBlockedException("Synthetic stop after reconciled Donnan referral.");
        };
        var runner = new QuestRunner(s, j, full, new());
        await runner.RunAsync();
        if (gap is not null)
        {
            Assert.Equal("Paused", runner.Job.Status);
            Assert.Equal(index, runner.Job.Route?.Task);
            Assert.NotNull(runner.Job.Pending);
            Assert.Empty(s.Commands);
            return;
        }
        Assert.True(runner.Job.Route?.Task > index, runner.Job.Reason);
        Assert.Null(runner.Job.Pending);
        Assert.Single(s.Commands, c => c.Kind == "next");
        Assert.Equal(reply.Text, s.Commands.Single(c => c.Kind == "next").ExpectedPageText);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "choose");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text")]
    [InlineData("generation")]
    [InlineData("identity")]
    [InlineData("revision")]
    [InlineData("pending")]
    [InlineData("pending-boundary")]
    [InlineData("pending-stale")]
    [InlineData("handoff")]
    [InlineData("next")]
    public async Task Reconnect_continues_recorded_assignment_without_querying_Donnan_again(string? gap)
    {
        var full = Recipe();
        var task = full.Journey.Single(t => t.Id == "donnan-next-task") with { ApproachStrategy = null };
        if (gap == "handoff") task = task with { Inputs = [new("Grapes", 1)] };
        var recipe = FiskDanaanAutomationTests.Recipe(DanaanEvidence.Id) with { Journey = [task] };
        var pages = CapturedPages(139, 140, "Donnan");
        var s = Ready(348, new(4, 5));
        var submitted = pages[1] with { Generation = s.Snapshot.Generation, Revision = s.Snapshot.Revision + 1 };
        s.Set(s.Snapshot with { Dialog = submitted });
        var received = pages[2] with { Generation = submitted.Generation, Revision = submitted.Revision + 1 };
        received = gap switch
        {
            "text" => received with { Text = "Synthetic unrecorded reply" },
            "generation" => received with { Generation = 3 },
            "identity" => received with { EntityId = received.EntityId + 1 },
            "revision" => received with { Revision = submitted.Revision },
            "next" => received with { HasNext = false },
            _ => received
        };
        var baseline = new Transaction("observed-stage", submitted.Generation, submitted.Revision - 1, s.Snapshot.Gold!.Value, 0, s.Snapshot.Experience!.Value, DateTimeOffset.UtcNow)
        { InventoryBaselineKnown = true };
        var j = Journal();
        var context = new QuestContext(s, j, new(), new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, Step = "run", DanaanOptions = new("cail", true),
            Route = new() { Task = 0, Page = 1, Baseline = baseline, BoundGeneration = 1, BoundEntity = submitted.EntityId,
                SubmittedPage = submitted, LastReply = submitted } });
        await context.BeginAsync(gap == "pending" ? "observed-page:meaveen-fruit" : "observed-page:donnan-next-task", submitted.EntityId, observedDialog: submitted);
        Assert.Equal(submitted.Revision - 1, context.Job.Pending!.Revision);
        var pending = context.Job.Pending;
        if (gap == "pending-boundary") pending = pending with { Revision = submitted.Revision };
        if (gap == "pending-stale") pending = pending with { Revision = submitted.Revision - 2 };
        await context.SaveAsync(context.Job with { Pending = pending, Route = context.Job.Route! with { LastReply = received } });
        s.Set(s.Snapshot with { Generation = 2, Dialog = null });
        int page = 0;
        Dialog Fresh() => pages[page] with { Generation = 2, Revision = s.Snapshot.Revision + 1 };
        s.OnCommand = c =>
        {
            if (c.Kind == "interact") { s.Set(s.Snapshot with { Dialog = Fresh() }); return; }
            Assert.Equal(2, c.ExpectedGeneration);
            Assert.Equal(Fresh().Text, c.ExpectedPageText);
            if (++page == pages.Length) s.Set(s.Snapshot with { Dialog = null });
            else s.Set(s.Snapshot with { Dialog = Fresh() with { Text = "You have not finished your task", HasNext = false, Choices = [] } });
        };
        var runner = new QuestRunner(s, j, recipe, new());
        await runner.RunAsync();
        bool receipt = gap is null;
        Assert.Equal(receipt ? 1 : 0, runner.Job.Route?.Task);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "interact");
        Assert.DoesNotContain(s.Commands, c => c.Kind == "choose");
        Assert.DoesNotContain(s.Commands, c => c.ExpectedGeneration == 1);
        if (receipt) Assert.Null(runner.Job.Pending);
        else Assert.NotNull(runner.Job.Pending);
    }

    [Fact]
    public async Task Actual_saved_Donnan_referral_continues_to_Kyros_without_reopening_progress()
    {
        using var capture = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content/evidence/datester-7.41/danaan-donnan-reconnect-checkpoint.json")));
        var job = capture.RootElement.GetProperty("Checkpoint").Deserialize<QuestJob>(Json)!;
        var recipe = Recipe();
        Assert.Equal("donnan-next-task", recipe.Journey[job.Route!.Task].Id);
        var current = capture.RootElement.GetProperty("Current");
        var s = Ready(current.GetProperty("Map").GetInt32(), current.GetProperty("Position").Deserialize<Tile>(Json)!);
        uint freshActor = job.Route.BoundEntity!.Value + 1;
        s.Set(s.Snapshot with { Name = job.Character, Generation = current.GetProperty("Generation").GetInt64(),
            Gold = job.Pending!.Gold, Experience = job.Pending.Experience,
            Entities = s.Snapshot.Entities.SetItem(freshActor, new(freshActor, "Donnan", new(2, 5), true) { Kind = 2 }) });
        var pages = CapturedPages(139, 140, "Donnan");
        int page = 0;
        Dialog Fresh() => pages[page] with { Generation = s.Snapshot.Generation, EntityId = freshActor, Revision = s.Snapshot.Revision + 1 };
        s.OnCommand = c =>
        {
            if (page == pages.Length) throw new QuestBlockedException("Synthetic stop after actual checkpoint recovery.");
            if (c.Kind == "navigate") return; // Recorded current position is already within screen range.
            if (c.Kind == "interact") { s.Set(s.Snapshot with { Dialog = Fresh() }); return; }
            Assert.Equal(s.Snapshot.Generation, c.ExpectedGeneration);
            Assert.Equal(freshActor, c.Dialog!.EntityId);
            Assert.Equal(Fresh().Text, c.ExpectedPageText);
            if (++page == pages.Length) s.Set(s.Snapshot with { Dialog = null });
            else s.Set(s.Snapshot with { Dialog = Fresh() with { Text = "You have not finished your task", HasNext = false, Choices = [] } });
        };
        var j = Journal(); await j.SaveAsync(job);
        var runner = new QuestRunner(s, j, recipe, new());
        await runner.RunAsync();
        Assert.Equal(27, runner.Job.Route?.Task);
        Assert.Null(runner.Job.Pending);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "interact");
        Assert.DoesNotContain(s.Commands, c => c.Kind == "choose");
        Assert.DoesNotContain(s.Commands, c => c.ExpectedGeneration == job.Pending.Generation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("assignment")]
    [InlineData("referral")]
    [InlineData("closed")]
    public async Task Captured_Cail_journey_from_acceptance_to_reward_and_close_consumes_early_supplies_once(string? disconnect)
    {
        var full = Recipe();
        string[] ids = ["donnan-accept", "meaveen-request", "meaveen-fruit", "donnan-next-task", "kyros-request", "kyros-greaves", "donnan-done", "erin-reward"];
        var tasks = ids.Select(id => full.Journey.Single(t => t.Id == id) with { ApproachStrategy = null }).ToArray();
        var captured = new[] { CapturedPages(113, 115, "Donnan"), CapturedPages(125, 126, "Meaveen"),
            CapturedPages(132, 134, "Meaveen"), CapturedPages(139, 140, "Donnan"), CapturedPages(150, 151, "Kyros"),
            CapturedPages(161, 163, "Kyros"), CapturedPages(169, 170, "Donnan"), CapturedPages(175, 176, "Erin") };
        for (int i = 0; i < tasks.Length; i++) Assert.Equal(tasks[i].Pages.Length, captured[i].Length);
        var s = Ready(348, new(4, 5));
        int task = 0, page = 0;
        bool interrupted = false;
        Dialog Current() => captured[task][page] with { Generation = s.Snapshot.Generation, Revision = s.Snapshot.Revision + 1, ObservedAt = DateTimeOffset.UtcNow };
        string mark = "Worthy of the Cap of Danaan, Deoch 223, Summer";
        s.OnCommand = c =>
        {
            if (c.Kind == "navigate") { s.Set(s.Snapshot with { Map = c.Map, Position = c.Target ?? new(6, 8) }); return; }
            if (c.Kind == "enter") { s.Set(s.Snapshot with { Map = c.Map, Position = c.InwardTarget }); return; }
            if (c.Kind == "step") { s.Set(s.Snapshot with { Position = c.Target }); return; }
            if (c.Kind is "interact" or "say") { s.Set(s.Snapshot with { Map = tasks[task].Map, Dialog = Current() }); return; }
            if (c.Kind == "inspect-self") { s.Set(s.Snapshot with { Legends = [mark], LegendsRevision = s.Snapshot.Revision + 1 }); return; }
            Assert.Contains(c.Kind, new[] { "next", "choose", "close" });
            Assert.Equal(captured[task][page].Text, c.ExpectedPageText);
            if (!interrupted && disconnect == "referral" && tasks[task].Id == "donnan-next-task" && page == 2)
            {
                interrupted = true;
                task++; page = 0; // Server has already assigned Kyros before presentation closes.
                throw new QuestBlockedException("Synthetic disconnect before referral presentation acknowledgement.");
            }
            if (c.Text == "I may have what you need.") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(1).Remove(2) });
            if (c.Text == "I have some for you.") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.Remove(3) });
            if (c.Text == "I completed the tasks") s.Set(s.Snapshot with { Experience = 74908048,
                ActiveStatus = new(s.Snapshot.Generation, s.Snapshot.Revision + 1, 3, "1000000 experience!", DateTimeOffset.UtcNow) });
            if (tasks[task].Id == "erin-reward" && c.Kind == "next") s.Set(s.Snapshot with { Inventory = s.Snapshot.Inventory.SetItem(4, new(4, "Cap of Danaan", 1)) });
            bool pause = !interrupted && tasks[task].Id == "donnan-next-task" &&
                (disconnect == "assignment" && page == 1 || disconnect == "closed" && page == 2);
            page++;
            if (page == captured[task].Length) { task++; page = 0; s.Set(s.Snapshot with { Dialog = null }); }
            else s.Set(s.Snapshot with { Dialog = Current() });
            if (pause)
            {
                interrupted = true;
                if (disconnect == "assignment") { task++; page = 0; }
                throw new QuestBlockedException("Synthetic disconnect after server assigned Kyros.");
            }
        };
        var j = Journal();
        await j.SaveAsync(new() { Character = s.Snapshot.Name!, Quest = DanaanEvidence.Id, DanaanOptions = new("cail", true) });
        var runner = new QuestRunner(s, j, full with { Journey = tasks, JourneyVariants = [] }, new());
        await runner.RunAsync();
        if (disconnect is not null)
        {
            Assert.True(interrupted, runner.Job.Reason);
            Assert.NotNull(runner.Job.DonnanAssignment);
            Assert.Equal(3, runner.Job.Route?.Task);
            Assert.Equal(1, s.Snapshot.Count("Leather Greaves"));
            Assert.Equal(0, s.Snapshot.Count("Cherry") + s.Snapshot.Count("Grapes"));
            s.Set(s.Snapshot with { Generation = 2, Dialog = null });
            runner = new QuestRunner(s, j, full with { Journey = tasks, JourneyVariants = [] }, new());
            await runner.RunAsync();
        }
        Assert.True(runner.Job.Status == "Completed", runner.Job.Reason);
        Assert.Equal(tasks.Length, task);
        Assert.Equal(1, s.Snapshot.Count("Cap of Danaan"));
        Assert.Equal(0, s.Snapshot.Count("Cherry") + s.Snapshot.Count("Grapes") + s.Snapshot.Count("Leather Greaves"));
        Assert.Single(s.Commands, c => c.Text == "I may have what you need.");
        Assert.Single(s.Commands, c => c.Text == "I have some for you.");
        Assert.Single(s.Commands, c => c.Text == "I completed the tasks");
        Assert.Equal(3, s.Commands.Count(c => c.Kind == "interact" && c.Text == "Donnan"));
        Assert.Null(s.Snapshot.Dialog);
        Assert.Null(runner.Job.Pending);
        Assert.Equal(mark, runner.Job.CompletionLegend);
    }
}
