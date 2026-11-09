# Needs a playtest

Built, and checked by the agent through the Unity MCP (scripted presses, screenshots, the simulator),
but **not yet tried by a human with a real mouse and keyboard**. Delete an item once it has been
played (owner, 2026-11-02: no ticking off); anything that turns out wrong becomes a backlog item in `NEXT_STEPS.md`.
Most of it came out of playtest 3 (2026-10-26 to 2026-10-28).

## Summons: once per level, until they fall (2026-11-08)

- [ ] **Once per level** - every summon one use per level; the old +1 charge / +1 turn nodes are now power, damage and health nodes. Does a summon feel worth saving for the right fight?
- [ ] **Until it falls** - party-replacers and allies stay the whole fight. Too strong (a summon soloing fights), or right?
- [ ] **Signatures come back** every 3 of the summon's turns ("★ Quake (2)" while waiting). Seen in the sandbox with the Golem.
- [ ] **The new moves** - Boulder Toss, Wings of Mercy, Dawnward, Dread Howl, Smoke Step, Flame Mantle + Claw, Life Kiss, Pack Howl, Unmake, Feed. Only Boulder Toss was seen in play. Names, icons and numbers are drafts.
- [ ] **The rename** - run is "level" and level is "floor" on every screen. Anything still saying the old word?

## Playtest notes, round of 2026-11-02 (built, not seen in play)

- [ ] **Late heroes start stronger** (2026-11-04) - the Ranger, Rogue and Tinkerer join with their trunk and third ability slot already filled (115 XP of nodes, free). Do they hold their own on arrival?
- [ ] **Summon prices** (2026-11-04) - every summon node now asks Void Shard plus Cut Stone, Scrap Iron, Rotted Timber or Hex Silk (all found by the Catacombs); the Mage's three now cost materials too. Its +1 charge node keeps the late material. Affordable when you reach it?
- [ ] **Stone Sentinel's Slam** - a fist icon instead of Slash; may lower Endurance 1 for 2 turns (60%) instead of a bleed.
- [ ] **The Forge** - now 3 Cut Stone + 3 Scrap Iron, both found in the Threshold and the Archive (was Ember Iron + Slag Coal, which only the Ashen Deep has).

## Texts, icons and the Warlock (2026-11-01)

- [ ] **The new Warlock** - idle, wince, fall and walk at the Paladin's proportions. Demon Form too. Your call on the look.

## Crafting as the gold sink (2026-11-01)

- [ ] **The Forge line in town** - after the first found recipe, one line on the town screen, gone once the Forge is opened. Not seen on
      screen yet: only its rule is tested.
- [ ] **The 14 late recipes** - does a late run feel worth chasing, are their prices fair, and do the agent-written texts read right?

## Hub and menus

- [ ] **Crafting at the Forge** (2026-10-29) - the Craft tab lists the Forge's three basic recipes plus
      any found; have/need per input, gold on the right; Forge spends them and the item lands in the
      Storehouse. Forging from a *worn* base puts the result on that hero. A recipe drop on a kill or a
      floor clear (Emberfall, Chapel of Knives), and that it never drops again once known. The town's
      gold pip on the Forge for newly found recipes.

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
