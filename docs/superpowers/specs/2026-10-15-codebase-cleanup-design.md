# Behavior-preserving codebase cleanup

Approved scope: make the repository easier to navigate and read without changing
runtime behavior, public names, persisted formats, quest rules, or the player UI.

## Audit and approach

The audit covered authored application, Core, game integration, test, tool, build,
content, and documentation files. Existing action/evidence pairs, observed-journey
partials, test names, and persistence boundaries already communicate useful
responsibilities. No source file was proven redundant. Vendor sources, native
source pins, evidence captures, and dated research retain their distinct roles.

The chosen approach uses existing namespaces and partial-class conventions.
Broad folder renames would create churn; comments alone would leave mixed
responsibilities in the largest files. New comments explain boundaries and
constraints rather than narrating individual statements.

## Source changes

1. Replace Core's mixed `Contracts.cs` with files for game contracts, recipe
   definitions, calibration, durable quest jobs, and execution context. Keep
   declarations, constructor order, defaults, and serialization names intact.
2. Extract desktop lifecycle and character-memory work from `DesktopRuntime`.
   Give shared commands and desktop result types files matching their names.
   Group birth, Emerald, and flower choices in existing dialog partials.
3. Extract command dispatch, interaction, navigation, travel notices, and walking
   from `GameSession`. Preserve guards, method bodies, field initialization order,
   generation checks, and movement/checkpoint sequencing.
4. Expand dense declarations, scheduler updates, and compact handlers in touched
   code. Move the shared synthetic session to a discoverable test support file.
5. Give content tools clear entry points and document their inputs, outputs, and
   dependencies. Add a current contributor source map and link existing guides.

## Boundaries

Keep existing uncommitted work. Do not operate real characters or interact with
Excalibur processes or working copies. Preserve four quest policies, all receipt
and evidence requirements, pending-transaction reconciliation, timed waits,
terminal-popup cleanup, ownership, cancellation, and update/install gates.
Do not reformat upstream code, alter protocol schemas, change package contents,
or remove historical evidence because a static reference search finds no caller.

## Implementation and verification

Core, desktop, and game-integration edits can run independently. Integrate before
building. Compare moved declarations and method bodies against a snapshot of the
starting working tree; public contracts and executable tokens should remain the
same except for partial-class declarations and documented local naming changes.
Check tool outputs against isolated synthetic inputs, never local game files.

Run focused contract/persistence/scheduler, desktop-choice/lifecycle, and
movement/dialogue tests, then both complete xUnit projects. Check documentation
links, Python syntax, whitespace, and the final diff. Interface behavior and
packaging are outside this cleanup, so screenshot and packaging gates do not
apply unless implementation expands that scope.

## Validation results

- Release solution build passed with zero errors. NuGet reported `NU1900`
  because its vulnerability-data feed was unavailable.
- Complete `DAQuester.Tests`: **1,487 passed**. Complete `DAQuester.AppTests`:
  **322 passed**. Both final runs used normal Windows permissions; the sandbox
  denied access to two synthetic current-user named-pipe checks on the first
  complete Core run.
- Focused desktop tests: **57 passed**. Focused Core tests: **190 passed**, with
  one existing movement timing test failing during overlapping runs. That test
  passed in isolation and in both complete Core runs. Its fixture and movement
  code match the starting working tree; no runtime change was made for it.
- Roslyn comparison of **5,855 declaration/member entries** found no code-token
  changes after normalizing partial declarations and equivalent grouped local/
  field declarations. DesktopRuntime and GameSession instance initializers keep
  their original order.
- All three edited Python tools produced identical JSON and console output on
  isolated synthetic inputs. All four Python tools parse. Repository Markdown
  file links resolve, and `git diff --check` passes.
- Content, native pins/patch, build metadata, workflows, and XAML retain the
  starting working-tree contents. Existing uncommitted work was preserved.
