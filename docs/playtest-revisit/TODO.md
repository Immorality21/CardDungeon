# Revisit playtest: to-do (2026-10-06)

The third playtest, and the first of **revisits** (`docs/plans/REVISITS.md`). Work through it and delete this folder once it's clear. As with the earlier playtests, cite items in code comments as "revisit playtest finding N".

**Setup.** A subagent played as a casual gamer who knows a few roguelikes, from a fresh throwaway save (`savedata_playtest_revisit`; the real save was confirmed untouched). It was told only that heroes spend XP on the sphere grid and that the grid is what gets heroes through harder content. Its job was to play until a revisit was offered, then clear the **standard revisit** (Fear level 0, i.e. the +50% enemy health and damage base only), retrying as often as needed.

**Method caveat.** Fights were run through `CombatManager.Submit*` (the calls the pickers make) and doors were walked with `OnMouseDown`. Menus and hub screens were real buttons and keys, but as synthetic events, so no real keyboard was tested.

Full notes are in `playtest_revisit_log.md`; screenshots are in this folder.

## Status (worked through 2026-10-06)

Suite 1,336 / 0. Items marked *verified* were checked in play mode against a copy of the playtest save.

| # | finding | status |
|---|---|---|
| 1 | Revisit run-complete repeats unlocks | ✅ fixed: a revisit's screen names the Fear level and the bonus, plus the best Fear level; no "Now open" rows. A first clear of a revisitable run adds a "Revisit" row. *verified* |
| 2 | "Fear level" never defined, empty gap | ✅ fixed: the picker's explanation line defines Fear level and points at the Hall of Progression. The conditions now scroll, with the Fear level line pinned below them. *verified* |
| 3 | Entry screen only says "Fear level 0" | ✅ fixed: a summary line spells out every change and the reward bonus (`RevisitOps.Summary`). *verified* |
| 4 | Revisits invisible / no record | ✅ fixed: a cleared run says "This place cannot be revisited" or shows its best Fear level; the first-clear screen announces revisits. *verified* |
| 5 | Nothing links losing to the grid | ✅ fixed: the defeat screen gets a "Grow stronger" row with the banked XP and the Hall of Progression; the picker says the same. Defeat screen not seen live; built from the same row helper. |
| 6 | +50% can't be checked | ✅ fixed: the in-combat Inspect page gets a "Revisit" section (health +X%, with the floor's own number, and damage +Y%). The Bestiary already labels its numbers "Base stats". Not seen live. |
| 7 | Event heal reported as damage | ✅ fixed: `EffectEntry.Healed`, set by every heal path; the event report reads amounts, not the text. Covered by `RoomEventReportTests`. The tester's "Warrior −6" reading is not explained by the code (a heal cannot lower health); most likely a stale party panel. |
| 8 | Storehouse Esc closes the screen | ⚪ works as designed: equipping returns the cursor to the slots and the hint switches to "Esc back", so the next Esc leaves (FF-style). *verified*. The "sometimes stays in the list" case was most likely the tester's synthetic input. |
| 9 | Grid button stale "Activated" | ✅ fixed. *verified* (Activated → switch hero → dimmed "Activate") |
| 10 | Merchant bought the wrong row | ⚪ not a game bug: the highlighted row is bought and named ("Bought X"), and the cursor moves to the next row. The tester's driver picked a row number it had computed before the list shifted. |
| 11 | Grid framing / cursor / HP preview; event decline; Leather Caps; last-floor stairs | ✅ fixed. Framing reaches three steps past the frontier (a fresh hero sees the first fork); the cursor follows the grid's edges first; previews include gear; declining says the event stays; identical bag items stack ("×21"); the last floor's exit reads "The Way Out". *verified* except the decline text and the exit text |
| 12 | Fear level 0 trivial | ✅ owner's call: the base is now **+50% to every enemy stat** (speed, defences, Intelligence and spell power included), not health and damage alone (`BaseEnemyStatPercent`). Plus a **Quickened Foes** condition (+25% Agility ×2, 2 Fear level each) on top. Not yet replayed. |
| 13 | Health refills every floor | ⚪ by design (health is level-scoped, `Party.HealAll` on a fresh floor). Recorded, not changed. |
| 14 | Grid cost ramp | ⚪ balance observation; balance is paused. Feed into the next pass. |
| 15 | Early attrition decides runs | ⚪ balance observation; same. |

