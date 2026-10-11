# Implementation plan

1. Establish the migration branch and shared design contract.
2. Extract the existing orchestration into DesktopRuntime without changing quest
   evidence or file formats; make character ownership explicit in async commands.
3. Build observable view models, validated account editing and shared Avalonia
   controls; implement all four pages and quest-choice dialogs.
4. Adapt synthetic UI coverage and rendering, then verify policy, launcher,
   ownership, saved data and update boundaries.
5. Inspect every rendered page/state, refine layout and scaling, update player
   documentation and release notes, package and verify older update compatibility.
6. Commit and push the migration branch and release tag; verify published assets.
