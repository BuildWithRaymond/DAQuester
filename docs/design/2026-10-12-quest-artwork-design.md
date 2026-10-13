# Quest artwork in the planner

Status: The user approved the 71 quest-to-sprite choices on 2026-10-12 and requested
equal circular crops with no white backgrounds, including the wolf thumbnail.

The quest library and current journey use 36-pixel circles. Items retain their
whole silhouettes; creatures and NPCs use a face or upper-body crop. Lorai uses
the lower part of its sprite because its face is below its tail. Sprites use
nearest-neighbor sampling and the existing dark theme.

`content/quest-artwork.json` records the approved choices and their Vorlof source
links. Explicit recipe aliases make the current journey resolve to the same
artwork as its library row. The metadata contains no game artwork or private
paths. Multi-route guides use the representative item approved in the preview.
The merged Terror of the Alley / Garden guide resolves to the existing approved
Alley creature; both legacy IDs remain valid. The newly merged Awakening,
Peasant's First Steps and Crypt Climb use the neutral fallback until their own
sprite choices are approved. No new sprite IDs are inferred for those quests.

Artwork is decoded read-only from the selected client's `legend.dat` and
`hades.dat`. Inventory artwork uses one-based IDs in banks of 266 EPF frames,
`itempal.tbl` palette ranges, and original dyes for colored variants. Creatures
use a static front-facing MPF frame and its palette; the multipart Arsaidh Aon
dragon uses its named head section. Native frame bounds were checked against
the guide thumbnails before implementation.

Palette index zero supplies transparency. White fur, highlights, and garments
remain opaque; the guide thumbnails' baked white background is never loaded
into the application. A shared worker cache loads artwork away from the UI
thread. Each control owns and disposes its bitmap, rejecting stale results after
quest changes, client changes, or removal from the visual tree.

Missing artwork keeps a neutral circular placeholder with a Settings hint.
Decoder details go to diagnostics.log. No new artwork, guide images, game files,
network fetches, character observations, or gameplay actions are packaged or
performed by this feature. Quest policies and reconciliation remain unchanged.

Verification includes synthetic palette/transparency/archive fixtures, catalog
and recipe coverage, both complete xUnit projects, and manual review of actual
native artwork and the planner at normal and minimum window sizes. The review
uses a separate output folder and isolated synthetic application data.
