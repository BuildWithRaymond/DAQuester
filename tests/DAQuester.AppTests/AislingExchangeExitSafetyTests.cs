using DAQuester.App.Services;
using DAQuester.Core;
using Xunit;

namespace DAQuester.AppTests;

public class AislingExchangeExitSafetyTests
{
    static AislingExchangeSupplyJourney Journey(bool submittedReceive, bool resolved)
    {
        var id = Guid.NewGuid();
        var operation = new QuestSupplySubmission(Guid.NewGuid(), id, "ExamplePlayer", "datester-7.41", "quote", [new("Mold", 1)]);
        return new(id, "ExamplePlayer", "datester-7.41", [])
        {
            Operations = [new(operation)
            {
                Resolved = resolved,
                ReceiptIntent = submittedReceive ? new(operation.ClientOperationId, operation.Character, operation.ServerProfile, 9, 10, [], []) : null
            }]
        };
    }

    [Fact]
    public void Uncertain_parcel_collection_keeps_scheduler_owned_window_open()
        => Assert.True(DesktopRuntime.SupplyGameActionPending(Journey(true, false)));

    [Fact]
    public void Settled_parcels_and_http_only_waits_do_not_require_a_live_game_window()
    {
        Assert.False(DesktopRuntime.SupplyGameActionPending(Journey(true, true)));
        Assert.False(DesktopRuntime.SupplyGameActionPending(Journey(false, false)));
        Assert.False(DesktopRuntime.SupplyGameActionPending(null));
    }

    [Fact]
    public void Uncertain_return_send_keeps_scheduler_owned_window_open()
    {
        var journey = Journey(false, true);
        var submission = new QuestSupplyReturnSubmission(Guid.NewGuid(), journey.Id, journey.Character, journey.ServerProfile,
            [], [new("Mold", 1, "journey_surplus")]);
        journey = journey with { Ended = true, ReturnOperations = [new(submission)
        {
            PendingSend = new(new(submission.ClientOperationId, "unit", journey.Character, journey.ServerProfile, 9, "Mold"),
                new() { ["Mold"] = 1 }, 1000)
        }] };
        Assert.True(DesktopRuntime.SupplyGameActionPending(journey));
    }

    [Theory]
    [InlineData("AwaitingRegistration")]
    [InlineData("AwaitingCollection")]
    [InlineData("AwaitingBankDeposit")]
    public void Http_only_return_wait_does_not_hold_game_window_open(string state)
    {
        var journey = Journey(false, true) with { Ended = true, ReturnStatus = state };
        Assert.False(DesktopRuntime.SupplyGameActionPending(journey));
    }
}
