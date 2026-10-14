# Character and dashboard polish

The Characters page should open with saved accounts collapsed. Each summary shows the portrait, character name, observed level and connection state. Launch and planner selection remain available. An accessible disclosure reveals account fields, login status, removal and legend marks. New unsaved accounts start expanded so name entry and existing keyboard focus still work. Expansion is local presentation state and survives ordinary refresh and navigation.

Keep the dashboard's split list and selected-character detail. A stacked detail would crowd the minimum window, while retaining one scrolling detail hides the journey and mixes queue navigation with progress. The selected detail uses a fixed journey, a separately scrolling queue when space permits, and anchored actions. At short window heights, show the queue summary with access to the planner and compress recent outcomes to its history link.

Use the existing navy surfaces, slate outlines, gold selection and teal status. Keep Georgia for restrained headings and Segoe UI for labels and controls. Align spacing to the shared four-pixel rhythm. The signature remains the real character sprite with a faint enlarged rendering of the same frame behind it. Clip only decorative artwork; constrain long text deliberately with complete tooltips.

Do not alter quest actions, ownership, saved checkpoints or live sessions. Verify saved/draft expansion behavior with focused tests, run both full xUnit projects, build the desktop app, and manually inspect native synthetic captures at normal and minimum sizes, including expanded accounts and long journey text. No screenshot test gate or new dependency is needed.

## Validation

The Release build succeeds. The 62 focused checks pass, and the final complete AppTests run passes all 192 tests. The complete Core suite passes 1,090 of 1,100 tests. Its ten failures are in the existing Muisir/Emerald notice expectations and Danaan redirect/travel fixtures; this UI change does not edit those runtime paths or tests. Named-pipe checks pass outside the restricted sandbox. NuGet vulnerability metadata remains unavailable (NU1900).

Native synthetic captures were manually reviewed at 1450 by 930 and 900 by 560. Saved rows collapse, expanded rows expose legends and account fields, and a new row opens with keyboard focus in its name field. List journeys, progress, XP feedback and action buttons fit inside their cards. Long text has deliberate ellipsis with complete tooltips; queue and history remain reachable at the minimum size. All captures and test reports remain under ignored artifacts. No game character or existing Excalibur process was operated.
