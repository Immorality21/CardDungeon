# Needs a playtest

Built, and checked by the agent through the Unity MCP (scripted presses, screenshots, the simulator),
but **not yet tried by a human with a real mouse and keyboard**. Delete an item once it has been
played (owner, 2026-11-02: no ticking off); anything that turns out wrong becomes a backlog item in `NEXT_STEPS.md`.
Most of it came out of playtest 3 (2026-10-26 to 2026-10-28).

## Phone playtest: saves, export/import, interface size (2026-11-15)

Only the editor has seen these; the WebGL build and a real phone have not.

- [ ] **The save survives a new build on itch** - play a bit in the browser, let CI push a new build, reload: the save is still there. (The first build with this change starts empty once: older saves sat in a folder named after the old URL.)
- [ ] **The save survives closing the tab** right after a fight or a level clear.
- [ ] **Export / Import** (title > Options > Save data) on a phone: Export downloads a file, Import opens the file picker, Replace restarts the game with that save. Also PC -> phone and back.
- [ ] **Interface size** - a phone starts at 130%. Readable? Buttons easy to hit? Try 150%. Dense screens (inventory, sphere grid, forge) shrink to fit the screen; are any of them too small to use?
- [ ] **The web page** - fills the screen, no zoom or scrolling, "turn your phone sideways" in portrait.

## Summons: once per level, until they fall (2026-11-08)

- [ ] **Once per level** - every summon one use per level; the old +1 charge / +1 turn nodes are now power, damage and health nodes. Does a summon feel worth saving for the right fight?
- [ ] **Until it falls** - party-replacers and allies stay the whole fight. Too strong (a summon soloing fights), or right?
- [ ] **Signatures come back** every 3 of the summon's turns ("★ Quake (2)" while waiting). Seen in the sandbox with the Golem.
- [ ] **The new moves** - Boulder Toss, Wings of Mercy, Dawnward, Dread Howl, Smoke Step, Flame Mantle + Claw, Life Kiss, Pack Howl, Unmake, Feed. Only Boulder Toss was seen in play. Names, icons and numbers are drafts.

## First-time hints (2026-11-09)

- [ ] **The six lines** - charges/health carry over, a refuge is one rest, a summon once per level, the Ultra gauge
      is kept, cleared levels can be replayed (story map), the party is full (town). Each once per save, the first
      time it matters (`docs/TUTORIAL.md`). Right moments, right words? All drafts. Seen in play: charges and summon;
      the refuge, Ultra, replay and full-party lines only by their tests.
- [ ] **Hints in the pause menu** - every line seen, oldest first. Does it read well? The town has no menu to show it in.

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

## The owner's to-dos

- [ ] **Boss finisher names** are placeholders: Bog Surge, Abyssal Tide, Death Nova, Acid Deluge, Coin
      Storm, Crushing Sweep. Rename them in `Assets/ScriptableObjects/Enemies/Behaviors/` -
      BehaviorMirefather, BehaviorAbyssalWarden, BehaviorLich, BehaviorMotherOoze,
      BehaviorGildedHoarder and PresetBoss. Each has the name twice in its Actions list (the wind-up
      and the hit); change both.
- [ ] **The Hero Workbook** still says "Heal -> Renew" for the Paladin's healer branch; the game's copy
      now says "Holy Touch -> Renew".
