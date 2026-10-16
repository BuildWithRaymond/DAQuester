using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App.Services;

internal static class DanaanCourtReplay
{
    // Project only the fields this prefix can prove. Payloads, entities, legends,
    // appearance and analysis would inflate large replay reads in the x86 app.
    sealed record Snapshot(long Generation, long Revision, string? Name, bool Connected, int? Map,
        uint? Gold, uint? Experience, bool InventoryKnown, ImmutableDictionary<byte, Item>? Inventory, Dialog? Dialog,
        Tile? Position, ServerNotice? ActiveStatus);
    sealed record Event(string Character, long Generation, long Revision, string Direction, Snapshot Snapshot);
    sealed record Replay(int SchemaVersion, string ServerProfile, bool Sanitized, Event[]? Events, RecordedJourneyAction[]? Actions);
    sealed record AssignmentSnapshot(long Generation, long Revision, string? Name, bool Connected, int? Map, Dialog? Dialog);
    sealed record AssignmentEvent(string Character, long Generation, long Revision, string Direction, AssignmentSnapshot Snapshot);
    sealed record AssignmentReplay(int SchemaVersion, string ServerProfile, bool Sanitized, AssignmentEvent[]? Events, RecordedJourneyAction[]? Actions);
    sealed record ReferralSnapshot(long Generation, long Revision, string? Name, bool Connected, int? Map, Tile? Position,
        uint? Gold, uint? Experience, bool InventoryKnown, Dialog? Dialog);
    sealed record ReferralEvent(string Character, long Generation, long Revision, string Direction, ReferralSnapshot Snapshot);
    sealed record ReferralReplay(int SchemaVersion, string ServerProfile, bool Sanitized, ReferralEvent[]? Events, RecordedJourneyAction[]? Actions);

    internal static async Task<(CharacterSnapshot[] Snapshots, RecordedJourneyAction[] Actions)> ReadReferralsAsync(
        string path, string character, long generation, long after, string profile, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        // These stages exchange information only. Do not deserialize every bag
        // and entity from a long x86 replay to prove its source pages and closes.
        var replay = await JsonSerializer.DeserializeAsync<ReferralReplay>(stream, cancellationToken: cancellation);
        if (replay is not { SchemaVersion: 3, Sanitized: true, Events: { }, Actions: { } } || replay.ServerProfile != profile)
            return ([], []);
        bool Relevant(long g, long r) => g > generation || g == generation && r >= after;
        var snapshots = new List<CharacterSnapshot>();
        foreach (var e in replay.Events)
        {
            cancellation.ThrowIfCancellationRequested();
            if (e is not { Direction: "S", Snapshot: { } s } || !Relevant(e.Generation, e.Revision) ||
                s.Generation != e.Generation || s.Revision != e.Revision ||
                !string.Equals(e.Character, character, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(s.Name, character, StringComparison.OrdinalIgnoreCase)) continue;
            snapshots.Add(new() { Generation = s.Generation, Revision = s.Revision, Name = s.Name, Connected = s.Connected,
                Map = s.Map, Position = s.Position, Gold = s.Gold, Experience = s.Experience, InventoryKnown = s.InventoryKnown, Dialog = s.Dialog });
        }
        return (snapshots.ToArray(), replay.Actions.Where(a => a is not null && Relevant(a.Generation, a.Revision) &&
            string.Equals(a.Character, character, StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    internal static async Task<(CharacterSnapshot[] Snapshots, RecordedJourneyAction[] Actions)> ReadDonnanAsync(
        string path, string character, string profile, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        // Assignment recovery crosses connections. Keep the original identities
        // and revisions; exclude all bag/entity/payload data from this projection.
        var replay = await JsonSerializer.DeserializeAsync<AssignmentReplay>(stream, cancellationToken: cancellation);
        if (replay is not { SchemaVersion: 3, Sanitized: true, Events: { }, Actions: { } } || replay.ServerProfile != profile)
            return ([], []);
        var snapshots = new List<CharacterSnapshot>();
        foreach (var e in replay.Events)
        {
            cancellation.ThrowIfCancellationRequested();
            if (e is not { Direction: "S", Snapshot: { } s } || s.Generation != e.Generation || s.Revision != e.Revision ||
                !string.Equals(e.Character, character, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(s.Name, character, StringComparison.OrdinalIgnoreCase) || s.Dialog?.Name is not ("Donnan" or "Kyros" or "Erin"))
                continue;
            snapshots.Add(new() { Generation = s.Generation, Revision = s.Revision, Name = s.Name, Connected = s.Connected, Map = s.Map, Dialog = s.Dialog });
        }
        return (snapshots.ToArray(), replay.Actions.Where(a => a is not null &&
            string.Equals(a.Character, character, StringComparison.OrdinalIgnoreCase) && a.Dialog?.Name is "Donnan" or "Kyros" or "Erin").ToArray());
    }

    internal static async Task<(CharacterSnapshot[] Snapshots, RecordedJourneyAction[] Actions)> ReadAsync(
        string path, string character, long generation, long after, string profile, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var replay = await JsonSerializer.DeserializeAsync<Replay>(stream, cancellationToken: cancellation);
        if (replay is not { SchemaVersion: 3, Sanitized: true, Events: { }, Actions: { } } || replay.ServerProfile != profile)
            return ([], []);
        var snapshots = new List<CharacterSnapshot>();
        foreach (var e in replay.Events)
        {
            cancellation.ThrowIfCancellationRequested();
            if (e is not { Direction: "S", Snapshot: { } s } || e.Generation != generation || e.Revision < after ||
                !string.Equals(e.Character, character, StringComparison.OrdinalIgnoreCase) ||
                s.Generation != e.Generation || s.Revision != e.Revision || !string.Equals(s.Name, character, StringComparison.OrdinalIgnoreCase))
                continue;
            snapshots.Add(new()
            {
                Generation = s.Generation, Revision = s.Revision, Name = s.Name, Connected = s.Connected, Map = s.Map,
                Gold = s.Gold, Experience = s.Experience, InventoryKnown = s.InventoryKnown, Inventory = s.Inventory ?? [], Dialog = s.Dialog,
                Position = s.Position, ActiveStatus = s.ActiveStatus
            });
        }
        return (snapshots.ToArray(), replay.Actions.Where(a => a is not null && a.Generation == generation && a.Revision >= after &&
            string.Equals(a.Character, character, StringComparison.OrdinalIgnoreCase)).ToArray());
    }
}
