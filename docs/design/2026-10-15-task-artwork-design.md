# Original game artwork in Auto Tasks

The player requested game sprites in Auto Tasks using the existing quest artwork treatment, then explicitly selected **inventory sprite 3, Parchment Scroll**, for Wizardy research. Altar Worship reuses its previously approved ring artwork and recipe alias. A spellbook and offering shield were considered during local visual discovery; the selected parchment and existing ring are the final choices.

Use the existing QuestGlyph, metadata catalog, read-only installed archive decoder and worker cache. Task rows use the same 36-pixel circular crop as quest rows; the details title uses the same control at 48 pixels. Preserve transparency, original palettes and nearest-neighbor rendering. Missing game files retain the existing neutral fallback and Settings hint; errors remain in diagnostics.log.

Add a research artwork catalog entry and bind each real task's recipe ID and configured game directory. Task item view models expose their runtime for existing artwork diagnostics. No game files or extracted images are packaged or committed, and no character operation or quest behavior changes.

Implementation and verification: adapt the existing recipe/artwork integration test before editing metadata; register research sprite 3; reuse QuestGlyph in task rows and details; run focused tests, both complete xUnit projects and a separate Release build. Review actual installed sprite rendering and native full/compact layouts using isolated data. Discovery previews remain under ignored artifacts. This records the player's selected sprite and requested existing design pattern; the writing-plans skill is unavailable.
