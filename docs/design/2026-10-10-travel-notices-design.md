# Travel interruptions

During automated travel, dismiss the known town roleplay entry notice before
recovering rejected fast movement. An Undine weapon warning must close before
removing the observed weapon from equipment slot 1. Wait for a later server
equipment-removal observation before resynchronizing movement and continuing the
existing route. Do not equip it again automatically.
If dismissing the entry notice completes a map transfer, return to cross-map
planning instead of continuing the old map's walking coordinates.

The local Excalibur reference recognizes the prefixes `((You are entering a
role-playing` and `You can't bring weapons here!`. The roleplay packet is also
in the sanitized Perfect Hairstyle capture. Weapon tests use synthetic protocol
fixtures, not a captured warning. Restrict weapon handling to Undine's catalog
maps and popup dialogs with no choices or input. Preserve unrelated dialogs.

Unknown/full inventory or an unanswered removal must pause with an actionable
reason, without walking or resending removal automatically. Keep the removal
requirement in the current connection after closing the warning, so an explicit
Resume rechecks equipment instead of forgetting it. Manual interruption and generation
changes still cancel automation. Tests use a local synthetic server; no real
characters or Excalibur processes are operated.
