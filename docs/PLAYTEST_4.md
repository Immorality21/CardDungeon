# Playtest 4 — external playtester, 2026-11-03

Feedback from an outside playtester (the first one who is not the owner), sent 2026-11-03 after playing
from The Entrance to The Threshold and on to The Drowned March. Logged 2026-11-10. **All of it was played on
or before 2026-11-05**, so the build predates the 2026-11-08 summon change (once per level, until it falls),
the 2026-11-09 first-time hints and the fixes in the commits since; a few findings may already be answered.

Cite them in code comments as **"playtest 4 finding N"**, like the earlier playtests. Delete this file once
everything is shipped or moved into a plan (`docs/NEXT_STEPS.md` keeps the ledger).

**Kinds:** 🐞 bug · 🔎 clarity (the game does not explain itself) · 🎮 UX / controls · ⚖️ balance ·
🧭 design question · 💡 idea · ✅ already answered by the design.

**Status:** bugs are fixed first (this pass); everything else is looped over with the owner afterwards.

## Part 1

1. 🐞 **Ruby Amulet: no visible slot for it.** Got one, but no slot to equip it in.
2. 🧭 **Guarantee a bonfire (refuge) somewhere in every level.**
3. 🐞 **Bonfire refused to heal: "party already at full health"** with Warrior 49/62, Paladin 40/65, Mage 71/84. Happened once; other heals worked.
4. 🎮 **Click outside a window to close it.** Today only the X closes it.
5. 🧭 **Disappointed a summon is not permanent; the Paladin's feels useless.** (Their build predates "until it falls", 2026-11-08.)
6. 🔎 **The Warrior's summon needs tooltips** saying what each of its options does.
7. 🐞 **The Mage's default Ultra can use abilities that are out of charges.**
8. 🐞 **Items of one set have different rarities**: Bonewrought Mail is green, the greaves and helm are blue.
9. 🐞 **The defeat screen ("The Party Has Fallen") cannot be closed with Enter.**
10. 🔎 **Warren Tunnels: hard to tell decoration from interactive objects.**
11. 🔎 **How is an enemy's weakness found?**
12. 🐞 **Tavern: keyboard navigation stops after one press.**
13. 💡 **Hall of Knowledge should say where a monster is found**, especially for the tavern's quests.
14. 🐞 **A quest for a boss you cannot revisit**: "slay the Abyssal Warden", but the Threshold cannot be replayed.
15. 💡 **Doors everywhere in a dungeon** instead of plain corridors; doors read more clearly.
16. ⚖️ **The Paladin's heal becomes useless** once health bars grow.
17. 🔎 **No feedback when the Mage's passive saves an ability charge.**
18. 🎮 **Keep the camera centred on the leader while walking.** Walking down, the hero sits below centre and you can't see where you're going. (See also 55.)
19. 🔎 **How much does a bonfire heal?** Not said.
20. 🐞 **Does the Ranger's Ultra stop on a miss?** Blinded, he made one attack, missed, and the Ultra ended.
21. 🐞 **The Ranger had 37 health on joining** without ever being equipped in town. (To verify: probably his free starting nodes.)
22. 🔎 **The Tinkerer joined for good after the Drowned March**, out of nowhere. Where did she come from? Maybe missed in the boss room.
23. 🔎 **Summon upgrades: what does "+x to its effect" mean?**
24. ✅ **Ranger/Tinkerer start with some nodes unlocked — intended?** Yes: late heroes join with their trunk and third slot filled (2026-11-04). Nothing on screen says so.
25. 🧭 **The "enemies have more health" Fear modifier is worth less** when the revisit itself already adds health.
26. 🎮 **Targeting: a Back entry in the target list is unneeded** (there are hotkeys), and the bottom enemy can only be reached by scrolling through the whole list.
27. ⚖️ **Room events go harmless later**: what is −3 health with 100?
28. 🐞 **Tinkerer's Rivet Gun "+2 power" upgrade mentions healing**, for a damage-only ability.
29. 🐞 **Steel Plate and Bonewrought Mail have identical stats.**
30. 🧭 **"A save system is a must, I don't want to lose my progress."** (The game saves; to find out what they lost — possibly the web build's storage, or a save wiped by a new build.)

## Part 2 (in play order, The Entrance to The Threshold)

31. 🐞 **In a room, the action list fights free movement**: entering from the left or right, the arrow keys walk you out of the room instead of browsing the actions.
32. 🧭 **Does Flee make sense when you can just walk out?**
33. 🐞 **Fleeing an encounter seems to move the party twice as far.**
34. 🧭 **What does Inspect add?** It shows nothing at the start of the game. A bestiary would be better (they had not unlocked the Hall of Knowledge yet).
35. 🐞 **Inspect cannot be scrolled with the arrow keys.**
36. 🔎 **"Enter: use" in the dungeon — use what?**
37. 🎮 **Auto-interact with bonfires** (and the like) when you walk into them.
38. 💡 **Flavour text on everything**, e.g. a torch: "Helps us see the way."
39. ✅ **Ultra gauge kept between fights — intended?** Yes; since 2026-11-09 a first-time hint says so.
40. 🐞 **The tutorial still eats keyboard input.** Better: let the player act and say what's wrong ("buy the Strength node first") than silently ignore the keys. After the forced +3 Strength node the grid can be browsed freely, which makes the earlier lock more confusing.
41. 🔎 **Sphere grid: hovering "Strength 10 13" shows a tooltip that just says "Strength".**
42. 🔎 **The two numbers have no context** (before/after?). Will they go stale? An arrow between them, or colour (better / worse), would fix it.
43. 🔎 **Same in the Storehouse's "Stats if equipped"**: "HP 44 50". Suggests an arrow or Souls-style colours.
44. 💡 **A party / status screen.**
45. 🐞 **Sphere grid keyboard: after clicking an unlearned node, only the first key press registers.** (Maybe the tutorial lock.)
46. 🐞 **Same first-press-only problem in the inventory.**
47. 🎮 **Town hover outlines are rectangles** that don't match the building sprites.
48. 🎮 **The Story (campaign map) has no keyboard zoom or pan**; arrows do nothing.
49. 🎮 **Campfire: "Leads the party" on a hero not in the party** should add them and make them leader.
50. 🎮 **Potion belt: clicking "Empty" does nothing**; what the belt is isn't explained; swapping potions between slots works but nothing shows it can.
51. 🔎 **The belt is shared by the whole party**, but only the Merchant greying out every hero hints at it.
52. 🎮 **"Leads the party" reorders the hero list** — the UI shifts under the cursor.
53. 🎮 **Hero order is shown twice** (top row and the list).
54. 🎮 **Hovering a hero on the campfire shows a tooltip with just their class.** Adds nothing.
55. 🎮 **The view shifting between rooms is distracting**, especially with the mouse. (Conflicts with 18; to settle together.)
56. 🧭 **Dead ends**: two empty rooms, nothing found, backtrack. Wanted?
57. 🐞 **Fast arrow presses while walking: only the last one counts**, so "down right up left" quickly doesn't end in the same place. A nit for hardcore players.
58. 🔎 **Mouse walking in the dungeon is never mentioned.**
59. 🐞 **The Victory screen can't be closed with Space or Enter**; only a click.
60. 🐞 **After finishing the first dungeon with the Paladin, the town's keyboard navigation is broken.**
61. 🎮 **"All equipment" is listed under the "Equipped" header** as if it were a slot.
62. 🐞 **"The Paladin has XP to spend too. Spend it on either hero."** "Either hero" suggests the XP is shared; it isn't.
63. 🐞 **Can't flee from the Abyssal Warden.** (To verify: a boss refuses Flee by design?)
64. 🎮 **Stairs don't auto-interact** when you walk into them.
65. 🐞 **Merchant: the item description can't be scrolled with the keyboard.** And, like 42, before/after numbers without explanation.
66. 💡 **Music with leitmotifs**: themes for bosses, heroes and places, reused elsewhere to link them.

## Bug pass, 2026-11-10 (built, compile-checked, not yet run in Unity or played)

None of these were fixed by the commits of 2026-11-05 to 2026-11-09. New EditMode tests were written but
not run (no Unity in the session).

| # | verdict | what changed |
|---|---|---|
| 1 | fixed | The "All equipment" row (2026-11-02) pushed the Necklace row below the Storehouse slot column's clip edge; all five neck items were hidden. Slot rows 56px → 48px (`CardDungeon.uss`). |
| 3 | not found; guarded | No path found that reports full health wrongly. The numbers fit `ApplyRest` running twice. It now refuses a second run on a spent refuge and logs `[Refuge]` health per hero when a rest heals nobody. The prompt's missing health counts living heroes only. |
| 7 | by design | MultiCast is free and may cast from an empty slot (owner, 2026-10-31; Focus is the price). The list now says "free" next to "0/2" (`abilities.menu.meta-free`). |
| 8, 29 | fixed (data) | The Bonewrought pieces were copies of Steel Plate, Warden Helm and Ember Greaves. Whole set now Rare, item level 4 (Mail 75g → 175g), with its own stat mix: a little Strength. Mail STR 2/END 4/HP 14/20% shadow; Helm STR 1/END 5/HP 16; Greaves STR 2/END 3/HP 14. **Owner:** Helm and Greaves still carry the Warden Helm / Ember Greaves reactions. |
| 9, 59 | fixed | The Victory and defeat screens never took keyboard focus back from the target/ability list. Both now take focus when they open, and the combat UI documents use `PanelKeyboard.HoldFocus` (see 12). |
| 12, 45, 46, 60 | fixed | **One root cause:** in Unity 6 every arrow also sends a navigation event that moves focus to the document root after the key is handled (`StopPropagation` doesn't stop it), and a click on a non-focusable button blurs focus. First press worked, then keys went nowhere. New `PanelKeyboard.HoldFocus` + `KeepFocusIn` (hub, title, combat UIs). 60 also: the tutorial-locked town now answers arrows with "Not yet". |
| 14 | fixed | The Warden revisit quest could never be done (the Warden only guards unrepeatable runs). New `QuestOps.IsDoable`: board fill/reroll skip undoable boss quests and clear them from an existing save. `WardenRevisit` is no longer revisit-only and its text was rewritten. **Owner to confirm the content change.** |
| 17 | small fix | No grid node saves charges; a MultiCast from a slot with charges left now floats "Charge kept", like the Arcane Circlet. May not be what the tester meant. |
| 20 | fixed | The Ultra never stopped on a miss. A miss had no projectile and no delay, so two "Miss" words stacked at once and it read as over. Missed hits now fly their projectile and pause 0.3 s, without the status effect. |
| 21, 24 | expected | 37 = base 29 + two free trunk nodes (late heroes start stronger, 2026-11-04). |
| 26 | fixed (part) | Up from the first target now wraps to the last enemy (Back is past the bottom), and the list scrolls with the cursor. Back stays: it was added after the 2026-10-30 playtest. |
| 28 | fixed (text) | Every Power upgrade used "deals (or heals) X more". Now "gains X base power, added before its stat bonus". |
| 31, 32 | fixed (owner's call) | The arrow pointing back out of the room (since 2026-10-31) fought the Fight/Flee cursor. **The Flee button is gone** (owner, 2026-11-10): the bar is just Fight; fleeing is the arrow back through the entry door (any direction), or a click on the entry door or the room behind. Not in boss or side rooms. |
| 33 | fixed | The held arrow that fled kept walking on landing. Walking keys held at Flee are ignored until released. |
| 35 | fixed | Inspect scrolls with Up/Down and Page Up/Page Down. |
| 40 | fixed | The guided grid can be browsed; only buying is held to the guided node. A wrong buy, hero switch or Escape says "Not yet: activate X first."; elsewhere the banner shows "Not yet." for 2.5 s. |
| 57 | fixed | New pure `WalkInput`: taps during a step queue in order (up to 3). |
| 62 | fixed (text) | "The Paladin earned XP too, and it is the Paladin's own. Spend it on the Paladin's grid, or go back when you are ready." |
| 63 | by design | Boss rooms hide Flee and seal the doors. |
| 65 | fixed | Page Up/Page Down scroll the open detail column on every hub screen. No key hint on screen yet. |
| 22 | clarity | The Tinkerer is the Drowned March's run-clear reward; the level-clear window shows "{hero} joins the party for good" but no blurb, and the map never says a run brings a hero. Moves to the clarity loop. |

**To confirm with a real keyboard and mouse** (synthetic MCP keys skip the focus paths, which is how 12/45/46 shipped):
several arrows in a row in the grid, Storehouse, tavern, Hall of Knowledge and story map, after a click as
well; the town after the first dungeon; Enter/Space on Victory and Enter on defeat after a target-list pick;
Inspect scrolling; target list Up wrap with 4-5 enemies; a blinded Pack Hunt shows three arrows; Fight/Flee
entered from a side door; flee with a held up/down arrow stops on the doorway; quick down-right-up-left
ends where it started; all 8 Storehouse rows visible; the guided grid; Page Up/Down on a long merchant
text; clicks still work (campfire belt, tabs, scrollbars).

## Owner's decisions on the rest, 2026-11-10

| # | decision |
|---|---|
| 41, 42, 43, 65 | **Show only the change**, coloured: "Strength +3", "HP −6" (sphere grid, Storehouse, Merchant). |
| 19 | The refuge prompt says how much it heals. |
| 36 | The walking hint names what Enter uses ("Enter: rest at the bonfire"). |
| 22, 24 | Show the hero's blurb when a level clear brings them; "Clearing it: X joins" on the story map; say a late hero joins with part of their grid learned. |
| 6, 23 | A description for each summon action; summon upgrade texts say what grows instead of "+x to its effect". |
| 10 | Interactive objects stand out from decoration (glow/outline). |
| 58 (+ all hints) | **A Controls screen** listing every key and mouse action, easy to reach in the dungeon and in town, instead of one-off hints. |
| 11, 50, 51 | Not now. **2026-11-13:** 11 works as intended (weaknesses are found by hitting, as the Inspect page shows); 50 and 51 go to the potion belt revisit, `POLISH_CONTENT.md` §18c. |
| 4 | Click outside a hub window closes it. |
| 48 | Story map: keyboard pan and zoom. |
| 47 | Town buildings highlight and click by their sprite's shape. |
| 61 | "All equipment" must look clearly distinct from the equipment slots. |
| 49, 52, 53, 54, 50 | Campfire: "Leads the party" adds a hero who isn't in it; the leader is marked, not moved; the order is shown once; no class-only tooltip; an "Empty" belt slot opens the picker. |
| 18, 55 | Camera follows the leader smoothly, no jump at a room change. |
| 37, 64 | Stepping onto stairs or a bonfire opens its prompt at once (resting stays a choice). |
| 27 | Room events' health damage/heals become % of max health, at about today's value where each is tuned. |
| 16 | Paladin heal: rely on its grid upgrades; check in the balance pass. **2026-11-13:** there was only one (+2); two more Holy Touch Power nodes added (+4, +6), `BALANCE_OPEN.md` step 8. |
| 2 | No guaranteed refuge; scarcity stays. |
| 56 | Dead ends should usually hold something: the room-kind planner prefers them for caches, events, refuges. |
| 25 | Show the combined Fear total (e.g. "enemy health +80%") rather than only the modifier's number. |
| 34 | Inspect stays and always shows what the party can see plus what the bestiary knows. |
| 30 | Ignore. |
| 13, 15, 44, 66 | Backlog: monster locations in the Hall of Knowledge, doors everywhere, a status screen, leitmotifs. **15 built 2026-11-13**: the way south from a room shows its archway door before the room below is seen (`DUNGEON_ROOMS.md`). |
| 38 | Flavour text on decorations: not taken; irrelevant for now (2026-11-13). |
| 5 | Summons: answered by the 2026-11-08 change (once per level, until it falls); the Paladin's summon is judged in that playtest. **2026-11-13:** the owner expects this settles it. |

## Decisions built, 2026-11-11

Everything in the table above is built. **EditMode suite: 1,907 passed, 0 failed** (balance included), run in
Unity. Seen in the sandbox: the Fight bar with no Flee button and the hint (a boss room now says only
"Enter fight"), and the Controls screen from the pause menu. Nothing else has been seen in play.

**For the owner to review (drafts and choices):**
- **Campfire:** the four seat tiles are gone, and only the roster list stays (it includes benched heroes), so the order of seats 2-4 is no longer shown anywhere. Clicking any belt slot opens a potion picker (pick, swap, or Empty).
- **Camera:** only the area around the leader is kept clear of panels, so a big room's far edge can sit under the HUD. Tuning constants live in `GameManager` (dead zone 0.5, look-ahead 1.5).
- **Room events:** 15 events moved to % of max health. The typical-health figures are estimates. **Synced to the Event Workbook 2026-11-11** (a dated line in each event's notes; the workbook's own texts carry no numbers):

  | event | floors (est. avg max HP) | old flat | new % |
  |---|---|---|---|
  | Choked Passage | Threshold (~35) | 6 dmg | 15% |
  | Musty Tome | Sealed Archive (~50) | 4 dmg | 8% |
  | Turning Arm | Sealed Archive (~50) | 7 dmg | 15% |
  | Rigged Plate | Warrens (~50) | 6 dmg | 12% |
  | Treasury Hoard (small) | Counting Room (~50) | 4 dmg, whole party | 8% |
  | Choked Passage (Galleries) | Catacombs (~60) | 6 dmg | 10% |
  | Reed Cage | Drowned March (~70) | 6 dmg, whole party | 8% |
  | Chained Bellows | Ashen Deep (~85) | 8 fire | 10% |
  | Cooling Crucible | Ashen Deep (~85) | 8 fire | 10% |
  | Key of the Flame Pit | Ashen Deep (~85) | 10 fire, whole party | 12% |
  | Drowned Font | Drowned Chapel (~85) | heal 20 | 25% |
  | Knife Rack | Blood Stair (~100) | 8 dmg | 8% |
  | Red Basin | Blood Stair (~100) | heal 25 | 25% |
  | Treasury Hoard | Hollow Vault (~100) | 4 dmg, whole party | 4% |
  | Last Parishioner | Drowned Chapel | heal 999 | 100% |
- **Text drafts:** summon action lines ("A plain strike at one enemy", "Sends it home", "Back N turns after use", "Once per summoning"); summon upgrade lines ("{summon} hits N harder", "...has more health: +N% of the summoner's"); Inspect's "learn more" line; "Joins with part of their grid already learned."
- **Stat changes:** better is green where the Storehouse used to show gold; unchanged stats are left out. The Merchant's belt upgrades ("2 → 3 slots") and the Forge's level preview keep their arrows.
- **Town hitboxes:** Read/Write switched on for the 8 building textures in `Assets/Sprites/Hub`.
- **Controls:** F1 in town and on the title screen, a button in the town bar and title Options, and a button beside Hints in the pause menu (no F1 in the dungeon, where walking reads the keys directly).
- **Dead ends:** caches and refuges are placed in dead ends first, and events try dead ends first. Placement changes per seed, so a floor saved mid-way before this change may resume with things moved.

**Seen on screen 2026-11-13** (a copy of the owner's save, scripted presses, so not the keyboard path): campfire roster,
Leader tag and belt picker work, but an empty band sits above the three columns where the seat tiles were. Storehouse
stat chips drew over the row captions in the All list: fixed (captions cut with "…"), though the cut often hides "Worn by
X". Merchant: the "If X wore it" changes sit below the detail column's fold. Sphere grid: "Health +6" in green. Controls
screen reads well. Em dashes removed from all 19 UI texts (owner: none on screen), guarded by `LocFollowUpTests`.

## Grouping

- **Bugs (this pass):** 1, 3, 7, 8, 9, 12, 14, 20, 21, 28, 29, 31, 33, 35, 40, 45, 46, 57, 59, 60, 62, 63, 65.
- **Clarity:** 6, 10, 11, 17, 19, 22, 23, 24, 36, 41, 42, 43, 51, 58.
- **UX / controls:** 4, 18, 26, 37, 47, 48, 49, 50, 52, 53, 54, 55, 61, 64.
- **Balance:** 16, 27. **Design questions:** 2, 5, 25, 30, 32, 34, 56. **Ideas:** 13, 15, 38, 44, 66.
