using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

// Exact dialogue comes from the owned successful bond capture. Movement,
// resource delivery, connection changes and timeouts below are synthetic.
public class ShinewoodFaerieBondTests
{
    const string Id = ShinewoodFaerieEvidence.Id, Pet = ShinewoodFaerieEvidence.Pet;
    static QuestDefinition Recipe() => new(Id, 1, "Shinewood Faerie", "captured bond regression",
        ["navigate", "dialog", "inventory", "speech", "spell", "item"],
        [new("preflight", "faerie-preflight", "visit"), new("visit", "faerie-visit", "return"), new("return", "faerie-return", "complete")], 0)
    { Repeatable = false };

    static Dictionary<ushort, Dialog> Pages()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../content/evidence/datester-7.41/shinewood-faerie-pet-bond.json"));
        using var fixture = JsonDocument.Parse(File.ReadAllText(path));
        return fixture.RootElement.GetProperty("Pages").EnumerateArray()
            .Select(p => p.Deserialize<Dialog>()!).ToDictionary(d => d.Step);
    }

    static (SimulatedSession Session, FileQuestJournal Journal) Flow(bool leaveConsent = false,
        bool deliverPet = true, string? changedOffer = null, bool skipOffer = false, bool loseReply = false,
        bool loseClose = false)
    {
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        var pages = Pages();
        var s = new SimulatedSession();
        foreach (var cap in Recipe().Capabilities)
            ((HashSet<string>)s.Capabilities).Add(cap);
        s.Set(new()
        {
            Name = "Synthetic", Id = 42, Connected = true, Generation = 1,
            Level = 99, Hp = 4200, MaxHp = 4200, Mana = 4139,
            Gold = 99, Weight = 1, MaxWeight = 100, Class = "Wizard", IsMaster = false, IsPure = false,
            InventoryKnown = true, SpellsKnown = true, Map = 500, Position = new(20, 20),
            Inventory = ImmutableDictionary<byte, Item>.Empty.Add(9, new(9, "Rucesion Song", 1))
        });
        void Page(ushort step)
        {
            var d = pages[step] with { Generation = s.Snapshot.Generation, Revision = s.Snapshot.Revision + 1 };
            if (step == 72 && changedOffer is not null)
                d = d with { Text = changedOffer };
            s.Set(s.Snapshot with { Dialog = d });
        }
        s.OnCommand = c =>
        {
            switch (c.Kind)
            {
                case "visit":
                    s.Set(s.Snapshot with { Map = 992, Position = ShinewoodFaerieEvidence.Approach, Entities = [] });
                    break;
                case "say":
                    Page(0);
                    break;
                case "next":
                    Assert.Equal(s.Snapshot.Dialog, c.Dialog);
                    Page(c.Dialog!.Step switch
                    {
                        0 => 1, 28 => 32, 32 => 51, 51 => skipOffer ? (ushort)73 : (ushort)72,
                        72 => 73, _ => throw new InvalidOperationException("Unexpected captured bond page")
                    });
                    break;
                case "choose":
                    Assert.Equal(s.Snapshot.Dialog, c.Dialog);
                    if (c.Text == "I want a Faerie as a pet.")
                        Page(28);
                    else
                    {
                        Assert.Equal("Yes", c.Text);
                        var pending = journal.Load("Synthetic", Id).Pending;
                        Assert.NotNull(pending);
                        Assert.Equal("faerie-bond", pending.Kind);
                        Assert.Equal(s.Snapshot.Revision, pending.Revision);
                        if (loseReply)
                            throw new TimeoutException("Synthetic uncertain bond reply");
                        s.Set(s.Snapshot with
                        {
                            MaxHp = deliverPet ? 3950u : 4200u,
                            Inventory = deliverPet ? s.Snapshot.Inventory.Add(13, new(13, Pet, 1)) : s.Snapshot.Inventory,
                            Dialog = leaveConsent ? s.Snapshot.Dialog : null
                        });
                    }
                    break;
                case "close":
                    Assert.Equal(s.Snapshot.Dialog, c.Dialog);
                    if (loseClose)
                        throw new TimeoutException("Synthetic uncertain close");
                    s.Set(s.Snapshot with { Dialog = null });
                    break;
                case "use-item":
                    Assert.Null(s.Snapshot.Dialog);
                    Assert.Single(journal.Load("Synthetic", Id).FaerieReceipts);
                    s.Set(s.Snapshot with { Map = 3082, Position = new(3, 1) });
                    break;
            }
        };
        return (s, journal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Captured_bond_verifies_pet_closes_dialogue_and_moves_on(bool leaveConsent)
    {
        var (s, j) = Flow(leaveConsent: leaveConsent);
        var runner = new QuestRunner(s, j, Recipe(), new());
        await runner.RunAsync();
        Assert.True(runner.Job.Status == "Completed", runner.Job.Reason);
        Assert.Null(runner.Job.Pending);
        Assert.Single(runner.Job.FaerieReceipts, r => r.Goal == Pet);
        Assert.Equal(1, runner.Job.Rewards[Pet]);
        Assert.Null(runner.Job.FaeriePetWait);
        Assert.Single(s.Commands, c => c.Text == "Yes");
        Assert.Single(s.Commands, c => c.Text == "I want a Faerie as a pet.");
        Assert.Equal(leaveConsent ? 1 : 0, s.Commands.Count(c => c.Kind == "close"));
        Assert.Single(s.Commands, c => c.Kind == "use-item");
        Assert.NotNull(runner.Job.FaerieReturn);
        Assert.Contains("Pet Faerie verified", runner.Job.CompletionText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_pet_receipt_preserves_bond_and_reconciles_late_delivery_without_another_yes(bool loseReply)
    {
        var (s, j) = Flow(deliverPet: false, loseReply: loseReply);
        var runner = new QuestRunner(s, j, Recipe(), new());
        await runner.RunAsync();
        Assert.Equal("Paused", runner.Job.Status);
        Assert.Equal("faerie-bond", runner.Job.Pending?.Kind);
        Assert.Empty(runner.Job.FaerieReceipts);
        Assert.Null(runner.Job.CompletedAt);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "use-item");
        await runner.RunAsync();
        Assert.Single(s.Commands, c => c.Text == "Yes");
        s.Set(s.Snapshot with { Dialog = null, Inventory = s.Snapshot.Inventory.Add(13, new(13, Pet, 1)) });
        var resumed = new QuestRunner(s, j, Recipe(), new());
        await resumed.RunAsync();
        Assert.True(resumed.Job.Status == "Completed", resumed.Job.Reason);
        Assert.Single(s.Commands, c => c.Text == "Yes");
        Assert.Single(s.Commands, c => c.Kind == "use-item");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("You have a connection with a faerie. To make it bond with you, you must give up 500hp and 250mp.", false)]
    public async Task Generic_yes_requires_the_exact_preceding_bond_terms(string? changedOffer, bool skipOffer)
    {
        var (s, j) = Flow(changedOffer: changedOffer, skipOffer: skipOffer);
        var runner = new QuestRunner(s, j, Recipe(), new());
        await runner.RunAsync();
        Assert.Equal("Paused", runner.Job.Status);
        Assert.NotNull(runner.Job.Pending);
        Assert.DoesNotContain(s.Commands, c => c.Text == "Yes" || c.Kind == "use-item");
        Assert.Empty(runner.Job.FaerieReceipts);
    }

    [Fact]
    public async Task Uncertain_bond_popup_close_resumes_cleanup_without_repeating_adoption_or_bond()
    {
        var (s, j) = Flow(leaveConsent: true, loseClose: true);
        var runner = new QuestRunner(s, j, Recipe(), new());
        await runner.RunAsync();
        Assert.Equal("Paused", runner.Job.Status);
        Assert.True(runner.Job.Pending?.TerminalSeen);
        Assert.Single(runner.Job.FaerieReceipts);
        Assert.DoesNotContain(s.Commands, c => c.Kind == "use-item");
        s.Set(s.Snapshot with { Dialog = null });
        var resumed = new QuestRunner(s, j, Recipe(), new());
        await resumed.RunAsync();
        Assert.True(resumed.Job.Status == "Completed", resumed.Job.Reason);
        Assert.Single(s.Commands, c => c.Text == "Yes");
        Assert.Single(s.Commands, c => c.Kind == "close");
        Assert.Single(s.Commands, c => c.Kind == "use-item");
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("entity")]
    [InlineData("pursuit")]
    [InlineData("stale")]
    [InlineData("changed-terms")]
    [InlineData("prompt")]
    [InlineData("duplicate-yes")]
    public void Bond_consent_rejects_foreign_stale_or_changed_offers(string mismatch)
    {
        var pages = Pages();
        var offer = pages[72];
        var question = pages[73];
        if (mismatch == "changed-terms") offer = offer with { Text = offer.Text.Replace("250hp", "500hp") };
        question = mismatch switch
        {
            "generation" => question with { Generation = offer.Generation + 1 },
            "entity" => question with { EntityId = offer.EntityId + 1 },
            "pursuit" => question with { Pursuit = (ushort)(offer.Pursuit + 1) },
            "stale" => question with { Revision = offer.Revision },
            "prompt" => question with { Prompt = "Unexpected input" },
            "duplicate-yes" => question with { Choices = [question.Choices[0], question.Choices[0]] },
            _ => question
        };
        Assert.False(ShinewoodFaerieEvidence.PetBondConsent(offer, question));
    }
}
