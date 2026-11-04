# Needs a playtest

Built, and checked by the agent through the Unity MCP (scripted presses, screenshots, the simulator),
but **not yet tried by a human with a real mouse and keyboard**. Delete an item once it has been
played (owner, 2026-11-02: no ticking off); anything that turns out wrong becomes a backlog item in `NEXT_STEPS.md`.
Most of it came out of playtest 3 (2026-10-26 to 2026-10-28); the first section is the outside
playtest's fixes (2026-10-30).

## Playtest notes, round of 2026-11-02 (built, not seen in play)

- [ ] **Ifrit's parting Hellfire** (2026-11-04) - when he leaves (dismissed, fallen or out of turns) he stays put, the name floats over him, he erupts into flame (new PixelLab frames), Hellfire lands, then he fades and the party steps back in. A column of fire rises under every enemy as it hits. Readable now? Seen in the sandbox through screenshots only.
- [ ] **Late heroes start stronger** (2026-11-04) - the Ranger, Rogue and Tinkerer join with their trunk and third ability slot already filled (115 XP of nodes, free). Do they hold their own on arrival?
- [ ] **Summon prices** (2026-11-04) - every summon node now asks Void Shard plus Cut Stone, Scrap Iron, Rotted Timber or Hex Silk (all found by the Catacombs); the Mage's three now cost materials too. Its +1 charge node keeps the late material. Affordable when you reach it?
- [ ] **Stone Sentinel's Slam** - a fist icon instead of Slash; may lower Endurance 1 for 2 turns (60%) instead of a bleed.
- [ ] **The Forge** - now 3 Cut Stone + 3 Scrap Iron, both found in the Threshold and the Archive (was Ember Iron + Slag Coal, which only the Ashen Deep has).

## The test boss: the Barrow King (2026-11-02)

- [ ] **The Barrow Throne** - optional, after the Catacombs. Does each phase read? The phase banners (The Crown Cracks, Last Rite), the named wind-ups (Grave Tide, Requiem) and the skeletons at 60%.
- [ ] **The guard** - at 60% he calls 3 Skeletons and a Bone Golem and cannot be targeted until they fall ("Shielded"). Does it read?
- [ ] **Its difficulty** - now after the Catacombs and a wall: a party that came straight there wipes about 95% of the time (simulator). Does it read as "come back stronger"?
- [ ] **The floor** - empty rooms to the throne for now. The door to the side room was not seen.

## Texts, icons and the Warlock (2026-11-01)

- [ ] **The new Warlock** - idle, wince, fall and walk at the Paladin's proportions. Demon Form too. Your call on the look.

## Crafting as the gold sink (2026-11-01)

- [ ] **The Forge line in town** - after the first found recipe, one line on the town screen, gone once the Forge is opened. Not seen on
      screen yet: only its rule is tested.
- [ ] **The 14 late recipes** - does a late run feel worth chasing, are their prices fair, and do the agent-written texts read right?

## Outside playtest fixes (2026-10-30)

Synthetic key events reached every handler below; what is not known is whether a **real** key press
does (focus), so press them for real.

- [ ] **Tutorial** - on a forced step the cursor starts on the button the tutorial wants.
- [ ] **Hover tips** - stats, the campfire's Combat numbers, resistances; and the tip following the hub's
      keyboard cursor.
- [ ] **Campfire hero status** - stats with meanings, ATK / CRIT / DODGE / P.DEF / M.DEF, the Momentum and
      Focus lines.
- [ ] **Revisit ranks** - rank I / II / III read apart (gold / orange / red with a badge).
- [ ] **Empty loadout** - putting away a hero's last ability leaves them with none, in and after a run.
- [ ] **Catacombs** - the first wipe there (after the tutorial) adds the replay hint to the death screen.

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
