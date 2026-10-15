using DAQuester.Core;

namespace DAQuester.DarkAges;

public sealed partial class GameSession
{
    sealed record EntryBlock(ServerNotice Notice, int? Map, bool Weapon, bool Shield);
    volatile EntryBlock? entryBlock;

    void ObserveEntryRefusal(CharacterSnapshot s)
    {
        // Latch the first actual refusal so navigation cannot hammer the door.
        // Equipment evidence, map changes or a new generation release this gate.
        if (!s.Connected || entryBlock is { } previous && (previous.Notice.Generation != s.Generation || previous.Map != s.Map))
            entryBlock = null;
        if (s.ActiveStatus is { Type: 3 } notice && notice.Generation == s.Generation && notice.Revision == s.Revision &&
            // Path Temple's Monk Nightmare tile reports class eligibility. It
            // does not refuse ordinary travel to Donnan or Erin.
            !(s.Map == 395 && notice.Text == "You are not a Monk.") &&
            (notice.Text is "Your armaments are repelled from here." or "Your must be armed to enter here." ||
                notice.Text.StartsWith("You are not a ", StringComparison.Ordinal) && notice.Text.EndsWith(".", StringComparison.Ordinal)))
            entryBlock = new(notice, s.Map, s.Equipment.ContainsKey(1), s.Equipment.ContainsKey(3));
        if (entryBlock is not { } blocked)
            return;
        if (blocked.Notice.Text == "Your armaments are repelled from here." && (blocked.Weapon || blocked.Shield) &&
            s.InventoryKnown && !s.Equipment.ContainsKey(1) && !s.Equipment.ContainsKey(3) ||
            blocked.Notice.Text == "Your must be armed to enter here." && !blocked.Weapon && s.Equipment.ContainsKey(1))
            entryBlock = null;
    }

    void RequireEntryAllowed()
    {
        if (entryBlock is { } blocked)
            throw new QuestBlockedException("Entry blocked: " + blocked.Notice.Text + " Reconcile requirements before another approach.");
    }
}