**Everything is done or recorded where it belongs.** Delete this folder after a replay of a Fear level 0 revisit confirms finding 12.

## The headline

**It cleared the Fear level 0 revisit of The Drowned March on the first try, and it was trivial.** Across about 30 fights no hero ever started below full health, and the boss Mirefather died before taking a turn. The player never failed, so it was never pushed toward the sphere grid. **Nothing in the game connects losing (or a revisit) to the grid.**

What it played:
- **The Threshold:** wiped once on floor 2 (two heroes; heals and potions ran out), cleared on the second attempt.
- **The Drowned March:** cleared first try; the Ranger and the Tinkerer joined.
- **The Fear level 0 revisit:** cleared first try with four heroes (Warrior 80 HP, Paladin 59, Ranger 51, Tinkerer 24) after spending most of its XP and buying gear.

## Priority order (proposed, not yet agreed with the owner)

1. Make the base revisit threatening (finding 12).
2. Fix the revisit's end screen (finding 1).
3. Point at the grid on a loss and on the revisit entry screen (findings 3, 5).
4. The other bugs (findings 7–10).
5. Everything else.

## Findings

### Revisit feature

1. **Bug — the revisit's Run Complete screen repeats unlocks.** It shows "The story map has new roads. Now open: The Ashen Deep / The Drowned Chapel", but both were already open. It says nothing about the Fear level or the reward bonus. (`pt_revisit_runcomplete_stale.png`) The fix is probably in `HubManager.MarkRunCompleted` / `CampaignOps.OpenedBy`: skip "Now open" for a run that was already completed, and show "Revisit cleared at Fear level N, +X% rewards" instead.
2. **"Fear level" is never defined.** The pips (`○○○ +1`) only make sense if you guess them. There is also an empty gap under "Revisit · choose the conditions": the blurb label is set to empty but still takes up space. (`pt_revisit_picker.png`)
3. **The entry screen only says "Revisit, Fear level 0".** It should remind the player of the +50% and the reward bonus. (`pt_revisit_entry.png`)
4. **Revisits are invisible until they appear.** The cleared Threshold never offers one and nothing says why (the tutorial exclusion is by design, but the player went looking). The story map doesn't record that a run was revisited, or at what Fear level (`GetBestRevisitFear` exists but is only shown inside the picker). (`pt_story_threshold_cleared.png`, `pt_story2.png`)
5. **Nothing connects difficulty to the grid.** Not the defeat screen, not the revisit picker, not the hub banner. The only nudge is the small gold dot on the Hall of Progression. (`pt_town_badge.png`, `pt_cur.png`) Suggestion: a defeat-screen line ("Your heroes banked X XP — spend it in the Hall of Progression"). This also ties into the Essence guide the owner wants (REVISITS.md, "Still to do").
6. **The +50% can't be checked.** The Bestiary shows the template number (Floating Eye health 20) while the revisit nameplate showed 60/60 (`pt_revisit_eye_60hp.png`). The 60 is correct: 40 from the floor's tuning × 1.5. The gap is the floor's own scaling, which the Bestiary never explains, so the player can't separate the two.

### Bugs elsewhere (from before this change)

