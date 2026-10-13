# Application icon

Approved on October 12, 2026.

The application mark is a round quest medallion: a beveled warm gold rim around
a dark navy face, with raised gold parchment and a large dark exclamation mark.
Broad shapes and strong contrast keep the quest identity recognizable at small
sizes. Lighting and material depth complement the existing fully dark theme.

The reference supplied by the user informed the circular silhouette and material
depth. Its compass geometry is not part of the new mark. The new artwork was
created for DAQuester using the built-in image generation tool.

`src/DAQuester.App/Assets/daquester.png` supplies the in-app brand image at
256 pixels. `daquester.ico` contains 32-bit transparent frames at 16, 20, 24, 32,
40, 48, 64, 96, 128 and 256 pixels. Existing window, executable and installer
references retain their filenames and consume the replacement artwork.

Verify the exported frames visually at actual sizes on the dark theme, confirm
that the icon decodes and is embedded in the release executable, run both xUnit
projects, and build and verify a separate release candidate. This change does
not alter application behavior or game interactions.
