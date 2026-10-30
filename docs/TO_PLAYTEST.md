# Needs a playtest

Built, and checked by the agent through the Unity MCP (scripted presses, screenshots, the simulator),
but **not yet tried by a human with a real mouse and keyboard**. Tick an item off (or delete it) once
it has been played; anything that turns out wrong becomes a backlog item in `NEXT_STEPS.md`.
Everything here came out of playtest 3 (2026-10-26 to 2026-10-28).

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

## The dungeon

- [ ] **Click a room to walk there** (2026-10-30) - with the mouse: an explored room across the floor
      (fast travel), a dark room right next door (same as its door), a dark room beside an explored one
      (travel, then step in). Refused: out of a room with enemies, through one, two dark rooms deep.
      "No safe way there" floats on an explored room you cannot reach. Clicking on the party window or
      a bar must not walk. **A real mouse click has not been tried** (the agent called the walk directly).

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
