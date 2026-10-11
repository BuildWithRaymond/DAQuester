# Incremental maintainability cleanup

Approved scope: simplify existing code and release work in small, verified batches.
Keep public APIs, JSON formats, quest policies, observed-evidence boundaries,
connection generations, durable checkpoints, and update consent compatible.

## Changes

1. Share the repeated atomic JSON checkpoint write used by journals, queues, and
   character memory. Keep each store's identity checks, normalization, locks,
   recovery rules, and failure messages. Cover locked-file retries, cancellation,
   and original-checkpoint preservation before changing the implementation.
2. Use conventional C# layout in the code being changed. Document contributor
   navigation and focused verification without adding a new framework.
3. Remove tests only where a retained test protects the same behavior. Keep
   protocol vectors, real timing boundaries, ownership races, and crash recovery.
4. Simplify repeated release compilation and validation only where evidence
   shows equivalent checks remain. Keep final publication-boundary verification,
   required notices and old-updater compatibility.

The user subsequently approved removing excessive UI tests. Remove automated
render matrices, pixel/layout assertions, the headless dependency and `Test-UI`.
Keep nonvisual app regressions in `DAQuester.AppTests`, and retain only an optional
four-page screenshot tool for documentation. Retired `--smoke` must fail before
accessing real user data.

## Verification

Measure the existing suite before pruning. Run focused checks after each batch,
then both complete test projects. Packaging edits also
require `tools/Build-Release.ps1` and `tools/Verify-Release.ps1`.

Existing login changes remain separate. Use synthetic fixtures only; do not
touch Excalibur working copies or control real characters.
