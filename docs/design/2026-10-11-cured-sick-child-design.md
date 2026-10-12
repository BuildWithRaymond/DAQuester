# Cured a Sick Child in Loures

Give Paulin five Sevti Blossoms, one per conversation, to earn the legend mark
"Cured a sick child in Loures". Every fact below was observed live on 2026-10-11
on valadriel (quester) and jackharkness (helper), unless it is listed under Open
questions. The catalog lists level 11+ and a total of 750,000 experience, which
matches five turn-ins of 150,000.

## Pact of Anaman lockout

A character can earn only one of "Cured a sick child in Loures" and "Aided the
Pact of Anaman" (confirmed by the player). Pact of Anaman gives Paulin five Tulsi
Blossoms instead; his menu offers Nila, Sevti and Tulsi. The character's
selection restriction (CharacterMemory) names the blocking legend mark for both
quest ids, the same place level gates live. The library
marks either quest Blocked, with that reason, for a character whose own legend
has the other mark, so it can't be queued. Preflight checks the same thing before
any command, so a queued run stops cleanly if the other mark appears. Only the
legend mark blocks; partial progress on either quest, including DAQuester's own
turn-in receipts, never does. Known limit: the server shows no blossom count, so
a character partway through the other quest by hand isn't blocked. The
choice dialog states the lockout as a fact: "This mark locks out Aided the Pact
of Anaman." before Confirm.

## Choosing how to get the flowers

The daily limit is on picking, per character. Paulin accepted two flowers from
valadriel on the same day, the second picked by jackharkness and traded to her.
So the player chooses between two ways of getting flowers, in the shared dark
choice dialog used by Mundane Birth and Emerald Dawn.

The dialog has one field and two cards:

- **Flowers already given to Paulin** (0 to 4). Prefilled from the journal. The
  server shows no counter, so a player who turned some in by hand sets it here.
  It stays editable until a DAQuester turn-in is pending.
- **One flower a day.** Only the quester picks. One flower per day, so the card
  states how many days remain ("Finishes in 3 more days") and the gold cost.
  DAQuester schedules the next run 24 hours after the last pick, the way Perfect
  Hairstyle schedules its weekly attempt.
- **Bring flowers from my other characters.** The card lists the player's saved
  characters, except the quester, as checkboxes. Each row shows its login state:
  Online, Saved login, No saved login (disabled), or Busy with another quest
  (disabled). Pick status is unknown until a character tries, so by default a
  row says nothing about picking. A second line appears only from DAQuester's own
  records: "Can pick again at 22:40 tomorrow" for 24 hours after a pick it made,
  or "Can't pick, refused at 22:54" after a refusal it has no pick time for (both
  disabled). The 24 hours follows the guide's one flower a day; the server's
  refusal message stays the authority. A counter says how many helpers are needed, which is the flowers
  remaining minus the quester's own pick if she can still pick today. At least
  one helper must be chosen. Choosing fewer than needed is allowed; the card then
  says how many flowers are left for later days. On each later day the run picks
  again with the quester and every chosen helper whose 24 hours are up.
- **Add characters.** When eligible helpers are fewer than needed, an "Add
  characters" link under the list closes the dialog and opens the Characters
  page; nothing is lost because choices save only on Confirm. It covers both
  missing alts and alts saved without a password. With no eligible helper at
  all, the helpers card is disabled and reads "No other characters can log in.
  Add them on the Characters page." with the same link.

Both cards show the gold check: "Each flower costs 5,000 gold at Paulin.
valadriel has 88,596." Confirm is disabled when the quester can't afford the
flowers planned for this run. **Change choices** works as it does for Emerald
Dawn, except while a trade or turn-in is pending.

## Picking (any character)

Walk to Mehadi Heart West (map 3077), stand on (3,9), then step onto (2,9).
Stepping on the tile opens a popup with no NPC name: "You see a flower here."
(Next), then "You see a white flower, which smells like lemon." (Next), then a
menu: "Pick the flower" / "Leave it be". Choose "Pick the flower" by its text.
Proof is a 0x0F inventory add of "White Blossom"; the pick sends no server
message. A character that can't pick receives the active message (0x0A type 3)
"You notice a small white flower that has been picked". That character stops
picking; the journey and the helper's row say, for example, "jackharkness can't
pick a flower right now." Record each pick and refusal time per character. A
character counts as able to pick again 24 hours after its last recorded pick;
DAQuester doesn't try to predict the server beyond that, and a refusal always
wins over the estimate. Skip picking when the character already carries a White Blossom.

