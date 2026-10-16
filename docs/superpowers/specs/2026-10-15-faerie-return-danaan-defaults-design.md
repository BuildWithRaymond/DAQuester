# Faerie return boundary and fixed Danaan route

The player requested two concrete behavior changes: finish the Faerie visit on
safe return to Shinewood Forest 1 or a verified town escape, and remove the
Cap/Band questions because Cail is the only implemented option. This request
supersedes the earlier requirement for a separate Cail/rite popup.

## Faerie completion

Use Shinewood Forest 1 (map 542) as the walking return boundary. Preserve actual
pet/refusal and spell receipts. Reach the documented destination, leave the
actual landing on a validated free non-trigger tile, and record the return there.
Town songs and dachaidh retain fresh authoritative arrival checks and the same
landing departure. Saved uncertain escapes reconcile without another submission.
The route validator needs the path through Shinewood 1, not a further Mileth trip.

Continuing to Mileth adds travel the player does not want. Completing at the
Faerie before escape would lose the safety boundary. The requested early safe
return is the selected approach.

## Danaan defaults

Desktop manual/queue and scheduler starts use the existing Cail route with the
supplied-Mold rite accepted, without opening a choice dialog or showing Change
choices for this family. Persist those options for each started character through
the existing journal path. Keep existing non-null options and locked progress
guarded; unknown routes, missing Mold, pending transactions and final popup
cleanup keep their current behavior. Preserve explicit control/MCP validation
for caller-supplied options and all unrelated quest prompts.

## Verification

Add failing regressions for Shinewood 1 completion without Mileth navigation,
safe landing departure, pending escape reconciliation, and Danaan starts without
choice UI. Exercise scheduler readiness and saved-option preservation. Run focused
tests, both complete xUnit projects and a Release build in isolated outputs.
Review affected UI with synthetic sample data. Do not operate game characters,
modify player Data, or restart Excalibur.
