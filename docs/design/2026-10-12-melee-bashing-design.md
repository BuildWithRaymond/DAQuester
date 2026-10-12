# Melee bashing

DAQuester's melee fighter for combat quests is a port of Talos Redux's
`DynamicMeleeBashing` and `BashingBase`, with the logic unchanged and only the
packet and state plumbing rewritten for DAQuester.

## Scope (player decisions, 2026-10-12)

Ported: target choice and lock, chase, facing (confirmed vs requested facing,
resync and resend), unstuck, step-window skill timing, the refresh after five
seconds without experience, the rotation (gap closer, asgall gate, assails with
assail buffs, Rescue on tagged monsters, teleport-behind, density-gated area
skills, line skills, melee skills, follow-ups), the skill table and classifier,
`EnsureWeaponEquipped` with `WeaponRegistry`, and random waypoints.

Left out: crashers and HP-cost skills, sleep combos, protection, assist and
follow chains, waiting for curses, the Shinewood necklace swap, the trace log,
the druid-form step around a weapon swap, and the archer bow swap.

Changed on purpose: Talos reset its weapon attempt flag on every call and so
retried forever; here a second failed equip stops the fight. Random waypoints
never pick a warp tile.

## Plumbing

- State: 0x13 monster health, 0x29 kill and asgall-reflect animations, 0x3A own
  status icons, 0x3F skill cooldowns by base skill name, monster sprite, kind,
  facing and last step, ability level and experience to next level, and the
  replies "You can't use skills here.", "You can't attack." and the two
  equipment refusals. A decode failure of the four combat packets is skipped and
  never interrupts a quest.
- Skill names arrive as "Mass Strike 11 (Lev:100/100)"; the level is stripped
  with Talos's own regex before lookup.
- Commands: assail (0x13 plus one zero byte, as Talos sends it), turn (0x11), one
  pathfinding step (onto the tile unless something stands on it, as Talos's
  NavPathfind), refresh. Arbiter's `ClientUseSkillMessage` cannot serialize, so
  use-skill now sends `3E <slot>` directly.
- Defaults are Talos's: 500 ms monster step, 50 ms ping, 100 ms skill interval,
  assails on. Like Talos, a skill is resent until its server cooldown arrives.

## Live verification (Cerda, Druid, West Woodland 8-1, 2026-10-12)

Weapon swap from Staff of Clarity to Ancient Hy-brasyl Tonfa, targeting and
chase, Pounce 9 and Ambush openers, Raging Attack, Furious Bash, Mass Strike 11,
Double Rake 9, Mantis Kick, kicks and assails, server cooldowns, kills, and
roaming between fights. No quest uses it yet; the control bridge's `fight`
method starts it.
