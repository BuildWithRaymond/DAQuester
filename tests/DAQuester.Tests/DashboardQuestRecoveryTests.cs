using System.Collections.Immutable;
using DAQuester.Core;
using Xunit;

namespace DAQuester.Tests;

public class DashboardQuestRecoveryTests
{
    [Fact]
    public void Emerald_dawn_blocks_unregistered_characters_and_offline_planning()
    {
        var state = FoundationTests.Ready(11, 1000) with
        {
            LegendsKnown = true,
            LegendsRevision = 1,
            Legends = ["((Unregistered))"],
            Experience = 1000
        };
        Assert.Equal(EligibilityKind.Blocked, EmeraldDawnEvidence.Eligibility(state).Kind);
        var memory = new CharacterMemory { Character = state.Name!, Level = 11, LegendsObservedAt = DateTimeOffset.UtcNow, Legends = ["((Unregistered))"] };
        Assert.Contains("registered", memory.SelectionRestriction("emerald-dawn")!.Reason);
    }

    [Theory]
    [InlineData(11, EligibilityKind.Blocked)]
    [InlineData(19, EligibilityKind.Blocked)]
    [InlineData(20, EligibilityKind.Eligible)]
    public void Faerie_requires_provisional_level_twenty(int level, EligibilityKind expected)
    {
        var state = FoundationTests.Ready(level, 1000) with
        {
            SpellsKnown = true,
            Weight = 0,
            MaxWeight = 100
        };
        Assert.Equal(expected, ShinewoodFaerieEvidence.Eligibility(state, new()).Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resumed_crypt_climb_returns_before_completion_and_keeps_checkpoint_when_return_fails(bool failReturn)
    {
        var state = new SimulatedSession();
        state.Set(FoundationTests.Ready(11, 1000) with
        {
            Name = "Kel",
            Map = 2,
            Position = new(5, 5)
        });
        var journal = new FileQuestJournal(Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString()));
        var job = new QuestJob { Character = "Kel", Quest = NewCharacterPath.ClimbId, Step = "run" };
        await journal.SaveAsync(job);
        bool returned = false;
        state.OnCommand = command =>
        {
            Assert.Null(journal.Load("Kel", NewCharacterPath.ClimbId).CompletedAt);
            if (command.Kind == "navigate")
            {
                if (failReturn)
                    throw new QuestBlockedException("Synthetic return obstruction");
                Assert.Equal(NewCharacterPath.Vestibule.Map, command.Map);
                state.Set(state.Snapshot with
                {
                    Map = command.Map,
                    Position = command.Target
                });
                returned = true;
            }
        };
        var context = new QuestContext(state, journal, new(), job);
        var action = new NewCharacterActions();
        if (failReturn)
        {
            var error = await Assert.ThrowsAsync<QuestBlockedException>(() => action.ExecuteAsync(context, new("run", "climb-run", "complete"), default));
            Assert.Equal("Synthetic return obstruction", error.Message);
            Assert.Null(journal.Load("Kel", NewCharacterPath.ClimbId).CompletedAt);
        }
        else
        {
            await action.ExecuteAsync(context, new("run", "climb-run", "complete"), default);
            Assert.True(returned);
            Assert.NotNull(journal.Load("Kel", NewCharacterPath.ClimbId).CompletedAt);
        }
    }
}
