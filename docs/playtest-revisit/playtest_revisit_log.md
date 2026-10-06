# Playtest log: revisits (casual player)
Started 2026-10-06

## Setup
- Fresh save folder savedata_playtest_revisit (did not exist before). Override set after play-mode entry, MenuScene reloaded; title said "New Game".
- Combat/walking automated via a driver: door clicks via OnMouseDown (documented route), Fight via fight-btn submit, hero actions via CombatManager.Submit* (the calls the pickers make). Heuristic: focus weakest enemy, heal <45%, abilities in big fights (3+ enemies / boss / lots of HP), summons on boss/3+.

## Run 1: The Threshold (tutorial), level 1 "Dungeon Entrance"
- New Game drops straight into floor 1 with a Warrior (26 HP). HUD: "The Threshold · Level 1 of 4".
- Fight 1: "An unknown foe" -> won, +20 XP. Paladin freed as captive ("yours for good once this level is cleared").
- Fight 2: Floating Eye -> won. Antidote Salve loot. Explored 5 rooms.
- Level clear: +35 gold (10 found+25 clear), +5 essence, +40 XP (Warrior +30, Paladin +10), Rotted Timber x1. Only "Back to Town" (tutorial). "Next: Upper Halls - continue it from the story map."
- Difficulty 1/5.
## Town 1
- Tutorial banner: "You came home with timber. Click the Hall of Progression's foundation to raise it." Built it (1 Rotted Timber). Entered: Sphere Grid, Warrior 30 XP / Paladin 10 XP.
- Banner "Click the glowing node - you can afford it." -> +3 STR (20 XP) on Warrior. Next node +6 HP costs 30 XP ("Need 30 XP - 10 banked"). Clear enough.
- Story map: The Threshold in progress (Level 2 of 4), others locked; detail lists floors and "Clearing it opens -> Drowned March, Warrens, Blood Stair". Entry screen: "Party locked until the run ends."

