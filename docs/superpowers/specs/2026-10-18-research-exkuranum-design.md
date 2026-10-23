# Research Exkuranum preparation and healing

Approved by the player on October 18, 2026. The player clarified that health is
directly visible; use actual own HP and maximum HP to verify full health.

Before each research visit, count Exkuranum across the known inventory. If fewer
than 30 are carried, leave the research trigger safely, travel to the recorded
Blackstar Village Research shop (map 3214), buy only the missing quantity, and
return to Mileth research. The player identified Blackstar Research as the shop;
its recorded route and the reference source's interior approach at (9,7) are code
facts. Resolve the actual merchant and Exkuranum offer from fresh state. No NPC
identity, price, purchase receipt, or new live outcome is invented.

The existing research runner owns this behavior. A separate preparation task
would require queue handoffs; a general potion service would broaden the change.
Keep purchase and healing checkpoints in research progress, independently of a
pending research submission. Save each baseline before sending. Verify purchase
with both exact item increase and quoted gold debit. A timeout or reconnect does
not authorize another purchase or dose.

Require known positive maximum HP before research. Use one Exkuranum promptly
when research lowers health, including while awaiting a result, and verify one
potion consumed and HP restored to maximum before proceeding. Retain an observed
research result status before a potion message can replace it. Preserve original
research resource and dialogue proof and never infer an outcome from health.
Persistent zero health keeps the existing bounded health reconciliation behavior.

Retain health obligations and pending dose receipts through result recovery,
pause, reconnect and cooldown. Unknown results remain pending even after healing.
Full health is an observed fact; an absent resource receipt blocks further work
without guessing whether an action committed.

Validation uses synthetic sessions only: exact deficit and split stacks, fresh
shop quote and receipts, delayed damage, healing before further dialogue,
retained result evidence, partial receipts, timeouts, reconnect and archived
result recovery. Run focused tests, both complete xUnit projects and an app build.
No packaging change, game character operation, Data change or Excalibur operation
is part of implementation or tests.
