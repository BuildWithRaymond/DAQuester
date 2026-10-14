# Character preview

The current journey shows the selected character's original game sprite. It reads
artwork from the game directory already selected in Settings. It does not extract,
modify or package game files, open a client, or issue gameplay actions.

Appearance, facing, walking and body actions come from observed packets for that
character ID in the current connection generation. A different character's
appearance cannot replace it. The latest own appearance is saved per character and
server profile in character memory. Logout and app restart retain that outfit;
it is shown idle until a fresh live appearance arrives. Saved appearance is never
placed back into a live snapshot and cannot prove quest actions. Characters with
no saved or live appearance render a clothed default outfit
using the original head, shirt, pants and boots from the selected game folder's
DAT files. The default never becomes account data or observed appearance. There
is no replacement artwork bundled with the app; a client path is needed for both
default and live sprites. A loaded character with unavailable or hidden artwork
does not fall back to the default outfit. Preview data does not prove quest completion, movement arrival
or rewards.

The preview matches the observed appearance and action using original frames.
Packets do not contain the native client's current animation frame or its random
idle choices, so animation phase is reconstructed rather than frame-synchronized.
Walking ends after its observed movement cycle; it does not loop forever after a
single movement packet. Sitting/resting forms, monster transformations and emotes
use their installed SPF, MPF and EPF artwork. Missing artwork leaves a neutral
placeholder with a short explanation; parser details go to diagnostics.log.

Rendering runs away from the UI thread. The preview owns its decoder/cache and
bitmap. Character changes, disconnect and window removal invalidate in-flight
results before they can appear. Original frame offsets and nearest-neighbor
display keep the sprite aligned and sharp.

Display notifications are batched into one pending update at background UI priority,
with a 50 ms delay. The sprite's 33 ms animation timer remains independent. Repeated
movement packets reuse saved display receipts, while a successful or failed checkpoint
write invalidates the affected caption data. Every explicit refresh reads saved state
again; the normal two-second tick also scans external changes off the UI thread.
Late scans cannot overwrite a newer save. Queue execution, reconciliation, active
messages, replay recording and diagnostic lines keep their existing delivery paths.
The display cache is never used as a game receipt or by an execution gate.

A decorative copy behind the journey sprite uses the same published bitmap,
enlarged 5.5 times with nearest-neighbor sampling at 12% opacity. It cannot drift
to another animation frame and stays clipped to the journey side of the card.
The foreground keeps its original frame offsets.
The accent stays centered on the foreground's canvas with a foot reference fixed
from its first visible frame. Animated limbs and weapons can change the cropped
outline without recentering the whole character. Changing characters or outfits
clears that reference before the next frame appears.

The character chooser, Characters page and Run history use circular portraits
composed from original front-facing head and face layers. Face zero uses the
original head skin embedded in the body artwork, clipped with the original face
silhouette so the torso stays out of the portrait. Each reads that character's own
live or saved appearance; characters without either use the default DAT outfit.
Portraits are presentation only and do not add observed facts to an account.

To change the implementation:

- `Core/CharacterAppearance.cs`: observed appearance and motion fields.
- `Core/CharacterMemory.cs`: durable appearance IDs, isolated by character and server.
- `DarkAges/Protocol/StateTracker.cs`: packet decoding, own-character filtering
  and disconnect clearing.
- `DarkAges/Rendering/DatSpriteCatalog.cs`: bounded, read-only DAT indexing.
- `DarkAges/Rendering/SpritePose.cs`: direction and action frame selection.
- `DarkAges/Rendering/CharacterSpriteRenderer*.cs`: indexed layers, palettes,
  dyes, resting forms and monster forms.
- `DarkAges/Rendering/SpfSprite.cs`: RGB565 SPF decoder.
- `App/Controls/CharacterSpritePreview.cs`: background rendering and bitmap lifetime.
- `App/Controls/CharacterSpritePreview.Drawing.cs`: sprites, circular portraits and accent placement.
- `App/Controls/CharacterSpriteAccent.cs`: decorative copy of the published live frame.
- `App/Controls/SpriteAccentLayout.cs`: stable canvas anchor for the enlarged accent.
- `App/Controls/SpriteImageBounds.cs`: transparent padding bounds for portraits and accents.
- `App/Controls/CharacterPreviewAppearance.cs`: default DAT outfit and live appearance selection.
- `App/Controls/QuestTrail.cs` and `App/ViewModels/QuestProgress.cs`: quest activity
  animation and checkpoint-derived step counts.

Step counts describe recipe checkpoints. A navigation step can contain many
walks, and gathering can take a variable amount of time. Activity animation never
advances saved progress. Paused, disconnected and completed quests stay still.
Trails use normal 12-pixel numbered markers across the card width. Longer recipes
wrap into rows instead of compressing their dots beside the sprite. Number labels
and pens are reused between animation frames; only the activity glow and moving
light change. At a row boundary the current marker pulses without a diagonal
moving light across other stages.

Tests use fabricated packet payloads and tiny generated image archives; they do
not require installed game artwork or operate real characters. Manually review
the sprite, progress trail and grouped buttons in normal and compact windows.

Verified completion celebrations are separate from observed character motions.
Effect 50 uses the original blue spiral over matching visible avatars, anchored
to the full sprite's visible feet. The effect loads the
installed `roh.dat` frame sequences, anchor tables and palettes read-only; no
effect artwork is bundled. An unselected character receives a brief sprite
notice so its celebration remains visible without changing character selection.

The saved runner/permanent outcome event follows the existing receipt rules and
deduplicates by character, quest and outcome time. Restored history never replays.
Weekly Hairstyle reward receipts celebrate as attempts without claiming a Suomi
legend. The overlay changes neither game snapshots nor replayed observations,
checkpoint state, four quest policies or queue ownership. It does not block
input, serializes simultaneous fallback notices, and respects reduced motion.
