# Needs a playtest

Built, and checked by the agent through the Unity MCP (scripted presses, screenshots, the simulator),
but **not yet tried by a human with a real mouse and keyboard**. Tick an item off (or delete it) once
it has been played; anything that turns out wrong becomes a backlog item in `NEXT_STEPS.md`.
Most of it came out of playtest 3 (2026-10-26 to 2026-10-28); the first section is the outside
playtest's fixes (2026-10-30).

## Outside playtest fixes (2026-10-30)

Synthetic key events reached every handler below; what is not known is whether a **real** key press
does (focus), so press them for real.

- [ ] **Keyboard the tester reported broken, not reproduced:** Enter on the story map, arrows in the
      town, Right in the Storehouse (now: next tab, All), the revisit condition picker. If one still fails
      with a real key, it is focus, not the handler.
- [ ] **Z / X / C** - Z confirms and X backs out everywhere Enter / Escape do; C opens and closes the
      dungeon pause menu.
- [ ] **Run complete / level entry** - Enter works without a Tab first.
- [ ] **Doors** - in a room like the tester's (party by a side door, another door in the bottom wall),
      Down picks the bottom-wall door.
- [ ] **Tutorial** - on a forced step the cursor starts on the button the tutorial wants.
- [ ] **Sphere grid cursor** - never stuck on a node near the start.
- [ ] **Hover tips** - stats, the campfire's Combat numbers, resistances; and the tip following the hub's
      keyboard cursor.
- [ ] **Campfire hero status** - stats with meanings, ATK / CRIT / DODGE / P.DEF / M.DEF, the Momentum and
      Focus lines.
- [ ] **Ability text** - "18 damage" in combat; "18 damage, scales with Strength" in the Storehouse.
- [ ] **Target picker footer** - highlighting an enemy shows what it attacks with and known weaknesses.
- [ ] **Turn order** - the "Turns" header and NOW tag; the HP plates easier to read.
- [ ] **A killing projectile** - the enemy's HP plate stays until the projectile lands.
- [ ] **Ability sounds** - Hamstring and other weapon abilities swing; every ability hit makes an impact
      sound.
- [ ] **Revisit ranks** - rank I / II / III read apart (gold / orange / red with a badge).
- [ ] **Floating Eye** - Eye Laser on one hero, Screech on the party.
- [ ] **Paladin** - Divine Protection cancels the next blow or ability hit on an ally ("Protected", gold
      shield icon); Aegis on the whole party; Smite hits harder on undead and demons.
- [ ] **Empty loadout** - putting away a hero's last ability leaves them with none, in and after a run.
- [ ] **Storehouse All tab** - with gear owned: every item listed, Enter equips / reassigns / takes off.
- [ ] **Enemy counts** - a few tries of the same floor feel comparable.
- [ ] **Catacombs** - the first wipe there (after the tutorial) adds the replay hint to the death screen.

## Hub and menus

- [ ] **Hall of Knowledge** (2026-10-29) - the Bestiary lot is renamed; Left/Right (or the tabs) switch
      Monsters / Recipes; recipe trees indent under their base item; an unfound recipe is a "???" row you
      can select, saying what it is made from. Real arrow keys not yet pressed.
- [ ] **Crafting at the Forge** (2026-10-29) - the Craft tab lists the Forge's three basic recipes plus
      any found; have/need per input, gold on the right; Forge spends them and the item lands in the
      Storehouse. Forging from a *worn* base puts the result on that hero. A recipe drop on a kill or a
      floor clear (Emberfall, Chapel of Knives), and that it never drops again once known. The town's
      gold pip on the Forge for newly found recipes.

- [ ] **Storehouse reassign dialog** - equipping gear another hero wears asks Reassign / Cancel; Enter,
      arrows and Esc all work; "Worn by X" reads in amber.
- [ ] **Town build badges** - a lot you can build or raise now shows a lit hammer and a gold name; an
      unaffordable one a dimmed hammer.
- [ ] **Sphere grid material costs** - each material shows "have/need", green when covered, red when short.
- [ ] **Run selection** - the "Your attempts" section is gone.
- [ ] **Victory / defeat screens** - trimmed rows, "XP Gained", the button fits the frame.
- [ ] **Combat speed** (Options on the title screen and in the pause overlay) - x1 / x2 / x3, takes effect
      at once mid-fight, back to normal speed after the fight and in town.

## The potion belt

- [ ] **Merchant, Potion Belt tab** - another slot, recipes (Antidote; Energy after the Catacombs; Elixir
      after the Drowned March), each type's capacity and potency; gold plus materials, green/red per material.
- [ ] **The revisit hint** - a step short of Sealing Wax / Grave Salt / Bog Pearl / Cinder Glass shows the
      gold line "... only turns up when you revisit ... at Fear 1 or higher". Not yet seen on screen.
- [ ] **Campfire belt slots** - a press cycles the potion in a slot; two per row; **locked once a run has
      started** (never tried in play).
- [ ] **Run start stocking** - the first floor fills each slotted potion to its capacity and removes the
      rest; floor 2 does not refill.
- [ ] **Energy Potion and Elixir in a fight** - +1 charge to the most-used ability; the Elixir heals too
      (its heal was only seen on a full-health hero); the log when every ability is already full.
- [ ] **No potion drops** - chests give gear only; no enemy drops a potion.
- [ ] **Revisit-only materials** drop on revisits at Fear 1+ (the Archive cache, the Lich, the Mirefather,
      the Cinder Tyrant, the late caches), never on a first clear.

## Combat

- [ ] **Health bars wait for the hit** - a party-wide enemy spell (the Floating Eye) drops each bar as
      its bolt lands; summons and Ultras now wait too.
- [ ] **Debuffs wait for the hit** - Lightning Bolt's status icon appears as the bolt lands, not as
      it sets off.
- [ ] **Spell effects** (new) - every spell starts with the same blue casting glow on the caster; a
      status, buff or heal lands with a coloured aura and sparks (purple falling for a curse, gold
      rising for a buff, green for a heal). An enemy's Weaken now casts and shows its curse landing.
      Judge the timing (each spell is ~0.5 s longer) and whether the glow is too strong.
- [ ] **Enemy ability banner** - every enemy spell and named move shows its name at the top.
- [ ] **Death animations** - every hero falls and stays lying; a revive gets them back up.
- [ ] **Downed heroes stay down** until the party goes back to town; refuges do not revive them.
- [ ] **Shield Up and Sacred Shield draw aggro** (+20 threat each; Sacred Shield's goes to the Paladin).
- [ ] **The Paladin's kit** - Holy Strike at the start, Holy Touch on the healer branch.

## Difficulty

- [ ] **The Catacombs and the Drowned March** feel right with gear (§5al).
- [ ] **The late runs' ordinary floors** are a bit harder (Ashen Deep, Blood Stair, Seeping Cistern, §5an).
      Their bosses were not touched; that is the next balance step in `NEXT_STEPS.md`.

## Art

- [ ] **Hero redraws** - the Warrior (new design), Ranger and Rogue at the Paladin's head size.
- [ ] **Stairs and chests** per level theme; the Wight faces the party.
- [ ] **Every ability has its own icon** (131). Weaker ones to judge: the three elemental cloaks, Aegis vs
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