7. **An event's text contradicts its effect.** In A Drowned Offering, "Kneel and say the words" → Success reads "Old wounds close over…" and then "Warrior takes 6 damage. / Paladin takes 6 damage." The actual change was Warrior 27 → 21 and Paladin 46 → 52. The story, the result lines and the HP change all disagree. (Drowned March floor 1; text in the log.)
8. **Esc in the Storehouse sometimes closes the whole screen.** After Enter equips an item, one Esc sometimes closed the Storehouse instead of stepping back to the slots as the hint says, and later keys landed on the town map. Seen twice. Other times the screen stays in the item list, where a stray Down+Enter re-equipped a Leather Cap over the Hood.
9. **The grid's button goes stale.** After activating a node and switching hero with Q/E, the detail column says "Select a node." but the button still reads "Activated". (`pt_bug_activated_label.png`)
10. **A Merchant purchase went to the wrong item.** After a purchase the list shifts and the selection follows the row position. The tester paid 22g meaning to buy a Wooden Shield, and no shield appeared.

### Clarity and UX elsewhere

11. Grid usability:
    - **It opens zoomed in on the trunk**, so the branches are off screen until you zoom out.
    - **The arrow-key cursor jumps unpredictably** ("Right" went to a far-left node).
    - **Node previews show base HP** ("Health 38 → 48") while the Storehouse shows 56 with gear: two different health numbers for the same hero.

    Smaller points:
    - **Declining an event leaves its Action button up**, so it still reads as unresolved.
    - **Six identical Leather Caps** clutter the equipment list (no stacking).
    - **The last floor's exit says "The stairs drop away into the dark below"** even though nothing lies below.

### Balance feel

12. **A Fear level 0 revisit is trivial for the party that just cleared the run.**
    - The Ranger, after the +4 and +5 Agility nodes and with crits, usually acted twice before enemies moved. Most fights logged zero enemy turns, and the boss never acted.
    - **The +50% scales health and damage but not speed**, so the danger never arrives.
    - Options: add Agility (or a turn-tempo factor) to the base; start the default revisit above Fear level 0; or scale the base with the party's progress.
    - **The rewards** (+25%, split four ways) felt modest but fine for no risk.
13. **Health refills at the start of every floor**, whether you press Continue or go back to town. Attrition only exists inside one floor, so going to town between floors only matters for spending XP.
14. **Grid costs ramp quickly** (20 → 30 → 45 → 65 → 90 → 120 → 155 → 195). The tester was often 1–2 XP short of the next node (63 vs 65, 119 vs 120). Each new ability costs 120 and sits three nodes deep.
15. **Early attrition decides runs.** The only wipe was The Threshold's floor 2, with two heroes and two heal charges.

## What told the player (or didn't) that the grid was the answer

- **What helped:**
  - the tutorial banner ("Every fight banked XP… click the glowing node");
  - the gold dot by the Hall;
  - inside the grid, glowing affordable nodes and "Need 30 XP — 10 banked" / "Activate a node next to it first".
- **What didn't:** after the wipe, nothing linked losing to the grid, and the revisit flow never mentions it. The tester spent XP out of habit, not because the game made the case. Because Fear level 0 was so easy, there was never an "I failed, so I need to invest" moment. **As built, the feature teaches nothing about the grid.**

## Grid purchases the tester made (for reference)

- **Warrior:** +3 STR, +6 HP, the +1 END/+6 HP hub, +3 STR, +2 AGI, Sunder (120), +10 HP, +2 LCK, +3 STR (155), +2 END.
- **Paladin:** +2 STR, +5 HP, the hub, +3 SPR, +3 SPR, Heal (120), +6 HP, +3 STR, +2 END, +3 STR.
- **Ranger** (arrived with 314 XP banked): the trunk nodes, +4 AGI, +4 LCK, Poison Dart (120), +5 AGI (155).
- **Tinkerer:** nothing.
- **Gear:** Steel Plate x2, Iron Sword, Ruby Amulet, Shadowweave Hood and Gloves, Warding Gauntlets, Wooden Shield, potion belt raised to 5.
- **Revisit rewards:** about 106, 89, 99 and 230 gold per floor ("31 for the clear" against 25 normally), +6 Essence per floor, 576 XP on the last floor.

## Leftovers from the test run

- The test save folder `savedata_playtest_revisit` (under the game's persistent data path) can be deleted.
- The playtest driver left a few harmless `PT_*` EditorPrefs keys.
