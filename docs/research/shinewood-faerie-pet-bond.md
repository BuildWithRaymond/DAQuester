# Shinewood successful Pet Faerie bond

The 2026-10-14 owned run reached a branch not covered by the earlier pet-refusal
capture. After checking the connection, the Faerie said:

> You have a connection with a faerie. To make it bond with you, you must give up 250hp and 250mp.

Its Next led to `Are you willing?`, with fresh Yes and No choices. The runner
paused because the pending adoption allowed only named pet/spell selections.
The player's recorded Yes then reduced observed maximum HP from 4,200 to 3,950,
delivered one Pet Faerie, and closed dialogue on the server. No final success
text page was sent. The existing receipt logic subsequently verified acquisition
and the Rucesion Song return, and the queue moved on.

`content/evidence/datester-7.41/shinewood-faerie-pet-bond.json` retains seven
exact NPC pages, the original submissions and four owned projected receipts.
The actor and generation are normalized. Source revisions remain: Yes 5298,
maximum-HP change 5299, Pet Faerie 5301, server dialogue close 5303 and actual
Rucesion Threshold arrival 5339. Unrelated inventory, player data and packet
payloads are omitted. The quoted MP cost is observed dialogue; this projection
does not independently establish a maximum-MP change.

The automatic path now recognizes only that exact consecutive same-entity,
same-generation offer and consent question. It saves a distinct `faerie-bond`
submission before selecting its unique Yes. Actual item/equipment receipt still
proves success. Server closure completes dialogue; if the submitted consent
page remains, only that exact bound page can close using the new pet receipt.
Missing delivery, unknown terms, other popups and interrupted submissions retain
the checkpoint without another bond selection. The separate one-deoch refusal
and spell paths are unchanged.

Synthetic tests exercise the captured sequence, both closure timings, late
delivery, uncertain submission/close and stale, foreign or changed consent.
The captured Yes was player-selected; this evidence does not certify a fresh
uninterrupted automatic bond-choice run.
