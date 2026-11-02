# Needs a playtest

Built, and checked by the agent through the Unity MCP (scripted presses, screenshots, the simulator),
but **not yet tried by a human with a real mouse and keyboard**. Delete an item once it has been
played (owner, 2026-11-02: no ticking off); anything that turns out wrong becomes a backlog item in `NEXT_STEPS.md`.
Most of it came out of playtest 3 (2026-10-26 to 2026-10-28); the first section is the outside
playtest's fixes (2026-10-30).

## Playtest notes, round of 2026-11-02 (built, not seen in play)

- [ ] **The quest board** - three quests drawn from a pool of nine (conditions: runs cleared), Reroll an untaken one for 100 gold (R), a collected quest goes back into the pool and the slot refills. New kinds: 3 wipes, 5 floors, the Warden on a revisit. Are the rewards right, and is 100 gold a fair reroll? Seen through scripted presses only.
- [ ] **The tavern and its first quest** - offered after the Sealed Archive (4 Scrap Iron, 2 Cut Stone). Accept "Rattling in the Catacombs", kill 5 Skeletons, collect: the Bone Scepter recipe and +50 XP on every hero, benched too. Second quest "The Dead Do Not Rest": 15 undead of any kind (a Skeleton counts for both), 1 Void Shard and +120 XP each. The flow was checked through scripted presses on a copy of a bot save; the kills were raised by hand, not fought. Are the rewards worth the trip?
- [ ] **Learning an ability asks** - buy Smite on the Paladin: a prompt names it and offers Carry it (a free slot) or Replace <ability> (all slots full), or Not now. His heal is never pushed out unasked. Keyboard: arrows + Enter, Esc = Not now. Not asked during the tutorial's guided node.
- [ ] **Storehouse All equipment** - the cursor starts on it; see the Hub section.
- [ ] **Floating Eye** - Eye Laser is a red beam straight to the hero, no bolt; Screech swells the eye and rings race out, then every hero is hit at the same moment.
- [ ] **Stone Sentinel's Slam** - a fist icon instead of Slash; may lower Endurance 1 for 2 turns (60%) instead of a bleed.
- [ ] **The Forge** - now 3 Cut Stone + 3 Scrap Iron, both found in the Threshold and the Archive (was Ember Iron + Slag Coal, which only the Ashen Deep has).

## The test boss: the Barrow King (2026-11-02)

- [ ] **The Barrow Throne** - optional, after the Catacombs. Does each phase read? The phase banners (The Crown Cracks, Last Rite), the named wind-ups (Grave Tide, Requiem) and the skeletons at 60%.
- [ ] **The guard** - at 60% he calls 3 Skeletons and a Bone Golem and cannot be targeted until they fall ("Shielded"). Does it read?
- [ ] **Its difficulty** - now after the Catacombs and a wall: a party that came straight there wipes about 95% of the time (simulator). Does it read as "come back stronger"?
- [ ] **The floor** - empty rooms to the throne for now. The door to the side room was not seen.

## Texts, icons and the Warlock (2026-11-01)

- [ ] **The new Warlock** - idle, wince, fall and walk at the Paladin's proportions. Demon Form too. Your call on the look.
- [ ] **New icons** - Smite, Divine Protection, the three cloaks, Sacred Shield, Ward, Mesmerize, Hamstring, Twin Strike, Earthsplitter, the Flamethrower Mech.
- [ ] **Grid node lines** - abilities, summons and Ultras by name, resistances saying Physical.

## Crafting as the gold sink (2026-11-01)

- [ ] **The Forge line in town** - after the first found recipe, one line on the town screen, gone once the Forge is opened. Not seen on
      screen yet: only its rule is tested.
- [ ] **The 14 late recipes** - does a late run feel worth chasing, are their prices fair, and do the agent-written texts read right?

## Outside playtest fixes (2026-10-30)

Synthetic key events reached every handler below; what is not known is whether a **real** key press
does (focus), so press them for real.

- [ ] **Keyboard the tester reported broken, not reproduced:** Enter on the story map, arrows in the
      town, Right in the Storehouse (now: next tab, All), the revisit condition picker. If one still fails
      with a real key, it is focus, not the handler.
- [ ] **Z / X / C** - Z confirms and X backs out everywhere Enter / Escape do; C opens and closes the
      dungeon pause menu.
