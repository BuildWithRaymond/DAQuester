using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DAQuester.Core;
// Planning and appearance memory are deliberately separate from live quest evidence.
public sealed record CharacterMemory
{
    public CharacterAppearance? Appearance { get; init; }
    public DateTimeOffset? AppearanceObservedAt { get; init; }
    public string Character { get;
        init; } = "";
    public int? Level
    {
        get;
        init;
    }
    public DateTimeOffset? LevelObservedAt
    {
        get;
        init;
    }
    public string[] Legends { get;
        init; } = [];
    public DateTimeOffset? LegendsObservedAt
    {
        get;
        init;
    }

    public string? CompletedLegend(string quest, CharacterSnapshot? current = null)
    {
        var legends = IsOwn(current, Character) && current!.Connected && FreshLegends(current) ? current.Legends : LegendsObservedAt is not null ? Legends.ToImmutableArray() : [];
        return quest switch
        {
            "entered-muisir-village" => MuisirEvidence.LegendMark(new() { Legends = legends }),
            "mundane-birth" => legends.FirstOrDefault(l => l.Contains("mundane birth", StringComparison.OrdinalIgnoreCase)),
            "emerald-dawn" => EmeraldDawnEvidence.FinalMark(new() { LegendsKnown = true, Legends = legends }),
            "perfect-hairstyle" => legends.FirstOrDefault(l => l.Contains(PerfectHairstyleEvidence.Legend, StringComparison.OrdinalIgnoreCase)),
            _ => null
        };
    }

    public Eligibility? SelectionRestriction(string quest, CharacterSnapshot? current = null)
    {
        bool own = IsOwn(current, Character);
        int? level = own ? current!.Level ?? Level : Level;
        if (quest == "spare-a-stick" && level is < 1 or > 11)
            return new(EligibilityKind.Blocked, "Requires level 1–11.");
        if (quest == ShinewoodFaerieEvidence.Id && level is { } known && known < ShinewoodFaerieEvidence.MinimumLevel)
            return new(EligibilityKind.Blocked, "Requires level 11+ to pass Shinewood Forest 7 and reach the Faerie.");
        if (CompletedLegend(quest, current) is { } legend)
            return new(EligibilityKind.Blocked, "Already completed: " + legend + ".");
        return null;
    }

    internal static bool IsOwn(CharacterSnapshot? snapshot, string character) => snapshot?.Id is not null && string.Equals(snapshot.Name, character, StringComparison.OrdinalIgnoreCase);
    internal static bool FreshLegends(CharacterSnapshot snapshot) => snapshot.LegendsKnown && snapshot.LegendsRevision > 0 && snapshot.LegendsRevision <= snapshot.Revision;
}

public sealed class FileCharacterMemory(string directory, string serverProfile)
{
    readonly SemaphoreSlim gate = new(1, 1);
    readonly Dictionary<string, CharacterMemory> cache = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (long Generation, long Revision, long LegendsRevision)> seen = new(StringComparer.OrdinalIgnoreCase);
    string PathFor(string character)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(character);
        return Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serverProfile + "|" + character.ToUpperInvariant()))) + ".json");
    }

    public CharacterMemory Load(string character)
    {
        lock (cache)
        {
            if (cache.TryGetValue(character, out var saved))
                return saved;
            string path = PathFor(character);
            if (!File.Exists(path))
                return cache[character] = new()
                {
                    Character = character
                };
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            saved = JsonSerializer.Deserialize<CharacterMemory>(stream) ?? throw new InvalidDataException("Unreadable saved character marks.");
            if (!string.Equals(saved.Character, character, StringComparison.OrdinalIgnoreCase) || saved.Legends is null || saved.Level is < 1)
                throw new InvalidDataException("Saved character marks do not match this character.");
            return cache[character] = saved;
        }
    }

    public async Task<CharacterMemory> ObserveAsync(string character, CharacterSnapshot snapshot, CancellationToken cancellation = default)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            var saved = Load(character);
            if (!snapshot.Connected || !CharacterMemory.IsOwn(snapshot, character) || snapshot.Generation < 1 || snapshot.Revision < 1)
                return saved;
            bool previous = seen.TryGetValue(character, out var last);
            if (previous && (snapshot.Generation < last.Generation || snapshot.Generation == last.Generation && snapshot.Revision <= last.Revision))
                return saved;
            bool fresh = CharacterMemory.FreshLegends(snapshot) && (!previous || snapshot.Generation != last.Generation || snapshot.LegendsRevision > last.LegendsRevision);
            var next = saved;
            if (snapshot.Appearance is { } appearance && appearance != saved.Appearance)
                next = next with
                {
                    Appearance = appearance,
                    AppearanceObservedAt = DateTimeOffset.UtcNow
                };
            if (snapshot.Level is > 0 && snapshot.Level != saved.Level)
                next = next with
                {
                    Level = snapshot.Level,
                    LevelObservedAt = DateTimeOffset.UtcNow
                };
            if (fresh)
                next = next with
                {
                    Legends = snapshot.Legends.ToArray(),
                    LegendsObservedAt = DateTimeOffset.UtcNow
                };
            if (next != saved)
            {
                await AtomicJsonFile.WriteAsync(PathFor(character), next, "Could not save character marks. Previous memory remains saved.", cancellation);
                lock (cache)
                    cache[character] = next;
            }

            long legendRevision = fresh ? snapshot.LegendsRevision : previous && last.Generation == snapshot.Generation ? last.LegendsRevision : 0;
            seen[character] = (snapshot.Generation, snapshot.Revision, legendRevision);
            return next;
        }
        finally
        {
            gate.Release();
        }
    }
}
