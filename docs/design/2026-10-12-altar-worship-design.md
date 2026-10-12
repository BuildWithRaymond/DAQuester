# Altar Worship

Drop an item worth more than 500 gold on the Mileth Altar every 3 hours. Vorlof
reports that 10 offerings earn Priests "Mileth Altar Worshipper (#)" with a Jade or
Amethyst Ring and 100,000 experience; other classes get a Red Jade Ring and 50,000
experience. Facts below were observed live on 2026-10-12 on Ferda (Warrior) unless
listed under Open questions.

## The offering

- Torrance stands at (3,5) in the Mileth Armor Shop (map 133). His menu's "Buy"
  opens a shop list (menu type 4, pursuit 74). Buying sends that pursuit with the
  item name as the only argument; each request buys one Leather Shield for 750.
  The list is shown again after every purchase.
- Ferda dropped one shield from (33,53) onto (31,52) and one onto (31,53). Each
  drop brought "25 experience!" and "You feel a touch of light around you", both
  type 3, and the shield left the bag.
- The second drop came five seconds after the first and got the same reply. The
  server doesn't refuse early offerings, and its reply doesn't say whether an
  offering counted.

## Decisions (player, 2026-10-12)

- The item is always a Leather Shield from Torrance. One already in the bag is
  offered first; otherwise the run needs 750 gold.
- Offerings are spaced 3 hours apart, as Vorlof's tester did, even though the
  server allows sooner.
- The quest stops after the 10th offering.
- Every class may run it for the experience. The details carry a "Legend mark for
  Priests only" callout, which turns red and names the class for anyone else; the
  library row starts with the class for non-Priests; and starting it on a
  non-Priest shows a one-time dialog ("Continue for experience"). The choice is
  saved on the character's journal, so the 3-hour runs don't ask again.

## Run

One action loops until the 10th offering: if the next offering isn't due, wait
(status Scheduled, with the time in the reason); otherwise buy if needed, walk to
(33,53) with a tolerance of 1, check the altar is within 3 tiles, save a pending
"altar-offer" with the bag counts, drop, and prove the offering from the altar's
reply plus one shield gone. A drop the altar never takes clears the pending entry
and stops with "Nothing was offered". After a reconnect, a pending offering is
settled from the bag alone. The class is read from the profile and remembered in
the character memory so offline rows can warn too.

## Live status

Live-tested on 2026-10-12 (Ferda, Warrior): the one-time warning, buying from
Torrance, the walk, offering 1 and the 3-hour schedule. A full 10-offering run
has not been live-tested.

## Open questions

- The reply to the 10th offering. The run waits briefly for a new ring, then reads
  the legend; with no mark (Priest) or no new ring (others) it stops and asks for
  the replay instead of guessing.
- Whether early offerings count toward the 10.