The popup also opens when a character logs in standing on (2,9), and movement
gets no answer while it is open. Answer or close a popup before walking, and step
off (2,9) after picking.

The swamp on the way (Shifting Swamp, Mehadi) can stun: "A fume rises and you
suddenly feel sleepy" and "Stunned" for about 18 seconds, then "You can move
again." Walking must wait out the stun instead of failing on corrected steps.

## Trading (helpers mode)

The quester waits in Loures Cyril Corridor (map 3070) at (4,4). A helper walks
to an adjacent free tile; (3,4) worked. Trades run one at a time per quester, and
each traded flower is turned in before the next trade starts.

Observed sequence, with the helper starting the trade:

1. Helper sends 0x4A BeginExchange and AddItem in the same moment: target is the
   quester's entity id, slot is the White Blossom's slot, checked by name first.
2. Server to helper: 0x42 Started (quester id and name), 0x10 removes the slot,
   0x42 ItemAdded party 0 "White Blossom". Server to quester: 0x42 Started
   (helper id and name), ItemAdded party 1 "White Blossom". No quantity prompt.
3. Helper sends Accept. Both sides receive 0x42 Accepted with "You exchanged.",
   party 0 for the one who clicked and 1 for the other.
4. Quester sends Accept only after seeing exactly one item, White Blossom, from
   party 1 and no gold. She receives 0x0F White Blossom, then Accepted party 0.

"You exchanged." arrives after the first Accept too, so it never proves the trade
finished. The item also leaves the helper's inventory at placement. Completion is
the quester holding both Accepted events for this trade plus the 0x0F White
Blossom, all on the same generation. Anything else offered, a different partner,
a timeout or a disconnect cancels the trade.

## Turning in (quester)

Click Paulin (map 3070, at (2,2), found by name), standing at (4,4). The server
sends "He looks forlorn" as the conversation opens. Menu: "The sick child, how is
she?". Next menu: "Offer Sevti Blossom". Page: "I must collect this blossom for
the chief physician..." (Next). Menu: always "Give Sevti Blossom and 5000 coins";
the plain "Give Sevti Blossom" hands it to the physician instead of the child.
Final page: "I gave it to her and bade her eat it..." then close. Never choose
"Perhaps for 1000 coins you can say"; it costs 1,000 gold and does nothing for
the quest.

Save a pending checkpoint before the final choice. Proof, on the same generation
and after that checkpoint: gold down exactly 5,000, the active message
"150000 experience!", the White Blossom removed, then "He smiles". Record the
experience number from the message rather than assuming 150,000. Offering with no
flower gives the active message "You don't have a Sevti Blossom". An ambiguous
final choice is never resent; reconnect reconciles gold, inventory and messages.

After the fifth turn-in, inspect the own legend for "Cured a sick child in
Loures" and complete the quest. The fifth turn-in also sends the active message
"A sick child recovers because of you" (live 2026-10-12). An existing mark at preflight completes without
commands.

## Journal

Save the mode, chosen helpers and prior count in the quester's journal before any
command. Save one receipt per turn-in (time, generation, gold before and after,
experience text) and per pick refusal (character, time). Progress is prior count
plus receipts. Helpers keep no quest journal of their own; their state lives in
the quester's run, and a helper is owned by that run while it picks and trades.
Helpers that DAQuester logs in use the saved-login launch queue, so their login
typing takes turns. Each helper DAQuester logged in for this run is logged out as
soon as its flower is traded, or as soon as it can't pick. A helper that was
already online before the run stays online.

## Open questions

1. What exact popup pages does a character that already picked see before the
   "has been picked" message? Captured only partly, after a login on the tile.
2. Can one character carry two White Blossoms? The design avoids it by turning in
   each flower before the next trade.
3. Does a cancelled trade return the item to the helper (0x0F)? How far apart can
   the two characters stand?
4. The exact legend text, and what Paulin says after the fifth flower.
5. Does the experience reward change with level? Both turn-ins were at level 99;
   the catalog total suggests a fixed 150,000.
6. How safe the Mehadi walk is for low-level helpers (the quest itself is 11+).
7. How should a helper log out: close its game client, or send the in-game log
   out first? Measure which one leaves no lingering character.
