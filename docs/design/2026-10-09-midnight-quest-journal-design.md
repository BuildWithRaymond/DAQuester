# Midnight quest journal

Approved by the user on October 9, 2026: redesign the whole native Windows app in dark mode, with a polished player-facing interface and optional developer tools.

## Visual direction

The product is a Dark Ages quest companion for players managing connected characters. Its primary job is to make the next quest action and saved progress clear. Use a midnight journal aesthetic: ink `#0D131E`, navy surface `#151E2B`, raised surface `#1C2838`, ivory `#EDF0F3`, muted slate `#A0AEC0`, and antique gold `#D9B879`. Mint and rose distinguish positive and blocked states with text labels. Use Georgia for restrained quest display headings, Segoe UI for controls and body copy, and Consolas only inside developer tools.

The signature is a fine-line celestial compass on the selected quest panel. No stock dashboard metrics, artificial progress percentages, or decorative imagery that obscures controls. Generous spacing, clear dividers, subtle rounding, and coherent dark native chrome carry the rest.

The user's follow-up requires every window surface to stay dark. Use custom dark title bars, selectors and dropdowns, checkboxes, number steppers, scrollbar drawing, input frames, and an in-app client file picker. Retain standard selection, keyboard input, range checks, window dragging, sizing, minimize/maximize, and close behavior. The screenshot smoke test rejects any solid light 8×8 surface in the rendered app.

## Main screen

A compact left navigation rail holds Quest journal, Quest library, Saved login, Settings, Developer mode, and an always-accessible Stop all action. A header explains the companion and offers Open Dark Ages. A compact roster shows character, connection, level, gold, readable progress, and next attempt. The selected row controls the quest detail and actions.

The lower workspace pairs a selected quest panel with the character's current journey. The quest panel offers the four existing executable quests, plain descriptions, prerequisites, rewards, the actual ordered quest stages, repeat policy, and Start/Pause/Resume/Stop. The journey shows real saved state, item counts, a map name when known, schedule, actual reward results, and NPC conversation without protocol identifiers. Disconnected characters get explicit launch and manual sign-in guidance. Pending or blocked work retains the runner's actual explanation.

## Developer mode

Off by default and remembered in a separate presentation preference. Reveal raw diagnostics, detailed NPC metadata, calibration review, automatic calibration, replay export, recording switch, and raw connection profile editing. Hiding developer mode must not stop recording, disconnect characters, cancel automation, or discard state. Normal settings expose a client path picker and character list without requiring JSON editing. Endpoint changes still require a DAQuester restart.

## Secondary surfaces

Use the shared theme for the quest library, birth choices, saved login, settings, developer calibration, and errors. The library supports search and clearly distinguishes the four available automatic quests from guide-only entries. Birth choices retain explicit season/town selection, integer age 12–40, saved choices, and the existing lasting-choice warning. Saved passwords retain Windows encryption and manual login. Use actionable button labels and readable wrapping.

## Architecture and verification

Keep the quest runner, control bridge, journal, proxy, protocol, and recipe behavior unchanged. Separate theme and reusable drawing controls from screen composition. Preserve existing quest selector and birth-control names. Test meaningful presentation behavior using the app's isolated --smoke path, which never starts proxies or the control bridge. Verify developer visibility, quest selection and repeat policies, real journal selection, birth gating, secondary screens, and screenshots at standard and compact window sizes. Run existing automated tests and publish a separate x86 release without replacing the running application's files. Do not operate Excalibur.

## Implementation sequence

1. Extend the smoke harness and observe the old interface fail the dark-mode requirement.
2. Add reusable theme, controls, and the player-focused journal workspace.
3. Theme and simplify all dialogs; add searchable library and structured settings.
4. Build, run smoke and existing tests, inspect screenshots, and correct observed layout issues.
5. Publish the verified release and provide its path and restart guidance.

Reviewed: no placeholders; player and developer surfaces are explicit; schedules and completion are based on actual saved evidence. This folder is not a Git checkout, so no design commit is possible.
