using System.Collections.Immutable;
using System.Text.Json;
using DAQuester.Core;

namespace DAQuester.App.Services;

internal static class ResearchReplay
{
    // Read only the resource/status fields needed to settle a saved submission.
    // Packet payloads, other character metadata and analysis are not loaded.
    sealed record Snapshot(long Generation, long Revision, string? Name, bool Connected, int? Map,
        uint? Gold, uint? Hp, uint? MaxHp, bool InventoryKnown, ImmutableDictionary<byte, Item>? Inventory, Dialog? Dialog, ServerNotice? ActiveStatus);
    sealed record Event(string Character, long Generation, long Revision, string Direction, Snapshot Snapshot);
    sealed record Replay(int SchemaVersion, string ServerProfile, bool Sanitized, Event[]? Events);

    internal static async Task<CharacterSnapshot[]> ReadAsync(string path, string character, long generation,
        long after, string profile, CancellationToken cancellation)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var replay = await JsonSerializer.DeserializeAsync<Replay>(stream, cancellationToken: cancellation);
        if (replay is not { SchemaVersion: 3, Sanitized: true, Events: { } } || replay.ServerProfile != profile)
            return [];
        var result = new List<CharacterSnapshot>();
        foreach (var e in replay.Events)
        {
            cancellation.ThrowIfCancellationRequested();
            if (e is not { Direction: "S", Snapshot: { } s } || e.Generation != generation || e.Revision <= after ||
                s.Generation != e.Generation || s.Revision != e.Revision ||
                !string.Equals(e.Character, character, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(s.Name, character, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new() { Name = s.Name, Generation = s.Generation, Revision = s.Revision, Connected = s.Connected,
                Map = s.Map, Gold = s.Gold, Hp = s.Hp, MaxHp = s.MaxHp, InventoryKnown = s.InventoryKnown, Inventory = s.Inventory ?? [],
                Dialog = s.Dialog, ActiveStatus = s.ActiveStatus });
        }
        return result.OrderBy(s => s.Revision).ToArray();
    }
}
