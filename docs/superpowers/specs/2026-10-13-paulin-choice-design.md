# Legend-first rewards and Paulin queue choice

Approved by the user on 2026-10-13.

Every reward line with an evidenced legend starts with its icon and name. Preserve chance, class, and alternative reward conditions. Keep catalog evidence unchanged; this is presentation only.

Adding either Paulin quest opens a dark comparison dialog before changing the queue. Cured a sick child requires five White Blossoms (Sevti), costs 25,000 gold, grants 750,000 observed XP and a public mark. Aided the Pact of Anaman requires five Golden Blossoms (Tulsi), costs no gold, grants 25,000 observed XP and a hidden brown mark. The observed Anaman XP takes precedence over the older guide. Completing one permanently excludes the other; partial delivery progress does not.

Require the unchecked acknowledgement “I understand completing one locks out the other.” Cancel is the default. When the other quest is queued, name it explicitly and offer Replace or Cancel. Replacement retains its queue position and other quests; no journal is erased. Revalidate the character, queue and eligibility after confirmation. Missing confirmation UI must not accept silently. Runtime queue edits reject the mutually exclusive pair, including direct control edits; removal of an old invalid pair remains possible.

Plan: add focused formatting and queue tests; implement presentation, confirmation and guard; run both full xUnit projects; build separately and inspect the dialog and reward lines with isolated sample data. Include the approved native-pixel sprite change in this verification. Raw game file packaging is not part of this change.