- [X] **Run complete / level entry** - Enter works without a Tab first.
- [X] **Doors** - in a room like the tester's (party by a side door, another door in the bottom wall),
      Down picks the bottom-wall door.
- [ ] **Tutorial** - on a forced step the cursor starts on the button the tutorial wants.
- [X] **Sphere grid cursor** - never stuck on a node near the start.
- [ ] **Hover tips** - stats, the campfire's Combat numbers, resistances; and the tip following the hub's
      keyboard cursor.
- [ ] **Campfire hero status** - stats with meanings, ATK / CRIT / DODGE / P.DEF / M.DEF, the Momentum and
      Focus lines.
- [X] **Ability text** - "18 damage" in combat; "18 damage, scales with Strength" in the Storehouse.
- [X] **Target picker footer** - highlighting an enemy shows what it attacks with and known weaknesses.
- [X] **A killing projectile** - the enemy's HP plate stays until the projectile lands.
- [X] **Ability sounds** - Hamstring and other weapon abilities swing; every ability hit makes an impact
      sound.
- [ ] **Revisit ranks** - rank I / II / III read apart (gold / orange / red with a badge).
- [ ] **Floating Eye** - Eye Laser on one hero, Screech on the party.
- [ ] **Empty loadout** - putting away a hero's last ability leaves them with none, in and after a run.
- [ ] **Storehouse All equipment** - no longer a tab (owner, 2026-11-02): the first row of the Equipment tab's slot column, where the cursor starts. Every item listed, Enter equips / reassigns / takes off and the cursor stays in the list.
- [ ] **Catacombs** - the first wipe there (after the tutorial) adds the replay hint to the death screen.

## Hub and menus

- [ ] **Crafting at the Forge** (2026-10-29) - the Craft tab lists the Forge's three basic recipes plus
      any found; have/need per input, gold on the right; Forge spends them and the item lands in the
      Storehouse. Forging from a *worn* base puts the result on that hero. A recipe drop on a kill or a
      floor clear (Emberfall, Chapel of Knives), and that it never drops again once known. The town's
      gold pip on the Forge for newly found recipes.

## Art

- [X] **Hero redraws** - the Warrior (new design), Ranger and Rogue at the Paladin's head size.
- [X] **Stairs and chests** per level theme; the Wight faces the party.
- [X] **Every ability has its own icon** (131). Weaker ones to judge: the three elemental cloaks, Aegis vs
      Sacred Shield, Watcher Gaze vs Mesmerize, Ward, Mug, Hamstring vs Maim, Twin Strike
      (`PIXEL_ART.md`).

## The owner's to-dos

- [ ] **Boss finisher names** are placeholders: Bog Surge, Abyssal Tide, Death Nova, Acid Deluge, Coin
      Storm, Crushing Sweep. Rename them in `Assets/ScriptableObjects/Enemies/Behaviors/` -
      BehaviorMirefather, BehaviorAbyssalWarden, BehaviorLich, BehaviorMotherOoze,
      BehaviorGildedHoarder and PresetBoss. Each has the name twice in its Actions list (the wind-up
      and the hit); change both.
- [ ] **The Hero Workbook** still says "Heal -> Renew" for the Paladin's healer branch; the game's copy
      now says "Holy Touch -> Renew".

## Room events in every biome (added 2026-10-31)

- **Choked Passage: crashed on WebGL (itch.io).** Clean in the editor. Copy the browser console (F12) when it happens again.
- **Door of Fire on Emberfall:** can a party beat the Prince (308 health) and two Elementals (73 each)? Is the chest worth the key?
- **Walking into opened rooms:** every event that opens a room now takes the party straight in. Does that read well?
- **Wall objects:** do the Sluice and the Door read as part of the wall, at any spot on it?
- **Weighing Scale at 120 gold:** usually affordable when the party reaches it?
- **Sewerwalker on the Rogue:** does it feel like a rogue set?
- **Event density:** Reed Cage and Crucible at 25% per room on small floors. Too many events a floor?

## The battle screen (2026-11-01)

- A fight on every biome: do the units read as standing on the floor? Is 27% the right height for the band?
- The command menu's two columns with the keyboard: Up/Down through the list, Left/Right across.
- A party-replacing summon, a squad and an ally column on the new floor.
