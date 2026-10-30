# Bag sorting

Approved by the user on 2026-10-28, with category sorting selected. Amended on
2026-10-29 to use an ordinary click instead of holding to confirm.

## Behavior

Add **Sort Bag** to the selected-character views (Quest planner and Auto Tasks).
Add **Sort All Bags** to views covering multiple characters (Dashboard,
Characters, Scheduler and Run history). Settings has no bag action.

Activate the compact button with an ordinary click or its standard keyboard
activation. No countdown, progress confirmation or confirmation popup is shown.
The user requested a narrower quick action after reviewing the initial render:
90 px for Sort Bag, 112 px for Sort All Bags, retaining the shared 48 px height.
One activation starts at most one operation. There are no confirmation dialogs or
slot-by-slot move popups. Completion, skipped characters and failures use the
existing inline notice/error area.

Sort All Bags covers saved, connected characters. It never launches or logs in
a character. The roster is captured when the command starts; eligibility and
connection identity are checked again before sorting each character. Busy,
offline or unobserved bags are skipped, with a concise aggregate result.

## Sorting choice

DAOrganizer has separate category and alphabetical sorts, with no shared saved
default. Use **category sorting**, retaining its family and alphabetical ordering
inside categories. A mode selector is unnecessary for this request.

Both alternatives preserve DAOrganizer's layout preferences:

- One stack each of Dual Crystal Arrows, Komadium, Red Potion, Exkuranum,
  Dibenomum and Hemloch occupies consecutive available first-row slots from
  the left, in that order. Missing items do not reserve holes.
- Trinkets are alphabetical across the first row from the right, without
  replacing quick-use items. Excess trinkets join the ordinary ordering.
- Remaining items fill rows bottom-up, each row left-to-right:
  49–59, 37–48, 25–36, 13–24, then remaining 1–12.
- Category order is Weapons, Armor, Accessories, Trinkets, Consumables,
  Materials, Books & scrolls, Tools, Cosmetics, Pets, Other.
- Duplicate stacks retain separate identities. Sorting only moves/swaps items;
  it never splits, merges, drops, consumes or equips them.

Use DAOrganizer's static source classification and ordering rules. DAQuester has
no pin editor or private category database, so this feature introduces neither
and does not read DAOrganizer's personal profile. Personal category overrides
and Organizer's saved equipment-derived categories are not shared between apps.
Any adapted substantial code keeps DAOrganizer's MIT attribution.

## Implementation boundaries

A pure Core planner computes the desired layout and conserving swap sequence.
A single GameSession command holds its action semaphore throughout the sort.
Before each write, validate the same connection generation, character identity,
complete expected inventory and absence of conflicting manual input inside the
transport send lock. Send the native inventory slot-change packet once, then
await the complete authoritative post-swap bag, including intermediate inventory
packets. Use DAOrganizer's four-second acknowledgement limit and 150 ms spacing.

Unexpected inventory changes, conflicting player input, disconnect, cancellation
or an uncertain acknowledgement stop the operation. Never retry an uncertain
swap automatically or optimistically alter observed inventory.

DesktopRuntime uses existing control ownership and cancellation. Refuse active
quests, reserved/locked queues, scheduler ownership, launches, calibration,
another control action, an open dialogue/exchange or an unknown inventory.
Never stop another owner to make sorting possible. Stop/Shutdown can cancel
sorting through the existing ownership lifecycle. Keep quest input semantics and
checkpoint behavior unchanged.

## Alternatives considered

1. **Category sorting (recommended):** matches DAOrganizer's grouping and special
   slots with one clear action.
2. **Alphabetical sorting:** matches DAOrganizer's name mode and special slots;
   simpler ordering if grouping is unwanted.
3. **Expose both modes:** adds a selector and persistent preference. Defer because
   the request calls for a simple Sort Bag action.

## Verification

Use synthetic fixtures only. Cover special items, trinkets, ordinary row order,
duplicate stacks, full bags, conservation, no-op layouts, acknowledgement timing,
manual interference, changed generation and no retry after uncertainty. Cover
ordinary-click activation, disabled and concurrent command rejection, scope
selection and runtime ownership cancellation.
Run focused tests and both complete xUnit projects. Review the shared dark
toolbar manually at normal/minimum sizes, including ordinary mouse and keyboard
activation and disabled states. Do not operate real characters or modify/restart
Excalibur.