## Run 1 cont: Threshold L2 "Upper Halls" -- WIPE (attempt 1)
- 6 fights. Fight 4 (Floating Eye x2 + Drakeling) burnt both potions and Paladin's Holy Touch. Fight 5 Stone Sentinel + Eye left us 3/26 and 7/25. Pushed into fight 6 (Eye x2 + Drakeling) -> wiped. (My auto-walker pushed on at low HP; a cautious player might have looked for rest. No refuge seen.)
- Defeat screen: "The Party Has Fallen. Somewhere in Upper Halls, the last of them goes down. The run ends here. Gold for reaching level 2 +20 / Everything banked before this floor kept / Gold found on this floor lost / XP, items and materials from this floor lost / Return to town".
- NOTHING on the defeat screen or in town points at the sphere grid. Town after wipe: no badge on the Hall (only 10 XP each banked anyway).
- Floor felt 3/5 — the attrition across 6 fights with 2 heroes is real; each 3-enemy room costs ~half the party HP.
## Run 2 (Threshold retry)
- Story map after wipe: Threshold back to "Open · 4 floors" — restart from floor 1 (Paladin kept).
- L1 again: 2 fights (Floating Eye x1 each), trivial. Level clear now offers "Back to Town" / "Continue" ("continue now, or rest in town first").
- Went to town: tiny gold dot badge beside "Hall of Progression" label (pt_town_badge.png). Subtle but noticed it.
- Grid: Warrior +6 HP (30). Paladin +2 STR (20). Merchant needs 10 Scrap Iron + 1 timber; Bestiary 4 Scrap Iron; Campfire Lv2 300 gold.
- BUG (cosmetic): after activating a node and switching hero with E, the right panel says "Select a node." but the button still reads "Activated" (pt_bug_activated_label.png).
- Grid cursor: first Up on Paladin jumped past the affordable STR to the HP behind it ("Activate a node next to it first.") — had to press Down. Slight friction.
- L2 Upper Halls cleared (2nd attempt): 6 fights + event "A Choked Passage" (END, risky — failed, 2 dmg each) + "The Treasury" (took safe coin) + cache + refuge (rest = 35% HP + refill charges, once). Used floor map fast travel back to the refuge — map is nice ("still waiting: 1 refuge").
- Clear: +121 gold, +5 essence, +216 XP (108 each), 4 Scrap Iron, 3 Cut Stone, 2 timber, Leather Cap x3, Oak Staff, Steel Plate. Difficulty 3/5 with rest; ended Warrior 13/32.
- Observation: "Back to Town" between floors seems to fully heal (entry screen showed full HP after L1). Makes "continue now" a strictly worse choice except for time?
## Town 3 (after Threshold L2)
- Grid view only shows the bottom of the tree; had to zoom out (scroll) to see two branches: left = HP/END/resist/"R", right = STR/AGI/LCK, ✦ ability nodes deeper (Warrior's right branch: Sunder, 120 XP), "M" = +1 ability slot (65).
- Costs ramp fast: 20, 30, 45, 65, 90, 120. With 108 XP Warrior could buy only the 45 hub; next nodes all 65+ (2 XP short of the STR node). Feels grindy but readable.
- Keyboard cursor on the grid is spatial and hard to predict ("Right" from the hub went to the far-left +10 HP once). Clicking nodes is fine.
- Bought: Warrior +1 END/+6 HP (45) -> 63 left. Paladin +5 HP start node (30) + hub +1 END/+5 HP (45) -> 43 left.
- Gear: Warrior Leather Cap (+6 HP) + Steel Plate (+12 HP +4 END). Paladin Leather Cap. Warrior now 56 HP, Paladin 41.
- L3 Collapsed Caverns: 4 fights (Stone Sentinels, Eye), cache (Iron Sword), declined Choked Passage (declining leaves the Action button up — event stays 'available'). +93 gold, +132 XP. 2/5.
- Town 4: Warrior +3 STR (65; 64 left - 1 short of a second 65 node). Paladin +3 SPR (65; 44 left). Paladin's tree shows ability nodes Shield Up / Sunder / Heal at 120 each, 3 nodes deep. Equipped Iron Sword (W), Oak Staff + 2nd Steel Plate (P). W 56 HP, P 53 HP. Grid node 'Health 38 -> 48' ignores gear while inventory says 56 — two different HP numbers.
- L4 Sunken Depths: Drakeling, Sentinel+Eye, BOSS Abyssal Warden + Eye (no Flee) — won comfortably, Cinder Imp. Run Complete: +102 gold, +236 XP, 3 Void Shard, Ruby Amulet, Simple Sword. 2/5. Exit confirm still says 'The Way Down ... stairs drop away into the dark below' on the final floor (nit). THRESHOLD CLEARED (attempt 2).
- Run Complete screen: "Every floor cleared. The story map has new roads. Now open: Drowned March, Warrens, Blood Stair".
- Story map: Threshold ✓ "Cleared · 4 floors" — NO Revisit option yet on the cleared run (pt_story_threshold_cleared.png). Nothing says when/how revisits appear.
- Town 5: Warrior +2 AGI (90) toward Sunder (120), 92 left. Paladin +3 SPR (90) toward Heal (120), 72 left. Ruby Amulet on Warrior. 406 gold, 5 Scrap Iron (merchant needs 10).

## Run 3: The Drowned March (5 floors)
- DM L1 Silt Shallows: 6 fights (Eyes, Drakeling, Bog Shaman+Eye x2), cache.
- BUG?: event "A Drowned Offering" — chose "Kneel and say the words (SPR · likely)" -> "Success. The warmth comes up out of the bowl and goes through all of you. Old wounds close over..." then "Warrior takes 6 damage. / Paladin takes 6 damage." Party panel before: W 27, P 46; next fight start: W 21, P 52. So the text says heal, the lines say damage to both, and the numbers moved W -6 / P +6. Something is inconsistent.
- DM L1 clear: +100 gold, +180 XP. Town: Warrior bought SUNDER (120), Paladin bought HEAL (120). Storehouse auto-carried both into the free 2nd slot: "2 charges · joins on the next floor". Nice touch. Holy Touch charges do NOT refill between floors (1 of 2 left) — only refuges.
- DM L2 The Reedcage: 3 fights (Bog Shaman, Drakeling, Hex Weaver), freed RANGER (joins). +73 gold, +140 XP. 2/5. Pressed Continue straight to L3.
- DM L3 Weeping Causeway: 5 fights, cache (Warding Gauntlets). +118 gold, +160 XP. 2/5. NOTE: party starts every floor at full HP, whether I Continue or go to town.
- Town 6: BUILT MERCHANT (11 scrap). 697 gold: bought Shadowweave Hood (175), Wooden Shield (22), belt 2->3->4->5 (50+75+100). 375 gold left.
- Grid: Warrior +10 HP (65) + LCK (90). Paladin +6 HP + 3 STR. Ranger arrives with 314 XP banked: start/trunk nodes + AGI + LCK (95+155) -> 64 left, Poison Dart (120) next.
- Equipment UX: 6 duplicate Leather Caps clutter the Head list; the list cursor opens on a random-ish row (not the best item); after equipping, cursor stays in the item list so a follow-up Down+Enter re-equips junk (I accidentally swapped the Hood back to a Leather Cap). Once, Escape in the storehouse dumped focus to the town and my next keys landed on the town map.
- Warrior 80 HP / Paladin ~59 / Ranger 37+6.
- DM L4 Rotwater Deep: 5 fights, skipped 'A Sealed Tomb'. +78 gold +178 XP. 2/5. Continue.
- DM L5 Mire Throne: 9 fights incl BOSS Mirefather + Eye (easy, Aimed Shot/Slash). Freed Tinkerer. Run Complete: +205 gold, +520 XP (W188 P166 R166). DROWNED MARCH CLEARED first try. Floors 2/5.

## REVISIT unlocked
- After clearing the Drowned March the story map says "Cleared - can be run again · 5 floors" and the button reads "Revisit…". The Threshold (also cleared) has NO Revisit and no explanation why (pt_story2.png). As a player I first looked for it on the Threshold after run 1 and found nothing — no hint revisits exist or when they unlock.
- Picker (pt_revisit_picker.png): header "Revisit · choose the conditions"; "Every revisit: enemies +50% health, +50% damage."; Conditions: Hardened Foes ○○○ +1 (+25% HP), Sharpened Blades ○○○ +1 (+25% dmg), Swarming Halls ○○ +2 (+1 foe per fight, up to five), No Respite ○○ +2 (50% less healing incl. refuges). Footer "Heat 0 · rewards +25% XP, gold and Essence". Button "Begin revisit".
  - Understood the +50% line immediately. "Heat" is never defined; I inferred pips = levels and +N = heat per level. Reward line at heat 0 is clear. The empty gap under the subtitle looks like a missing line.
  - Nothing on the picker (or anywhere) says "you'll want a stronger party / visit the Hall of Progression".

## Town 7 — prepping for the revisit
- Hub after the run: XP W279 / P235 / R285 / Tinkerer 0 (new, 24 HP).
- Grid: Warrior +3 STR (155) + 2 END (90). Paladin +2 END (90) + 3 STR (90). Ranger Poison Dart (120) + 5 AGI (155). Costs now 90-155 per node.
- Merchant: restock (25), Shadowweave Gloves (175) on Ranger. Tried to buy a Wooden Shield after the list shifted — 22g spent but no shield appeared (probably bought a Leather Cap: the list moves up after a purchase and the selection follows the row index).
- Storehouse: after Enter equips an item, a single Esc sometimes closes the WHOLE storehouse (hint says "Esc back to the slots"); my next keys then landed on the town. Reproduced twice.
- Campfire: 4 of 4 marching (W leads), "Each hero earns 25% of the XP / A wider party is safer; a narrower one levels faster."
- Party into revisit: Warrior 80 HP, Paladin 59, Ranger ~51, Tinkerer 24 (0 investment).
## REVISIT attempt 1 — Drowned March, heat 0
- Entry screen: 'Level 1 of 5 · Revisit, heat 0' (pt_revisit_entry.png). No reminder of the +50% / reward. Party W80 P59 R51 T24.
- RV L1 Silt Shallows: 6 fights, took ZERO damage. +106 gold (31 for clear), +6 essence, +200 XP. Felt 1/5 — nothing 'spongey' noticed at all. Continue.
- RV L2 Reedcage: 3 fights, zero damage. +89 gold, +176 XP. 1/5.
- RV L3 Weeping Causeway (verbose): 6 fights, ZERO enemy turns logged in most fights — Ranger (AGI 22, crits) acts twice before enemies move; Bog Shaman dies to ~79 dmg in 3 hits. Revisit at heat 0 is a cakewalk for this party.
- RV L3/L4: all fights trivially won, no damage taken. L4 +99 gold +233 XP. 1/5.
- RV L5 Mire Throne: BOSS Mirefather + Eye died in 6 hero actions (Slash x2, Lightning Bolt, Ranger 20/38/24) — the boss never got a turn. +230 gold, +6 essence, +576 XP, 2 Void Shard, 8 Mire Hide.
- REVISIT CLEARED ON ATTEMPT 1, no wipes, ~31 fights, party never dropped below full HP in a single fight log (W80/P59/R51/T24 at every fight start; no damage visible). Rewards vs first clear of L5: 230 vs 205 gold, 576 vs 520 XP — "+25%" but with 4 heroes splitting it.
- Revisit Run Complete screen repeats 'Every floor cleared. The story map has new roads. Now open: The Ashen Deep / The Drowned Chapel' — those were already open; no mention of heat or the revisit bonus (pt_revisit_runcomplete_stale.png).
- Sanity check (2nd revisit start, then left): revisit L1 nameplate 'Floating Eye 60/60' while the Bestiary lists Floating Eye 'Health 20'. Can't tell from the UI how much is floor scaling vs the +50%; nothing in-game shows the base vs revisit number side by side.

## Wrap-up
- Console: 0 errors / 0 warnings at the end.
- Override cleared, play mode exited (editor left on MenuScene, not dirty; it was on HubScene before). Real savedata listing identical before/after (realsave_before.txt vs realsave_after.txt).
- Totals: 3 runs started (Threshold x2 — 1 wipe on L2 —, Drowned March x1), revisit x1 cleared first try (+1 aborted sanity-check start). ~76 fights.
