# Ciad greeting and research supply continuation

The player requested cancellation of the unrelated Ciad greeting during Spare a
Stick and other automatic quests, followed by continuation. The player also
reported Wizardy Researcher buying Exkuranum and closing its client.

Read-only diagnostics show scheduler-owned clients closing after paused quest
attempts. They do not establish a native client crash. The restricted observed
evidence is [retained separately](../../../content/evidence/datester-7.41/ciad-greeting-research-shop.json).

## Greeting cancellation

Extend the existing recognized-notice path with the captured greeting's exact
map, text and pursuit-page shape. Close its current page without selecting Next
or an offered choice. Bind cancellation to the current generation and revision;
continue only after observed closure. Unknown pages remain guarded. An explicitly
bound notice-close command must not have its page automatically dismissed during
movement settlement and then reject its own now-stale dialog.

This uses the existing travel and quest-action notice handling so the fix applies
across automatic quests. Changing scheduler logout policy would mask the actual
interruption and is outside this fix.

## Merchant continuation

The existing transport dismisses merchant windows locally while retaining their
last authoritative server page. Research preparation must accept a successful
local merchant dismissal and continue from that exact known page to the research
room. It must not wait for the pursuit-specific server-close receipt. A different
or uncertain page remains guarded.

Keep inventory and quoted-payment proof separate from presentation cleanup.
Reconcile the saved verified purchase on Resume without another purchase.
Research pursuit pages retain their existing server-close requirements. Avoid
clearing or fabricating authoritative server state to acknowledge a local action.

## Validation

Reproduce both failures synthetically before implementation. Cover real local
transport notice cancellation, ordinary and fast travel, preservation of unknown
pages, the merchant page remaining in the authoritative snapshot, return to the
research flow, and recovery without repeated payment. Run focused regressions
and both complete xUnit projects. Build to a fresh isolated output; keep running
clients, player Data and Excalibur untouched.
