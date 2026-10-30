// Adapted from DAOrganizer's SlotPlanner and PotionSlots.
// MIT License
// Copyright (c) 2026 BuildWithRaymond and DAOrganizer contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Immutable;

namespace DAQuester.Core;

public sealed record BagSlotSwap(byte Source, byte Target);

public sealed record BagSortPlan(
    ImmutableDictionary<byte, Item> Original,
    ImmutableDictionary<byte, Item> Desired,
    ImmutableArray<BagSlotSwap> Swaps);

public static class BagSortPlanner
{
    private static readonly string[] QuickItems = ["Dual Crystal Arrows", "Komadium", "Red Potion", "Exkuranum", "Dibenomum", "Hemloch"];

    // Static DAOrganizer category rules only. Personal category overrides and
    // its saved cross-character equipment history are deliberately not loaded.
    public static BagSortPlan Create(IReadOnlyDictionary<byte, Item> inventory)
    {
        Validate(inventory);
        var original = inventory.ToImmutableDictionary();
        // Keep original slots here as stack identities, including equal stacks.
        var desired = new Dictionary<byte, Item>();
        var assigned = new HashSet<byte>();
        foreach (var name in QuickItems)
        {
            var target = (byte)Enumerable.Range(1, 12).First(slot => !desired.ContainsKey((byte)slot));
            var item = original.Values.Where(item => item.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Slot == target ? 0 : 1).ThenBy(item => item.Slot).FirstOrDefault();
            if (item is null)
                continue;
            desired[target] = item;
            assigned.Add(item.Slot);
        }

        var trinkets = original.Values.Where(item => !assigned.Contains(item.Slot) && BagSortClassification.Infer(item.Name) == "Trinkets")
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Slot);
        var right = Enumerable.Range(1, 12).Reverse().Select(slot => (byte)slot).Where(slot => !desired.ContainsKey(slot));
        foreach (var (item, target) in trinkets.Zip(right))
        {
            desired[target] = item;
            assigned.Add(item.Slot);
        }

        var ordered = original.Values.Where(item => !assigned.Contains(item.Slot))
            .Select(item => (Item: item, Category: BagSortClassification.Infer(item.Name)))
            .OrderBy(entry => BagSortClassification.Order(entry.Category))
            .ThenBy(entry => entry.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => BagSortClassification.Family(entry.Item.Name, entry.Category), StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Item.Slot).ToArray();
        var slots = Enumerable.Range(1, CharacterSnapshot.InventoryCapacity)
            .OrderByDescending(slot => (slot - 1) / 12).ThenBy(slot => slot)
            .Select(slot => (byte)slot).Where(slot => !desired.ContainsKey(slot)).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            desired[slots[i]] = ordered[i].Item;

        var occupants = original.Keys.ToDictionary(slot => slot, slot => slot);
        var swaps = ImmutableArray.CreateBuilder<BagSlotSwap>();
        foreach (var (target, item) in desired.OrderBy(entry => entry.Key))
        {
            var from = occupants.First(entry => entry.Value == item.Slot).Key;
            if (from == target)
                continue;
            // Equal stack payloads have no observable swap receipt. Exchange
            // their planning identities without sending a physical no-op.
            if (!occupants.TryGetValue(target, out var targetOriginal) ||
                item != (original[targetOriginal] with { Slot = item.Slot }))
                swaps.Add(new(from, target));
            if (occupants.Remove(target, out var displaced))
                occupants[from] = displaced;
            else
                occupants.Remove(from);
            occupants[target] = item.Slot;
        }

        return new(original, desired.ToImmutableDictionary(entry => entry.Key, entry => entry.Value with { Slot = entry.Key }), swaps.ToImmutable());
    }

    public static bool Matches(IReadOnlyDictionary<byte, Item> left, IReadOnlyDictionary<byte, Item> right) =>
        IsWellFormed(left) && IsWellFormed(right) && left.Count == right.Count &&
        left.All(entry => right.TryGetValue(entry.Key, out var item) && entry.Value == item);

    public static ImmutableDictionary<byte, Item> ApplySwap(IReadOnlyDictionary<byte, Item> inventory, BagSlotSwap swap)
    {
        Validate(inventory);
        ArgumentNullException.ThrowIfNull(swap);
        if (swap.Source is < 1 or > CharacterSnapshot.InventoryCapacity || swap.Target is < 1 or > CharacterSnapshot.InventoryCapacity ||
            swap.Source == swap.Target || !inventory.TryGetValue(swap.Source, out var source))
            throw new ArgumentException("An inventory swap requires an occupied source and distinct item slots 1–59.", nameof(swap));
        var result = inventory.ToImmutableDictionary().ToBuilder();
        result.Remove(swap.Source);
        if (inventory.TryGetValue(swap.Target, out var destination))
            result[swap.Source] = destination with { Slot = swap.Source };
        result[swap.Target] = source with { Slot = swap.Target };
        return result.ToImmutable();
    }

    private static void Validate(IReadOnlyDictionary<byte, Item> inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (!IsWellFormed(inventory))
            throw new ArgumentException("Inventory items must match their slot keys and use item slots 1–59.", nameof(inventory));
    }

    private static bool IsWellFormed(IReadOnlyDictionary<byte, Item>? inventory) =>
        inventory is not null && inventory.All(entry => entry.Key is >= 1 and <= CharacterSnapshot.InventoryCapacity &&
            entry.Value is not null && entry.Value.Slot == entry.Key && entry.Value.Name is not null);
}
