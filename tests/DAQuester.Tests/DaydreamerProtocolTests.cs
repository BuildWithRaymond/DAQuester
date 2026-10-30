using System.Reflection;
using System.Text.Json;
using Arbiter.Net.Serialization;
using Arbiter.Net.Server;
using Arbiter.Net.Server.Messages;
using Arbiter.Net.Types;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using DAQuester.DarkAges.Protocol;
using Xunit;

namespace DAQuester.Tests;

public class DaydreamerProtocolTests
{
    static ServerNotice? Evidence(CharacterSnapshot snapshot, string name)
    {
        var property = typeof(CharacterSnapshot).GetProperty(name);
        Assert.NotNull(property);
        return (ServerNotice?)property.GetValue(snapshot);
    }

    internal static byte[] Message(byte type, string text)
    {
        var builder = new NetworkPacketBuilder(ServerCommand.Message);
        try
        {
            new ServerSystemMessage { MessageType = (WorldMessageType)type, Message = text }.Serialize(ref builder);
            return builder.ToPacket().Data;
        }
        finally { builder.Dispose(); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void Registration_accepts_only_exact_server_bar_evidence_without_exporting_nonactive_text(byte type)
    {
        var tracker = new StateTracker(17);
        var recorder = new ReplayRecorder();
        var logged = new List<string>();
        tracker.ServerMessage += (_, text) => logged.Add(text);
        tracker.Observed += (opcode, bytes) => recorder.Record(opcode, bytes, tracker.Snapshot);
        const string text = "Your expiration date is 9-11";
        tracker.Observe(0x0A, Message(type, text));
        tracker.Identity("Elara", 7);

        var registration = Assert.IsType<ServerNotice>(Evidence(tracker.Snapshot, "Registration"));
        Assert.Equal((17L, type, text), (registration.Generation, registration.Type, registration.Text));
        Assert.DoesNotContain("Registration", JsonSerializer.Serialize(tracker.Snapshot));
        Assert.Equal(type == 3 ? 1 : 0, logged.Count);
        Assert.Equal(type == 3 ? 1 : 0, recorder.JourneyReceipts("Connecting", 17, 0).Snapshots.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void Chat_and_popup_types_cannot_authorize_registration_or_daydreamer_results(byte type)
    {
        var tracker = new StateTracker(17);
        tracker.Identity("Elara", 7);
        tracker.Observe(0x0A, Message(type, "This account is on auto-renewal"));
        tracker.Observe(0x0A, Message(type, "This is your 30th consecutive Login!"));

        Assert.Null(Evidence(tracker.Snapshot, "Registration"));
        Assert.Null(Evidence(tracker.Snapshot, "DaydreamerStatus"));
        Assert.Null(tracker.Snapshot.ActiveStatus);
    }

    [Fact]
    public void Registration_is_connection_scoped_and_does_not_follow_a_replaced_character()
    {
        var tracker = new StateTracker(17);
        tracker.Observe(0x0A, Message(2, "Your account is on auto renewal."));
        tracker.Identity("Elara", 7);
        Assert.NotNull(Evidence(tracker.Snapshot, "Registration"));
        tracker.Identity("Elara", 7);
        Assert.NotNull(Evidence(tracker.Snapshot, "Registration"));
        tracker.Identity("Another", 8);
        Assert.Null(Evidence(tracker.Snapshot, "Registration"));
        tracker.Observe(0x0A, Message(3, "This account is on auto-renewal"));
        tracker.Observe(0x0A, Message(3, "This is your 2nd consecutive Login!"));
        tracker.Disconnect();
        Assert.Null(Evidence(tracker.Snapshot, "Registration"));
        Assert.Null(Evidence(tracker.Snapshot, "DaydreamerStatus"));
        Assert.Null(Evidence(new StateTracker(18).Snapshot, "Registration"));
    }

    [Fact]
    public void Only_recognized_active_results_are_retained_with_their_original_receipt_revision()
    {
        var tracker = new StateTracker(17);
        tracker.Identity("Elara", 7);
        tracker.Observe(0x0A, Message(3, "This is your 30th consecutive Login!"));
        var status = Assert.IsType<ServerNotice>(Evidence(tracker.Snapshot, "DaydreamerStatus"));
        tracker.Observe(0x0A, Message(3, "You received 20,000 gold coins."));
        Assert.Same(status, Evidence(tracker.Snapshot, "DaydreamerStatus"));
        Assert.True(status.Revision < tracker.Snapshot.Revision);
        Assert.Equal("You received 20,000 gold coins.", tracker.Snapshot.ActiveStatus?.Text);
        tracker.Observe(0x0A, Message(2, "This is your 31st consecutive Login!"));
        Assert.Same(status, Evidence(tracker.Snapshot, "DaydreamerStatus"));
        tracker.Identity("Another", 8);
        Assert.Null(Evidence(tracker.Snapshot, "DaydreamerStatus"));
    }

    [Theory]
    [InlineData("Other says: Your expiration date is 9-11")]
    [InlineData("Your expiration date is 13-11")]
    [InlineData("Your expiration date is 9-32")]
    [InlineData("Your expiration date is 9-11 next year")]
    public void Unrecognized_registration_text_does_not_authorize_a_connection(string text)
    {
        var tracker = new StateTracker(17);
        tracker.Observe(0x0A, Message(3, text));
        Assert.Null(Evidence(tracker.Snapshot, "Registration"));
    }

    [Fact]
    public void Truncated_system_message_cannot_establish_registration()
    {
        var tracker = new StateTracker(17);
        Assert.ThrowsAny<Exception>(() => tracker.Observe(0x0A, [3, 0, 40, 65]));
        Assert.Null(Evidence(tracker.Snapshot, "Registration"));
    }

    static void Adopt(StateTracker tracker, ServerNotice source, string character = "Elara", uint id = 7)
    {
        var method = typeof(StateTracker).GetMethod("AdoptLoginRegistration", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(tracker, [source, character, id]);
    }

    static object? LoginRegistration(CharacterSnapshot snapshot)
    {
        var property = typeof(CharacterSnapshot).GetProperty("LoginRegistration");
        Assert.NotNull(property);
        return property.GetValue(snapshot);
    }

    static CharacterSnapshot Bind(CharacterSnapshot snapshot, ServerNotice source, long generation = 17, long revision = 10,
        string character = "Elara", uint id = 7)
    {
        var type = typeof(CharacterSnapshot).Assembly.GetType("DAQuester.Core.LoginRegistrationReceipt");
        Assert.NotNull(type);
        var receipt = Activator.CreateInstance(type, source, generation, revision, character, id);
        var property = typeof(CharacterSnapshot).GetProperty("LoginRegistration");
        Assert.NotNull(property);
        property.SetValue(snapshot, receipt);
        return snapshot;
    }

    static bool CurrentRegistration(CharacterSnapshot snapshot)
    {
        var method = typeof(DaydreamerEvidence).GetMethod("HasCurrentRegistration", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return (bool)method.Invoke(null, [snapshot])!;
    }

    [Fact]
    public void Bound_login_registration_preserves_the_source_receipt_without_publishing_it_as_a_world_notice()
    {
        var source = new ServerNotice(16, 5, 2, "Your expiration date is 10-29", DateTimeOffset.UtcNow.AddSeconds(-1));
        var tracker = new StateTracker(17);
        tracker.Identity("Elara", 7);
        int observed = 0, logged = 0;
        tracker.Observed += (_, _) => observed++;
        tracker.ServerMessage += (_, _) => logged++;
        long before = tracker.Snapshot.Revision;

        Adopt(tracker, source);

        var snapshot = tracker.Snapshot;
        var binding = LoginRegistration(snapshot);
        Assert.NotNull(binding);
        Assert.IsType(typeof(CharacterSnapshot).Assembly.GetType("DAQuester.Core.LoginRegistrationReceipt")!, binding);
        Assert.Same(source, binding.GetType().GetProperty("Source")!.GetValue(binding));
        Assert.Equal(17L, binding.GetType().GetProperty("Generation")!.GetValue(binding));
        Assert.Equal(before + 1, binding.GetType().GetProperty("Revision")!.GetValue(binding));
        Assert.Null(snapshot.Registration);
        Assert.Null(snapshot.ActiveStatus);
        Assert.Null(snapshot.DaydreamerStatus);
        Assert.Equal((0, 0), (observed, logged));
        Assert.True(CurrentRegistration(snapshot));
        Assert.Equal(EligibilityKind.Eligible, DaydreamerEvidence.Eligibility(snapshot with
        {
            Hp = 100, Map = 500, Position = new(3, 4)
        }, new() { Character = "Elara", Quest = DaydreamerEvidence.Id }).Kind);
        string serialized = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("LoginRegistration", serialized);
        Assert.DoesNotContain(source.Text, serialized);
    }

    [Theory]
    [InlineData("disconnected")]
    [InlineData("character")]
    [InlineData("id")]
    [InlineData("source-generation-zero")]
    [InlineData("source-generation-current")]
    [InlineData("source-revision-zero")]
    [InlineData("source-text")]
    [InlineData("source-channel")]
    public void Login_registration_adoption_requires_connected_identity_and_genuine_other_generation_source(string invalid)
    {
        var tracker = new StateTracker(17);
        if (invalid != "disconnected") tracker.Identity("Elara", 7);
        var source = new ServerNotice(invalid == "source-generation-zero" ? 0 : invalid == "source-generation-current" ? 17 : 16,
            invalid == "source-revision-zero" ? 0 : 5, invalid == "source-channel" ? (byte)0 : (byte)2,
            invalid == "source-text" ? "Not registration" : "Your expiration date is 10-29", DateTimeOffset.UtcNow);
        long before = tracker.Snapshot.Revision;

        Adopt(tracker, source, invalid == "character" ? "Another" : "Elara", invalid == "id" ? 8u : 7u);

        Assert.Null(LoginRegistration(tracker.Snapshot));
        Assert.False(CurrentRegistration(tracker.Snapshot));
        Assert.Equal(before, tracker.Snapshot.Revision);
    }

    [Fact]
    public void Direct_world_registration_is_preferred_and_login_binding_resets_with_the_own_identity()
    {
        var source = new ServerNotice(16, 5, 2, "Your expiration date is 10-29", DateTimeOffset.UtcNow);
        var tracker = new StateTracker(17);
        tracker.Identity("Elara", 7);
        tracker.Observe(0x0A, Message(2, "This account is on auto-renewal"));
        var direct = tracker.Snapshot.Registration;
        long before = tracker.Snapshot.Revision;
        Adopt(tracker, source);
        Assert.Same(direct, tracker.Snapshot.Registration);
        Assert.Null(LoginRegistration(tracker.Snapshot));
        Assert.Equal(before, tracker.Snapshot.Revision);
        Assert.True(CurrentRegistration(tracker.Snapshot));

        var bound = new StateTracker(17);
        bound.Identity("Elara", 7);
        Adopt(bound, source);
        bound.Identity("Elara", 7);
        Assert.True(CurrentRegistration(bound.Snapshot));
        bound.Identity("Another", 8);
        Assert.Null(LoginRegistration(bound.Snapshot));
        Assert.False(CurrentRegistration(bound.Snapshot));
        bound.Identity("Elara", 7);
        Adopt(bound, source);
        bound.Disconnect();
        Assert.Null(LoginRegistration(bound.Snapshot));
        Assert.False(CurrentRegistration(bound.Snapshot));
    }

    [Theory]
    [InlineData("valid", true)]
    [InlineData("disconnected", false)]
    [InlineData("name-null", false)]
    [InlineData("id-null", false)]
    [InlineData("character", false)]
    [InlineData("id", false)]
    [InlineData("generation", false)]
    [InlineData("revision-zero", false)]
    [InlineData("revision-future", false)]
    [InlineData("source-generation-zero", false)]
    [InlineData("source-generation-current", false)]
    [InlineData("source-revision-zero", false)]
    [InlineData("source-text", false)]
    [InlineData("source-channel", false)]
    public void Login_registration_readiness_validates_binding_and_original_receipt_independently(string invalid, bool expected)
    {
        var source = new ServerNotice(invalid == "source-generation-zero" ? 0 : invalid == "source-generation-current" ? 17 : 16,
            invalid == "source-revision-zero" ? 0 : 5, invalid == "source-channel" ? (byte)0 : (byte)2,
            invalid == "source-text" ? "Not registration" : "Your expiration date is 10-29", DateTimeOffset.UtcNow);
        var snapshot = Bind(new CharacterSnapshot
        {
            Generation = 17, Revision = 10, Connected = invalid != "disconnected",
            Name = invalid == "name-null" ? null : "Elara", Id = invalid == "id-null" ? null : 7
        }, source, invalid == "generation" ? 18 : 17, invalid == "revision-zero" ? 0 : invalid == "revision-future" ? 11 : 10,
            invalid == "character" ? "Another" : "Elara", invalid == "id" ? 8u : 7u);

        Assert.Equal(expected, CurrentRegistration(snapshot));
    }

    [Theory]
    [InlineData(17, 1, true)]
    [InlineData(16, 1, false)]
    [InlineData(17, 0, false)]
    [InlineData(17, 11, false)]
    public void Direct_registration_requires_a_positive_observed_current_generation_revision(long generation, long revision, bool expected)
    {
        var snapshot = new CharacterSnapshot
        {
            Generation = 17, Revision = 10, Name = "Elara", Id = 7, Connected = true,
            Registration = new(generation, revision, 2, "Your expiration date is 10-29", DateTimeOffset.UtcNow)
        };
        Assert.Equal(expected, CurrentRegistration(snapshot));
    }
}
