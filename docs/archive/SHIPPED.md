# Shipped ledger

What has shipped, newest first, moved out of `docs/NEXT_STEPS.md` on 2026-11-14 so the index stays short. Add a
line here when something ships; the reasoning lives in the plan files, `docs/BALANCING.md` and the subsystem guides.

- **"Found in" on the Hall's monster page, bows and daggers** (2026-11-14) — the Enemies and Hub guides. Each monster
  lists the levels and floors it is met on (unreached levels counted, not named). Three Ranger-only bows and three
  Rogue-only daggers in the shop; the Ranger left the swords.
- **Class items and a weapon for every hero** (2026-11-14) — the Dungeon and Hub guides. `ItemSO.UsableBy` limits
  gear to named heroes (`InventoryManager.Equip` enforces it; the Storehouse shows "Mage only"); each hero's class
  weapon is a rare (10%) boss drop on a replayable level near where they join (`RunLevelEntry.BossDrops`). Every
  weapon names who can use it (swords, cleaver, knife, staves; the Hub guide). Icons by PixelLab; stats and
  texts are agent drafts.
- **Status wards and the Hall's recipe hint** (2026-11-14) — the Cards and Hub guides, `plans/COMBAT_DEPTH.md` §9,
  `plans/HUB.md` §7. Gear can make a hero immune to an affliction or cut its chance (`ItemSO.StatusWards`); five pieces,
  each from one enemy, two dropped as they are (`ItemSO.DropOnly`) and three crafted from that enemy's recipe. Hall of
  Knowledge level 2 says where an unfound recipe turns up. Icons by PixelLab; texts are agent drafts.
- **Ultras rethought** (2026-10-30, owner; playtested and fine for now 2026-11-09) — `plans/COMBAT_DEPTH.md` §13. One fill
  rule for all (damage taken fills fastest, then dealt and healing done; the Mage stays on Focus), the gauge keeps its
  charge for the whole level, one Ultra free per hero on the trunk. The remaining Ultra nodes on the grid are later,
  hero by hero; they block nothing.
- **On screen, a run is a "level" and a run's level is a "floor"** (2026-11-08, owner) — root `CLAUDE.md`. Player text
  and new doc writing only; code, assets and save files keep Run/Level.
- **Playtested by the owner 2026-11-09:** the room events in every biome, the hub and menu changes (crafting at the
  Forge), and the FFIV-style battle screen.
- **The test boss: the Barrow King** (2026-11-02, owner) — the Enemies guide, `BALANCING.md` §5ba. An optional
  one-floor run after the Catacombs, a wall on purpose (about 95% wipes for a party that came straight there), hand-drawn, a refuge before the throne; three phases. Two engine pieces:
  a reaction on crossing a health line (`TriggerKind.OnHealthBelow`, the phase change) and a wound-up, named
  spell (a telegraphed `CastMagic`, `EnemyActionType.ChargeCast`). Then a guard: 3 Skeletons and a Bone Golem
  that shield him (`EnemySO.ShieldedBySummons`, `AlsoSummons`). `BossPhaseTests`; seen in play.
- **Localization follow-ups, two icons, the Warlock's head** (2026-11-01) — `plans/POLISH_CONTENT.md` §22, `PIXEL_ART.md`.
  Enum names, raw grid keys and English grammar in code are gone from the screen; Smite and Divine Protection have
  their own icons, and ten weak icons were redone; the Warlock and his Demon Form were redrawn to the Paladin's proportions.
- **The battle screen, FFIV-style** (2026-11-01, owner) — the Combat guide, `plans/POLISH_CONTENT.md` §1, `PIXEL_ART.md`
  §8f. A menu band over the bottom 27%, ten new backgrounds, units placed by their feet with shadows.
- **`CombatManager` and `RoomActionUI` split by job** (2026-11-01) — partial classes, one file per job (Rooms guide).
  The dead combat log went with it: `CombatResult.Log`, the `combat.log.*` texts, `GetSkipTurnMessage`.
- **The belt is decided** (2026-11-01, owner): free, restocked when a run starts from town. We want players to use
  potions and to buy potion upgrades. `plans/POLISH_CONTENT.md` §18c.

- **Health carries between floors** (2026-11-01, owner) — `BALANCING.md` §5au. Nothing heals on arrival at a
  floor; refuges heal 50% and the March has three. The balance model carries health and spends the belt per run.
- **The belt step of the tutorial** (2026-11-01, owner) — `TUTORIAL.md`. The Threshold's clear pays for the
  Merchant and a healing capacity step and teaches the Energy recipe; the tutorial ends after packing it.

One line each. Reasoning lives in `docs/BALANCING.md`, `docs/archive/` and the
per-subsystem `CLAUDE.md` files — not here.

- **The text system** (2026-10-30) — root `CLAUDE.md` (`Localization/`). Every player-facing text lives
  in one table (`Resources/TextTable.asset`), edited in `Tools ▸ Texts`; code, UXML and assets hold
  keys. 1,952 texts (764 from assets, 104 UXML, 1,084 code), English unchanged, then a first rewording
  pass. `LocTests`; checked in play on every screen.
- **Outside playtest fixes** (2026-10-30, the itch.io build). Group 1: Gilded Leaf, no run charges in
  town, revisit ranks coloured, upgrade nodes show their ability's art, 24 Ultra icons (PixelLab), the
  HP plate waits for a killing projectile. Group 2: Z/X/C keys, Enter works at once on the run-complete
  and level-entry screens, Back reachable in the ability menu, the first door arrow picks the matching
  wall, the grid cursor never sticks, the tutorial cursor starts on its target. Group 3: "18 damage"
  in combat and "scales with Strength" in the hub, hover tips (`HoverTips`), hero status at the
  campfire. Group 4: turn-order header and NOW tag, bigger HP text, enemy knowledge in the target
  picker, a readable sword glyph, swing and impact sounds for abilities. Group 5: Floating Eye's Eye
  Laser and Screech, the Paladin's own kit (Divine Protection, Smite, Aegis), empty ability loadouts,
  the Storehouse All tab, a per-floor enemy band, the Catacombs replay hint. Suite 1,689 / 0; what is
  not yet seen in play is in `TO_PLAYTEST.md`.
- **Click a room to walk there** (2026-10-30) — the Rooms guide. A click on another room's floor
  walks the party there when the way is safe (`DungeonMapOps.WalkRoute` + `RoomAt`,
  `RoomActionUI.TryWalkTo`): the map's fast-travel rules, plus one step into a room not yet entered.
  `DungeonMapTests` +7; suite 1659 / 0; playtested by the owner with the mouse.
- **The Hall of Knowledge** (2026-10-29) — the Hub guide, `plans/HUB.md` §7 phase 7. The Bestiary lot
  and screen renamed (code keys unchanged); a **Recipes** tab beside Monsters (Left/Right switch):
  every recipe in its tree under what it is made from (`RecipeTreeOps`, `RecipeTreeOpsTests`), base
  items no recipe makes heading their own tree, unfound recipes as selectable "???" rows in place
  (owner's choice, so a hint can go there). Suite 1652 / 0; both tabs checked in play.
- **Crafting at the Forge** (2026-10-29) — `plans/HUB.md` §7 phase 7, the Hub guide. Recipes are items
  (`ItemCategory.Recipe`, `CraftingOps`, `InventoryManager.TryCraft`): found on enemies and on floor
  clears (`LevelDefinitionSO.RecipeTable`), kept forever, skipped once known at every drop site. The
  Forge (renamed from Ability Forge) opens on a **Craft** tab: known recipes, have/need per input, the
  result's stats; a worn base hands its slot to the result. The Forge teaches Simple Sword, Leather Cap
  and Padded Jerkin; five more drop (Flaming Short Sword, Bog Hood, Hexsilk Gloves, Gilded Charm, the
  rare Voidforged Blade), with PixelLab icons. `CraftingOpsTests` + `CraftingContentTests`; suite
  1648 / 0; two crafts checked in play on a throwaway save.

- **The Seeping Cistern + the Campaign Map Editor** (2026-10-11) — the Dungeon, Enemies and Room Events
  guides, `BALANCING.md` §5ah. A new two-floor **`Secret` + `Challenge`** run off **The Warrens** (it
  appears once the Warrens is cleared): the **Rogue** is its captive, the guaranteed way in, and the
  Cutpurse gamble is now a long shot (Agility **8 → 20**). Three PixelLab enemies, all weak to Fire: the
  **Bile Slime** (Caustic Spit, poison), the **Spore Bloater** (bursts into poison on death, a reaction),
  and the boss **Mother Ooze** (Toxic Deluge on everyone, buds off two slimes). Attrition 2.94 / 0.58 for
  a Warrens graduate, 0.36 / 0.53 after the March. **`Tools ▸ Campaign Map Editor`**: the story
  graph with secret nodes, arrows, drag/connect/add/remove, the run asset inline, a preview save and the
  guard-rail checks. Suite 1538 / 0; the boss fight checked in play (`Sandbox/SeepingCistern.asset`).
- **The Warlock's workbook pass** (2026-10-11) — the Heroes, Magic, Combat and Enemies guides. From the
  owner's reviewed workbook rows: **Blood Shower** (10 + INT Shadow on every enemy, 12% health) replaces
  Cinderstorm at the blood branch's tip (Cinderstorm is now the Cinder Tyrant's enemy-only spell);
  Essence awakenings **Life Tap → Dark Pact (2 charges) → Dark Pact II (3)**, **Blood Shower → Crimson
  Rain** (+60% Bleed on every enemy) and **Siphon Soul → Soul Feast** (the drain heals the whole party,
  `SpellEffect.DrainToParty` + `EffectResolver.LivingAllies`). New trait **`UnitTraits.Bloodless`** -
  never bleeds (`StatusImmunity.TraitImmune`), shown in the Bestiary; Skeleton, Bone Golem, Lich, Wight,
  both machines and the Stone Sentinel; the Ghoul now bleeds (owner). `WarlockKitTests`; suite 1526 / 0; checked in play
  (`Sandbox/WarlockKit.asset`): Blood Shower's price, Soul Feast healing both heroes 23 off 68 damage,
  Dark Pact II refilling a slot, Crimson Rain bleeding a Slag Hound and not the Skeletons. **Leeching
  Curse** (owner's pick) replaced Oil Slick at the drain branch's tip: `BuffType.Leeched` on one enemy,
  3 turns - 30% of every blow or ability the Warlock's side lands on it heals him (`Cards/LeechTracker`,
  live and in the simulator; a magenta cross on the HP bar; curable). Oil Slick is now the Dragon's and
  Hex Weaver's `EnemyOnly` spell. Checked in play: +12 off his own 42 Bloodbolt, +7 off the Warrior's 26.
  **Every Warlock ability has a +1 charge spur** (`MagicUpgrade` Charges, owner 2026-10-11; carried to its
  awakening), so a full grid carries 3 of each and Dark Pact II's third charge lands. Suite 1531 / 0.
  **Open:** Crimson Rain is a draft name; all numbers are first drafts.
- **Ability upgrades on the sphere grid** (2026-10-09) — the Heroes and Magic guides. One node kind,
  `MagicUpgrade` (it replaced the crit-only node from earlier the same day): Power, CritChance, Duration,
  ApplyChance or Charges on one ability, summed per hero into an `AbilityGrant`, carried to the
  ability's awakening, folded into copies of the effects at the cast; the ability text, the hub tab and
  the balance model all read the same grant. **Showcase: the Ranger**, seven ▲ spurs (Aimed Shot,
  Poison Dart, Volley, Hush). Checked in play: the text, a +2 Aimed Shot landing 29 not 27, the grid
  screen. Suite 1469 / 0. **Next:** upgrade spurs for the other heroes, via the Hero Workbook.
- **Crits, misses (Blind) and debuff chances** (2026-10-09) — the Combat and Magic guides,
  `BALANCING.md` §5af. `CritRules` (crit numbers moved off `CombatManager`); `MagicSO.CanCrit`, on for
  Slash, Sunder, Backstab, Aimed Shot and Poison Dart; `SphereNodeKind.MagicCrit` (first one:
  `ranger-a-volley-crit`, a spur off Volley); the summon/Ultra physical single-target hits can crit too
  (Exatrix's Rend, Radiant Cut, Succubus Lash, the Horror's Crushing Maw / Engulf / Wrong Angles). `BuffType.Blinded` + `DefenseRules.RollMiss` (50%,
  physical only, before the dodge); **Smoke Bomb now blinds every enemy** (50% each, 3 turns) instead
  of the party Agility buff. `SpellEffect.ApplyChance` / `EnemyActionEntry.ApplyChance` rolled by
  `AfflictionRules` on every debuff (default 100). Owner's chances: Poison Dart 75%, every bleed 60%,
  every Silence 50%, the Freeze combo 50%; Smoke Bomb 50%. **The Hex Weaver blinds the party**
  with **Blinding Hex**, the first **enemy-only ability** (`MagicSO.EnemyOnly`: never on a grid or in
  the catalog, `EnemyAbilityTests`), cast weight 0.5 - at 1 the Blood Stair's step went to 75.2%. The balance
  model prices all three, Blind included (`StatShift.Blinds`). `CritMissAfflictionTests`; suite
  1459 / 0; seen in play (`Sandbox/CritBlind`).
- **Art and UI pass** (2026-10-08) — `POLISH_CONTENT.md` §21, `docs/PIXEL_ART.md` §8e. Combat backgrounds
  for the 8 floors that had none (`combat_halls/caverns/archive/chapel`, PixelLab pro, 15 generations
  each) and the battle art bottom-anchored at 1.3x so its floor meets the formation
  (`CombatStage.BackgroundFloorLift`); a 24px nine-slice window frame (`UI/Frames/window_frame.png`) on
  `.cd-window` and the five combat panels; a death screen backdrop with a guttering fire
  (`UI/Backdrops/death.png`, the dialog docked left); lights on the Hall of Progression, Merchant and
  Bestiary; a neutral flagstone `floor-tile.png` (it was blue bricks that read as text), mirrored per cell,
  and the seven legacy saturated room tints muted. All seen in play.
- **Enemy support verbs** (2026-10-08) — `plans/COMBAT_DEPTH.md` §12, `BALANCING.md` §5ae. BuffAlly,
  Guard (a single-target *cover* on `GuardTable`, until the guard's next turn) and Summon (called bodies
  pay nothing) in the planner, the live fight, the simulator and the closed form; on the Bog Shaman,
  Steam Automaton and Gilded Hoarder. `EnemyVerbTests`; suite 1,438 / 0; all three seen in the sandbox
  (`Sandbox/EnemyVerbs`).
- **The town and the menu backdrops come alive** (2026-10-08) — the Hub guide, `docs/PIXEL_ART.md` §8d.
  PixelLab idle loops for the campfire and forge (7 frames each), and `AmbienceLayer`: flickering glows,
  sparks, chimney smoke and twinkling stars over the hub, plus the title / level-entry torches and the
  story map's lanterns (`Resources/BackdropAmbience.asset`). Seen in play.

- **Run history** (2026-10-07) — `plans/POLISH_CONTENT.md` §15. `RunHistory.json`, recorded from
  `GameEvents` by `RunHistoryRecorder` (no scene wiring); `CombatFinished.FoesStanding` names who a
  lost fight fell to. The death screen says where and to whom, the run-complete panel totals the run,
  the story map lists each run's attempts. `RunHistoryTests`; seen in play on the sandbox's throwaway
  save (death screen and story map).
- **Every enemy has traits** (2026-10-07) — the Enemies guide. Demon / Flying / Beast / Elemental /
  Construct from the art; no gameplay change (only Disassemble reads them). No enemy is Undead yet.
- **Ultra gauge and threat read the health stream** (2026-10-07) — `plans/EVENTS.md` §4.3. Same
  numbers, no more re-reading bars; checked in play (a hit fills the gauge, an attack earns threat).
- **Chain Lightning has its own icon** (2026-10-07) — PixelLab pixen, 32 px, seed 9001.

- **The Mage joins the roster** (2026-10-07) — `plans/SPECIALIZATION.md`, "The Mage";
  `BALANCING.md` §5ad. A new main-line run, **The Sealed Archive** (Threshold → Archive → Drowned
  March; two floors, Mage captive on floor 1, Stone Sentinel boss), with templates `AshStacks` /
  `BrokenOrrery` and the `ArchiveStacksRoom`. His grid rebuilt into storm / frost / fire branches with
  the Fire Cloak at the fire branch's third node, so the Ashen Deep's answer is back; new ability
  **Chain Lightning**. The March retuned for a three-hero arrival (floors 2.85 / 2.75 / 3 / 3.1 / 3) and
  the Blood Stair's first two floors nudged (1.95 / 2.4). Suite 1,399 / 0; Chain Lightning cast in the
  sandbox (`Sandbox/MageShowcase`).

- **The Tinkerer rebuilt** (2026-10-07) — `plans/SPECIALIZATION.md`, "The Tinkerer". Disassemble
  (`SpellEffectType.Disassemble`: Mechanical targets, odds from Bestiary kills, salvage on success);
  `UltraKind.Mount` (a mech that guards its rider through `GuardTable`, follows her turns through
  `TurnManager.Follow`, lays tags via `SummonSO.UsesTags`, and steps out with the party for a
  replacement summon); `UnitTraits`; a 36-node grid; the elemental spells moved to a stub Mage grid;
  Clockwork Sentry and Steam Automaton in a new Slag Halls room; steampunk art for her, the mechs, the
  enemies and 14 icons. Suite 1,399 / 0; mount, turn order and Disassemble seen in the sandbox
  (`Sandbox/TinkererMechs`).
- **The hub shows grown items' stats** (2026-10-07) — `plans/EVENTS.md` §4.1. `GearPiece` (an item and
  its copy's save entry) through `WithGear` and the swap helpers, so every hub stat reads a copy's
  reached milestones.

- **Event-driven rules** (2026-10-07) — `plans/EVENTS.md`. `HealthOps` is the only writer of health
  (a guard test scans for others) and raises `UnitDefeated` with the killer; one death path
  (`ResolveDeaths`) replaced six corpse scans and fixed two unhandled deaths; `CombatEvents` per fight in
  the game and the simulator; `TriggeredEffect` reactions on items, enemies and summons; `GameEvents`
  for items, currency, XP, kills, levels, heroes and nodes, with a required `EconomySource`; items count
  kills/boss kills/victories per copy and grow at milestones; Sacrifice's `IsSacrifice` flag became a
  guests/stand-ins split. Reviewed independently the same day, ten findings fixed. Suite 1,381 / 0;
  sandbox fights checked in play mode.

- **Essence becomes the revisit currency** (2026-10-06) — `plans/HUB.md` §3c. Only revisits pay
  Essence (12 a floor × the reward multiplier). It buys **awakenings**: Essence-priced, 0 XP grid nodes
  (`SphereNodeKind.MagicAwaken`) that swap a known ability for a flashier version in the same slot. The
  Warrior's two: Unbroken Line (Bulwark + regeneration) and Rallying Roar (War Cry + Agility). The
  Forge's per-ability upgrades and `EvaluateEconomy` are gone; combos still upgrade. Suite green;
  bought in play mode on a throwaway save.

- **Revisits, first version** (2026-10-06) — `plans/REVISITS.md`. A cleared run (Drowned March,
  Warrens, Ashen Deep) can be revisited at Fear level built from composable conditions: +50% enemy health
  and damage as the base, plus Hardened Foes, Sharpened Blades, Swarming Halls and No Respite. Rewards
  (XP, gold, Essence) scale with Fear level; Void Shard drops only on a new best Fear level; the Warrens boss no
  longer drops it at all. `RunFear`, `RevisitOps`, `Resources/Revisits.asset`; `RevisitTests`; suite
  1,329 / 0; checked in play mode.

- **The Cultist's branch summons** (2026-10-05) — Abyssal Nightmare
  (madness: all-enemy delay + STR/SPR down, one random Silence) and Blood Idol (rites: a non-attacking
  ally that rotates party buffs, draws threat, costs 20% of his health); allies are now one of each
  kind per summoner. New `SummonSO` fields `RandomTargetEffects`, `RotateActions`,
  `SummonerHealthCostPercent`. Checked in the sandbox.
- **The Cultist, built** (2026-10-05) — Eighth hero: frail INT/SPR
  caster, three-branch grid (summoner with Writhing Spawn + The Watcher + Sacrifice, madness curses,
  blood-paid party rites), seven new abilities, rescued in The Hollow Vault. Suite green (1,306).
- **The Cultist's Sacrifice** (2026-10-04) — `UltraKind.Sacrifice`:
  any living hero but the last falls for the floor and a Horror rises in their place off their stats,
  its Attack chosen by their highest stat, for the rest of the fight. Target picker, sim policy,
  horror sprite, placeholder Cultist grid. Suite green (1,306); checked in the sandbox.
- **Warlock round 3** (2026-10-04) — `BALANCING.md` §5aa. The
  Demon Army balanced against the other replacements (Void Shard price, weaker troops, flat Hellfire,
  stay capped at 4); Ultras in the balance sim (`SimUltras`); a second Ultra kind, `Strike`, and two
  first-draft Demonology Ultras (Rain of Fire, Soul Harvest); Demon Form sprite redone; Void Shard
  supply vs the Blood Stair checked. Suite green (1,304).
- **The Demon Army + the Ultra gauge + Demon Form** (2026-10-04) — the
  Magic guide, `COMBAT_DEPTH.md` §13. Squad summons (`SummonSO.SquadTiers`, `SummonSize` /
  `SummonPromote` nodes); the Warlock's one summon is the Demon Army (3 Imps → 4, promoted to
  Succubi), improved from every branch. A minimal per-fight Ultra gauge filled by health lost, the
  **Ultra** command, `UltraSO`; Demon Form (+50% HP keeping the share, Shadow attack, Chaos Bolt).
  `UltraTests`; suite green (1,301); checked in the sandbox.
- **The Warlock rebuilt + allies that join mid-fight** (2026-10-04) — the
  Magic guide. `SummonKind.JoinParty` (a summon that fights *beside* the party, one per summoner, in a
  vanguard column), `SpellEffectType.Drain` and `RestoreCharge` (Life Tap, with a picker). The Warlock:
  HP 34 (the roster's biggest), a three-branch grid (blood / drain / demons), Imp + Succubus,
  Sacrifice renamed **Blood Pact**. `WarlockMechanicsTests`; suite green (1,283); checked in the
  sandbox (`Sandbox/WarlockDemons.asset`).
- **Spirit defends against magic, Luck dodges** (2026-10-03) — `BALANCING.md` §5z, the Combat guide.
  `DefenseRules`: Intelligence/Spirit-scaled damage is met by Spirit, the rest by Endurance; Luck
  gives a dodge chance against physical hits only (basic attacks and Strength/Agility abilities — a
  dodge voids the whole ability on that target). Three nimble enemies got Luck. `DefenseRulesTests`;
  suite green.
- **Hero visions** (2026-10-03) — the Heroes guide. `HeroSO.Vision` + `Tools ▸ Hero Vision`:
  each hero's intended fantasy, role, signature and branches beside what the grid grants today.
  Warrior/Paladin/Ranger pre-filled; the other four are the owner's to write.

- **The Ranger's summons** (2026-10-01) — `plans/SPECIALIZATION.md` §4b ("The Ranger's two"), the
  Magic and Combat guides. **Galewing** (branch A, special attack: 2 × Agility to every enemy, then
  -3 Endurance for 2 turns) and **Exatrix** (branch B, party replacement: 150% HP / 130% AGI; her
  Attack, **Rend**, delays its target 50% of a turn; Signature **Nightfall**, damage + Agility cut to
  all). Code: `SpellEffectType.TurnDelay` + `TurnManager.Delay` (capped at one extra turn),
  `EffectResolver.Clock`, `SummonSO.AttackAbility`. PixelLab art with idle loops; `TurnDelayTests`;
  verified live in `Sandbox/RangerSummons.asset`. Suite: the same two pre-existing balance reds.
- **The Paladin's summons** (2026-09-30) — `plans/SPECIALIZATION.md` §4b ("The Paladin's three"),
  the Magic guide. One per branch: the **Aegis Lion** (special attack: +50% own Endurance and +60
  threat — a taunt), the **Judgement Seraph** (party replacement, a hitter: 120% HP / 180% STR, Holy
  Radiant Cut + Judgement) and the **Dawn Stag** (40% heal to all + regeneration). PixelLab art with
  idle loops. Code: `SummonSO.BonusThreat`/`ThreatMultiplier`, and a power upgrade now raises only a
  summon's first effect. All three verified live in `Sandbox/PaladinSummons.asset`.
- **Every hero has a way in** (2026-09-30) — `plans/SPECIALIZATION.md` §5b item 4, the Dungeon and
  Room Events guides, `BALANCING.md` §5x. Three new sources for the last three heroes: the
  **Tinkerer** joins on clearing the Drowned March (`RunDefinitionSO.JoinsOnClear`, guaranteed, in the
  balance model), the **Rogue** is the Cutpurse gamble in The Warrens' new Thieves' Den
  (`RoomEventOutcome.JoinsHero`, not placed once owned), the **Cleric** is the captive of **The Drowned
  Chapel** — a new two-floor secret run off the March, keyed on the Tinkerer, home at last of the Dark
  Jailor. One join path (`DungeonManager.JoinParty`); `HeroUnlockTests.EveryHeroInTheRoster_HasAWayIn`
  guards the roster. The Blood Stair's floors 2–3 softened (2.6/2.9 → 2.3/2.55) to keep its step
  ceiling against a wider endgame party. Suite: same two pre-existing balance reds, one finding
  fewer (Warren Tunnels no longer over one health bar).

- **Menu review** (2026-09-28, worked through 2026-09-30) — a whole-game UI review, one section per
  screen, judged at 1280x720 against the Storehouse inventory; the to-do file was deleted once clear,
  and code comments cite it as "menu review". Shipped from it: every hub service on the inventory's
  fixed frame (Campfire, Merchant, Forge, Sphere Hall, Bestiary, story map, level entry, lot panel),
  the town dimmed behind services, one HP plate under every unit, `cd-reason` on every dimmed action,
  the room bar naming its thing, Fight/Flee naming the foes (`FoeLine`), event odds tags, dialog scrims,
  the pause order, the HUD's camera safe area (`CameraSafeArea`), Options dial bars, the hub purse,
  element colours, the map legend and `WaitingLine`, a left-docked Inspect with one-row drops, the
  victory order with XP per hero, and a run-complete screen that names the run and what it opened.

- **Second fresh-save playtest** (2026-10-02) — 18 findings, worked through by 2026-10-03 and the
  to-do file deleted once clear; comments cite them as "playtest 2 finding N". Shipped: an ability
  learned mid-run fills a slot (1), real run slots/charges in the Storehouse and campfire (2), the boss
  leads the foe line (3), inventory chips name their comparison (4), event choices wrap beside their
  tag (5), victory / level-clear window fixes (6), stat-node glyphs and a compact tutorial banner over
  the grid (7), the camera clears the room bars too (8), campfire roster captions (9), an affordable-only
  upgrade hammer (10), neutral party-lock wording (11), Threshold rooms with 2-3 enemies and a 65 HP
  Warden (13), a Merchant priced in scrap rather than timber (14), events once per floor (15), a Treasury
  gamble that pays more than the safe choice (16), no level-long afflictions in the exit room (17), and
  every hero starting on their first ability (18). The balance side is a clean baseline, not a tuned
  one — see `BALANCING.md` §5y. Not covered by that playtest: death and retry, Leave the Dungeon, the
  Merchant/Bestiary/Forge screens, summons, The Warrens, The Blood Stair, Drowned March floors 3-5,
  mid-run hero swaps.

- **First fresh-save playtest** (2026-09-28) — 24 findings, worked through the same day and the
  to-do file deleted once clear; code comments cite them as "playtest finding N". What remains lives
  on: 14 → `plans/POLISH_CONTENT.md` §21, 19 and 22 → `plans/BALANCE_OPEN.md` §0, 5 and 10 → the
  tutorial (§20). Shipped from it: party panel on rescue (1), automatic rescue (2), room spot
  placement (3), turn marker (4), level-clear window (6), dungeon HUD + bedrock backdrop (7), ability
  descriptions (8), enemy nameplates (9), story-map labels (11), UI scale + boss bar clear of the
  turn order (12), hub captions and building hover (13), two-column Inspect (15), campfire / spent
  markers (16), room seams (17), one exit per event (18), threat (20), a quiet tutorial floor (21),
  validation doc (23); the CI licence scare (24) was the local Hub, not CI.

- **Threat** (2026-09-28) — `plans/COMBAT_DEPTH.md` §11, `Assets/Scripts/Combat/CLAUDE.md`.
  `ThreatTable` (pure, `ThreatTableTests`): damage ×1, healing ×0.5, base 10, half of every pick flat
  random. Credited per hero-side action from an HP snapshot in `CombatManager`, read by every
  single-hero pick in `EnemyActionPlanner` / `EnemyMagicPlan` and the retarget fallback; wiped when a
  hero falls. Per ability: `MagicSO.ThreatMultiplier` + `BonusThreat`.

- **Summons, the party-replacing kind: the Warrior's Cairn Golem** (2026-09-28) —
  `plans/SPECIALIZATION.md` §4b ("The party-replacing kind", now marked built). Learned past
  `warrior-a-hold` (410 XP + 2 Void Shard + 3 Mire Hide) with a +50 HP-ratio / +1 turn / +1 charge
  chain. The party leaves the stage (invisible, frozen on the clock), the Golem — 250% of the
  summoner's health, a stat snapshot with no buffs — fights for three of its own turns from its own
  menu (Attack · Brace · ★ Quake · Inspect · Dismiss), every enemy blow lands on it and the one that
  breaks it goes no further. `CombatManager.HeroSideUnits()` is the one source of "the heroes";
  `TurnManager` gained suspend/resume and an inserted turn; the sim runs the same rules
  (`SummonStay`, `SummonOps.StatsFor`) and summons on a telegraphed wind-up. Boar-style intro, PixelLab
  sprite with a 4-frame idle, `SummonSO.ArrivalSound`, and the summon picker for a hero with two.
  **Fixed on the way:** Slow cast as a Debuff *raised* Agility (double negation in `SlowBuffHandler`).
  Frontier now sweeps per summon (`FloorFrontier.BySummon`) — not measured yet. 20+ new tests; the
  suite's only reds are the two pre-existing balance failures, byte-identical to the baseline.
- **Summons, first one: the Warrior's Bloodfang Boar** (2026-09-28) — `plans/SPECIALIZATION.md` §4b
  (decisions table), Cards guide. Special-attack kind: +50% of each hero's own Strength for 3 turns,
  1 charge, learned past `warrior-b-edge` (410 XP + 2 Void Shard + 3 Ember Iron) with a +10% / +1 turn /
  +1 charge upgrade chain. New Summon command, percentage stat buffs (`PowerMode.PercentOfTargetStat`),
  animated PixelLab creature on stage, and the balance model can summon and report finales with and
  without it. Not yet: the party-replacing kind, summons for the other six heroes, a picker for two.
- **A sandbox for quick tests** (2026-09-27) — `docs/SANDBOX.md`. `Tools ▸ Sandbox` (or
  `SandboxLauncher.Launch` from a script / the Unity MCP) runs `MainGameScene` with a configured
  party — any hero, XP bank, a greedy grid spend, "unlock the path to node X", abilities, gear — a
  chosen floor and up to five enemies one door from the start, and logs a setup report of what the
  party actually has. **Isolation is the save folder** (`FileHandler.DirectoryOverride` →
  `savedata_sandbox`), so nothing has to remember not to save; verified by hashing all 25 real save
  files before and after a run. The config is turned into ordinary save files, so the dungeon boots
  exactly as after the hub. New hooks: `DungeonManager.PartyOverride` / `SeedOverride` /
  `FreshDungeonSpawned`, `SphereGridOps.PathTo`. 16 tests (`SandboxSetupTests`).
- **FF ranks and large bosses on the battle stage** (2026-09-27) — Combat guide, `BALANCING.md` §5s.
  `EnemyFormation` (pure, 15 tests): one column up to three, **front 2 / back 3** for four or five,
  and a **boss alone at the back** with its escort placed just clear of its measured width.
  `EnemySO.CombatScale` sizes a boss on the stage (Warden and Tyrant 1.8 ≈ 3 units, twice a hero)
  without re-importing its sprite. `MaxBodiesPerRoom` **6 → 5**, the formation's design size; no
  room in the campaign exceeds it.
- **PixelLab art pass, part one** (2026-09-27) — `docs/PIXEL_ART.md`. The PixelLab MCP replaces
  hand-placed pixel grids (the `pixel-art` skill falls back to those, with a warning, when out of
  generations). New sprites and 3-frame idles for the Paladin, Rogue, Abyssal Warden, Cinder Tyrant,
  Dragon (which had been drawn as a green slime), Cinder Imp, Bog Shaman, Slag Hound, and a new enemy,
  the **Dark Jailor** (not yet placed in any spawn table). `CombatIdleMotion`'s scale pulse was
  removed — it stretched the new art — so a unit without idle frames now stands still; the
  leftovers are listed in `POLISH_CONTENT.md` §1.
- **Healing potions stop being a net loss** (2026-09-17) — `docs/BALANCING.md` §5v,
  `POLISH_CONTENT.md` §18b. Reported as "stale"; measurement said something sharper. A potion is
  spent as a **whole combat action**, so it competes against the *turn*, not against the health bar:
  5 HP flat against a measured average ordinary hit of **8.9** (139 hero×enemy samples, 26 HP bar)
  meant the party took a swing to gain less than a swing. **Not a weak item — a never-correct one.**
  It survived every prior pass because `EvaluateHealing` guarded only the *ceiling*
  (`MaxSingleHealFraction`), which reads 5-on-26 as healthy restraint: **a one-sided band reports
  the wrong half as fine.** Fixed in three parts. `ItemSO` gained **`ConsumablePercent`** beside the
  flat amount, with one shared `HealAmountFor` that combat, the balance model and the tooltip all
  read — a healing number derived twice is one that disagrees with itself — and the percentage is
  what stops a potion going stale as heroes buy health on the grid. The belt potion is **2 + 30%**
  (10 HP, 1.1 swings) and a **Greater Healing Potion** (3 + 45%, 15 HP) sits above it; both stay
  inside `MaxSingleHealFraction`, so a dead item was not traded for a new finding. And the analyzer
  gained the missing **floor** check, verified by feeding it a throwaway copy of the old potion and
  watching it fire. **Authoring the second tier immediately found a bug**: `FindHealingPotion` took
  the first healing consumable in catalog order, so the model sized the *free belt* off the rare
  tier — it now picks the weakest, because the belt is the free top-up and better tiers are things
  the player went and got. `SustainPool` 36 → 46 solo, tier-2 party pool **99 → 121 (+22%)**; the
  three standing-red floors still fail but the gap closed (advice moved from "cut to 1.8/1.3/1.7
  combat rooms" to "2.3/1.6/2.1"), so **re-measure attrition before trusting an older number**.
  5 new pure tests. Left open and stated: a full-restore Elixir conflicts with
  `MaxSingleHealFraction` by design, and **consumables still cannot be bought at all**
  (`MerchantUI.GenerateStock` filters to Equipment), which is now the binding constraint on how
  often the better tiers get used.
- **The party stops being bought, and the campfire starts selling the XP split** (2026-09-17) —
  `docs/plans/HUB.md` §7 phase 6 and `SPECIALIZATION.md` §5. The plan was to make the campfire's
  level *be* the bought party slot. **The purchase turned out to be the thing to delete**, not
  relocate: §5b had already ruled that gold never buys a hero, and buying the right to *field* one
  more hero is that trade wearing a different hat — the last piece of the tavern, and exactly the
  "no in-game explanation" §5 had been complaining about. The party is now **four wide from the
  first run** (`PartySlots` is one constant; `BonusPartySlots` deleted). **That did not flatten the
  decision**, because the toll was never what made it one: `XpSplit` divides a kill across the
  lineup, so going wide is paid every run in diluted XP rather than once at a shop — the code had
  been saying so in its own doc comment the whole time. What the fire sells instead is **how the XP
  is divided**: Even → **Mentor** (a named hero takes a double share) → **Catch Up** (the double
  share goes to whoever is furthest behind, re-chosen every award). The two are one mechanic
  differing only in who is picked, so there is one code path and one number (`FavouredShares`, 2),
  and **every mode hands out the identical total** — the property that keeps a campfire level legal
  under phase 6's rule and leaves the balance model's XP accounting untouched. The remainder follows
  the *favoured* hero rather than the leader, which a play-mode check turned up: on a small award
  the extra part is a real slice and giving it to index 0 could tie the leader with the hero the
  player deliberately chose. One model correction came with it — `InvestmentFrontier` charged width
  as *heroes bought past the base cap*, which with nothing bought prices every width at zero, so it
  now charges from **`PartySlots.FreeWidth`** (1, the solo start; the old base of 2 predated §5b
  restoring it). 14 new pure tests. **The balance consequence was measured, not assumed**: the
  regression failures after the change are byte-identical to before it — still "2 hero(es)", still
  158/180/140 against 99 — because the **roster**, not the cap, was what limited width there. That
  makes the four heroes with no unlock source a balance blocker, and means the analyzer wants to
  model roster size rather than cap. Two layout faults found by screenshot, both the same shape as
  the Forge's: three `--narrow` buttons abreast overflow a window by a third, and an unwrapped
  button carrying a hero *name* spills over the rows either side of it. ~900 gold of sinks left with
  the purchase; the Forge ladder replaced 750 of it.
- **Hub upgrades, and the Ability Forge as the first ladder** (2026-09-17) — `docs/plans/HUB.md` §7
  phase 6. The question the plan left open was *what a building level grants*, and the answer that
  unblocked it is a **rule** rather than a list: **access, capacity or information, never a raw
  stat**, because §7 already made buildings a *hard* axis on the frontier and a stat-granting level
  quietly turns them into a second gold-to-power route. The **Forge** is the worked example —
  `MaxLevel 3`, 250/500 gold, granting an ability/combo upgrade ceiling of **1 → 3 → 5** — and three
  properties are the part worth copying. It **gates buying, not what has been bought**
  (`GetMagicPowerBonus` deliberately never consults the ceiling, so no hub change can reach back
  into a spell the player is carrying); a full Forge lands **exactly** on the old flat
  `MaxComboUpgradeLevel`, so *what a finished save can reach is unchanged and only the ramp is
  gated* — which is why `RunCurveModel`'s "everything built" default still reports what it always
  did; and the player is told **before** paying rather than meeting a dead button. Two pieces of
  shared machinery landed with it and every later lot inherits them: `BuildingOps.UpgradeCost`
  (`GoldPerUpgrade` buys the *first* rung, each one after costs a multiple — flat pricing makes a
  ladder stop mattering the moment one rung is affordable, and this is `PartySlots.CostForNext`'s
  own curve) and `BuildingSO.LevelGrants` + `GetLevelsWithNoGrant`, which fails the build on a
  priced rung that never says what it buys — the mirror of the existing free-upgrade check. 13 new
  pure tests (62 green across the three hub/forge classes; the suite's only 2 reds are the
  documented solo-start balance failures in `BALANCE_OPEN.md` §0). Verified in play mode through the
  real click path: 2215 → 1965 gold, lot level 1 → 2, ceiling 1 → 3, next price 500, then Slash
  climbing to Lv 3 and refusing to go further with **425 essence still in the purse** — the case
  that would read as a bug without the "raise the Forge" line. **One fault was found by looking at
  it**: the grant line did not wrap (UITK labels default to `nowrap`) and walked straight out
  through both window borders, which is why `.hub-lot-grant` exists rather than reusing
  `.cd-info-label`. The 250/500 prices are **authored, not measured** — a new gold sink competing
  with gear, on an axis the frontier has not been taught yet.

- **The floor map** (2026-09-08) — `docs/plans/POLISH_CONTENT.md` §14a, the last piece of §14.
  **M** while walking, or the pause overlay's new **Map** button; the two are never up together and
  Back from the map puts the overlay back. Rooms drawn **to scale** from `Room.GridPosition` plus the
  template's size, doors as trimmed connectors, one glyph per room — and three *independent* channels
  so nothing competes: contents in the centre (enemies → captive → event → cache → refuge), a gold
  box for where the party stands, and the exit's `▼` in the corner (a boss room is both at once, and
  had to stay both). **The knowledge rules are pure and tested** (`DungeonMapOps` +
  `DungeonMapTests`, 18 cases) because they are the half that can leak a floor, and they **mirror
  what the dungeon already shows** rather than inventing a second model: a room is drawn if it has
  been entered or a door from an entered room leads to it — the same rule `Room.Hide` uses for door
  renderers — so the reveal is **one step deep, not a flood fill**, and content is reported for
  explored rooms only. An unvisited neighbour is an outline that says a room is there and nothing
  about what is in it. The status line names the **frontier** and never the floor's room count:
  *have I searched everything* is answered by "2 ways not yet taken" reaching zero, while a total
  would hand the player the size of the floor before they walked it.
  **The plan's "just reuse `SphereGridView`" was wrong and is now recorded as wrong** — a sphere grid
  places an authored graph in its own space and needs pan/zoom; a dungeon already *has* real 2D
  coordinates, so this fits the rects to the panel and the floor's shape becomes the information.
  A map you have to pan is a map you cannot read at a glance. The idiom was reused (Painter2D on a
  host element authored in UXML), the widget was not. `DungeonManager.CurrentRooms` is new — the map
  is the first caller that needs the whole graph rather than the room the party is in. Verified in
  play mode on a real floor: rooms explored and frontier counts matching the walk, the cache and
  enemy glyphs in the right boxes, door lines trimmed, and the full window dance
  (M → back to room, Escape → pause → Map → Back → pause → Escape). Two rendering faults were found
  and fixed *by looking at it*, which is the argument for the screenshot pass: centre-to-centre door
  lines drew straight through the unfilled outlines, and `--cd-stone` as the explored fill was
  indistinguishable from the panel's own ground. **The keys were driven synthetically, so the
  handlers are proven and the real key path is not** — M in particular wants a human press.
- **Fast travel on the map** (2026-09-08, user request) — clicking an explored room, or arrows +
  Enter, puts the party there instantly. **The design is entirely in what it refuses**, and the two
  refusals are the load-bearing part: travel is **off completely while live enemies hold the party's
  room** — those doors are already disabled and combat offers Flee, so stepping out through the map
  would be a free Flee with none of its cost — and **no route passes *through*** an enemy-held room,
  because a walk cannot (the room seals on entry). Such a room is still a legal *destination*; going
  back to finish a fight is a real choice. Only explored rooms, so travel can never skip a floor.
  **Arrival reuses the ordinary door-walk** through the route's **last** door, which is what leaves
  the entry door, `Party.PreviousRoom` and therefore the **Flee route** exactly as a walk would have
  — via a two-step placement (`PlaceInRoom` to the second-to-last room, then `PlaceAtDoor`) so the
  party is not recorded as having come from the far side of the floor. The rules are re-derived at
  travel time rather than trusted from the open model, so a stale click cannot outrun a room's state.
  Camera snaps on arrival, because a lerp across a floor reads as a sweep rather than as arriving.
  Input is a transparent `Button` per travellable room (`SphereGridView`'s choice for nodes) and the
  keyboard cursor is a **parchment-light ring drawn inside** the box — the first attempt used a gold
  wash, which over the dark room fill blends to mud, and gold is already spoken for by the party's
  own room. 8 more pure tests (26 total); verified in play mode both ways round: travel by click and
  by Enter, `PreviousRoom` landing adjacent, and a fight correctly refusing to let the party leave.
- **The last five things the player read as "Magic"** (2026-09-08) — the display rename of
  2026-09-06 was audited rather than extended. A **full code rename was considered and declined**:
  ~2,600 `Magic*` identifiers, 231 serialized keys in `.asset` files (189 `GrantedMagicKey`, 42
  `Magic`) and three prefabs, all of it developer-facing and none of it visible to a player. What was
  actually still leaking: the **Forge** called itself "Magic Forge" in two places (the screen title
  in `Hub.uxml` and `forge.asset`'s `DisplayName` — now **Ability Forge**, so lot and screen agree
  the way Bestiary and Merchant already do), the forge and storehouse **blurbs** sold "a spell" and
  "spells", **Hush** sealed a foe's "magic", and three `StatCatalog` descriptions scaled "spell
  power" (authored player text, unused until §16's compendium, wrong for a Tinkerer's gadget either
  way). Everything else that greps as Magic or Spell is a field name, an `m_EditorClassIdentifier`,
  a `Debug.LogWarning`, an XML doc comment or a Balance-window column. **Two warts recorded, not
  fixed:** `Assets/Prefabs/UI/Combat/MagicRow.prefab` is unreferenced legacy uGUI carrying a visible
  `m_text: Magic` (dead, so invisible — delete it when the uGUI leftovers are swept), and
  `EffectResult` says a hero **casts** every ability, which is the same category error one level
  down — a Tinkerer does not cast a gadget.
- **The party bar while exploring, and a pause menu** (2026-09-06) —
  `docs/plans/POLISH_CONTENT.md` §14b, §14c. The party window is no longer combat-only: it is up
  while walking the floor, rebuilt per room (`ShowPartyStatusOutOfCombat`, so a hero rescued
  mid-level appears) and kept through the end of a fight rather than torn down. Health is
  *level*-scoped — it refills on a fresh floor or in a refuge — so every walking decision needs it,
  and all of them were made blind. New **`pause-window`** in `RoomAction.uxml`, opened with Escape,
  checked before everything else in `OnCombatHotkey` and the only thing the keyboard reaches while
  up. It carries the hub's audio dials **by reuse, not by copy**: `AudioOptionsUI` queries its root
  by element name, so the UXML repeats the `master-*`/`music-*`/`sfx-*`/`options-mute` names and
  hands it the pause window. It opens only from the three states the room panel owns the keyboard in
  (walking, Fight/Flee, a hero's command menu) — which are also the states where nothing is ticking,
  so "Paused" is honest without stopping a clock, and pause never fights a dialog or the ability
  picker for Escape. **Leaving mid-run** (`DungeonManager.HandleQuitToHub`) leaves the *run*
  standing — the map offers it as continuable — but **restarts the floor**
  (`DungeonSaveData.RestartAtEntrance`): every enemy stands back up and the party begins at the
  entrance, having forfeited the floor's un-banked XP, kill-gold and loot. **The party is what does
  not reset** — health, charges, afflictions and spent potions all restore, so a hero who went down
  stays down and a wounded party walks back in wounded. Leaving can never buy back a death or a
  heal, which is what keeps it "I want to stop playing" rather than a tactic; it is still not a
  cheaper death (dying deletes the run save) because it buys nothing — the same enemies, in the same
  places, met exactly as hurt. **The hole that rule opens is closed in the same change**: a floor
  that resets would put the *refuge* back, and refuge + carried-over health is an unbounded heal
  loop, so a restart keeps everything the floor already paid out (looted cache, spent refuge,
  resolved event). The rule is *the restart puts the enemies back; it does not put back anything the
  floor already gave you.* `HandleQuitToHub` also discards pending gold, a plain field on a
  `DontDestroyOnLoad` singleton that would otherwise be banked by the next run's first clear.
  Verified end-to-end in play mode on two real floors — wounds and a downed hero carried, enemies
  restored, party back at the door, map dark again, and a spent cache still spent — plus panel
  states, dial readouts and the arm/close/disarm cycle. **The keys were driven synthetically, so the
  handler is proven and the real key path is not.**
- **Heroes are unlocked, not handed out; and "Magic" becomes "Ability"** (2026-09-06) —
  `docs/plans/SPECIALIZATION.md` §5b. The unlock *record* already existed
  (`PartySaveData.OwnedHeroKeys`, written deferred by `Party.MarkOwnedDeferred` so a rescue is
  forfeited on a wipe) — what was missing was that **nothing used it**: `StartingHeroes` handed the
  player Warrior + Paladin + Ranger, which made the tutorial's Paladin rescue a silent no-op
  (`PlaceCaptiveIfConfigured` skips a captive you already own) and left four of the seven heroes
  unreachable. The start is **one hero** again, so both rescues are real. New:
  `CampaignNodeEntry.RequiresHeroes` + `CampaignOps.HeroGateSatisfied` — the first **key-shaped**
  gate in the game, always all-of, ANDed with the run gate, and **failing shut** when a caller
  passes no roster (a run wrongly locked is visible; a run wrongly offered is not). The Hollow
  Vault now asks for the Ranger. Two static-analysis walks opt out via `CampaignOps.IgnoreHeroGate`,
  because `GetUnreachableNodes` has no roster and would otherwise report every gated node as a
  prerequisite cycle. The guard rail is `GetNodesWithBrokenHeroGates`: a gate is only sound if the
  hero is on the **required** path — a captive down an optional branch or behind an `Any` fork is
  not guaranteed, and gating on one strands the save. `Campaign_NeverStrandsASaveWithNothingToPlay`
  now walks a roster forward alongside the completed-run set. **The Reedcage** (`DrownedMarch_2`) is
  a hand-authored six-room layout at Drowned March floor 2 — a deliberately linear stair into the
  mire, so the only captive-eligible rooms are on the only route and the Ranger cannot be walked
  past. **Display rename**: the combat command, both pickers, the Forge tab, the Inventory tab and
  the enemy Inspect heading say **Ability**, not Magic or Spells (the code keeps `MagicSO`); the
  Forge also stopped telling the player to *Draw* a spell it has not been able to since 2026-09-04.
  **Measured cost, and it is real**: taking two heroes out of the starting lineup drops the second
  tier from three bodies to two, and three floors (Silt Shallows, The Reedcage, Warren Tunnels) go
  **critical** on `EveryRunLevelIsClearableOnOneHealthBar`. Isolated by re-running the balance suite
  with the old three-hero start, where it is **13/13 green with The Reedcage already in place** — so
  this is the solo start's bill, not the new content's. Left standing deliberately (balance is
  paused until the specialization refactor lands); see `docs/plans/BALANCE_OPEN.md` §0.
- **The town is painted, and the gates are on** (2026-09-05) — `docs/plans/HUB.md` §7 phase 4 plus
  the art. A lot is **Absent** until its `RequiredRunKeys` clear, **Available** (a foundation and a
  material price) once offered, then **Built**; clicking an unbuilt lot opens a panel that names the
  price or the run in the way, and confirming it spends the materials and **phases the new sprite
  in over the backdrop**. Materials gate *whether*, gold gates *when*. `BuildingSO` gained a
  **draw rect separate from its hit box**, so silhouettes overlap freely while UITK's rectangular
  hit-testing stays unambiguous — `HubView` is backdrop / sprites / buttons in three layers.
  Placeholder pixel art ships for all of it (`tools/hub-art/`, disposable). Opening sequence:
  campfire and storehouse free; Sphere Hall (1 timber), Bestiary and Merchant offered at once; only
  the Forge behind the tutorial. **What a building *level* grants is deliberately undecided** — the upgrade logic is
  complete and tested, but every lot is `MaxLevel 1` so nothing is on sale that buys nothing, and
  `HubState.LevelOf` is the seam waiting for that call.
- **The hub becomes a place, and the menu splits in two** (2026-09-05) —
  `docs/plans/HUB.md` §7 phases 2-3, plus §5b's tavern removal. **Three scenes now**: `MenuScene`
  is a dependency-free title screen (Continue / Options / Quit, reads no save, room for the
  save-slot picker), `HubScene` is the town, and the loop is **hub → dungeon → hub** — both ways
  out of a run return to the hub, never to the menu. The ten-button home screen (which had
  ~85 units of headroom left, i.e. one more button) became a **painted town**: `HubSO` +
  `BuildingSO` + the pure `BuildingOps`, progress in `MetaProgressSaveData.Buildings`, rendered by
  `HubView`/`HubPresenter` as one letterboxed 1280x720 canvas — six lots and a road, flat
  placeholders, no art required. **Every lot shipped built** at that point, behind a single constant
  with the gated path already under test — phase 4 removed the constant hours later. The **story is not a building** (a lot must never be able to lock the player out of
  running) and the **campfire** is the one lot placed by default. The **tavern is deleted** —
  `TavernUI`, `TavernStock`, `ShopPricing.RecruitPrice`, `HeroSO.RecruitCost`,
  `HeroRoster.RemoveOwned`; `GetRecruitable` became `GetUnownedHeroes`. Heroes come from rescue and
  `StartingHeroes` until §5b's unlock record lands.
- **Default-unlocked grid nodes, and the first thing materials buy** (2026-09-05) —
  `docs/plans/SPECIALIZATION.md` §4c, `docs/plans/HUB.md` §7. Two fields on `SphereGridNode`.
  `UnlockedByDefault` makes a node active from the moment the hero exists — the **Warrior starts
  knowing Slash and the Paladin Holy Touch** (a new single-ally heal), so nobody is ever
  empty-handed on their first fight. `MaterialCosts` puts a second, un-grindable price on a node:
  `warrior-b-cry` (War Cry) now costs 350 XP **and** 2 Ember Iron + 1 Void Shard, the first drain
  materials have. Both fold through one new function, `SphereGridOps.ActiveNodes` (saved ∪ default),
  which every rule reads — so a default unlock grants, opens and refuses re-purchase with no
  migration and nothing recorded in the save. **Fixes a standing reachability bug**: adjacency to the
  *start node* used to open a node whether or not the start had been bought, so a new hero's second
  node was purchasable while the first still read as unbought. The frontier now only grows out of
  something the hero actually holds. The other five heroes still buy their signature — arming them is
  one checkbox each.
- **Materials drop** (2026-09-05) — `docs/plans/HUB.md` §7 phase 1. `ItemCategory.Material` + ten
  authored materials; `EnemySO.LootItem` → a rolled-per-entry `List<LootDrop>` (flat `Chance`,
  quantity range) and a new `LevelDefinitionSO.MaterialTable` rolled by caches — **enemies drop what
  they are made of, a floor yields what the place is made of**. Bestiary shows one loot row per
  entry, each `???` until seen; a hub Inventory ▸ **Materials** tab; `MaterialCost` +
  `InventoryOperations.SpendMaterials` (all-or-nothing) ready for the drains. `MaterialYieldModel`
  measures the tap per floor/run and the analyzer reports an unobtainable material and a
  `MaterialTable` on a level with no cache. **Nothing spends materials yet** — that is phase 2.
- **The seven-hero roster** (2026-09-05) — `docs/plans/SPECIALIZATION.md` §5b/§4c. Tank, Acolyte and
  Scout deleted; **Paladin, Cleric, Ranger, Cultist, Tinkerer, Rogue** authored with grids, sprites
  and ten new spells (the holy line, the Ranger/Rogue Agility line, the Cultist's blood magic). Every
  grid is two branches — three for the Paladin — off a short trunk, spells laddered ~385/980 xp, no
  branch named anywhere in the data. `SphereGridSeeder` deleted. **The tavern still sells heroes**;
  the unlock half of §5b is not done.
- **Draw removed; magic moves onto the sphere grid** (2026-09-04) — `docs/plans/SPECIALIZATION.md`
  §9b. Every spell is learned on a `MagicKnown` node; **knowing and carrying split** (slots are 2 +
  `MagicSlot` nodes, the kit chosen on a new Inventory ▸ **Spells** tab via `MagicLoadoutOps`);
  charges refill at run start and in a **refuge**; `EnemySO.DrawableMagics` → `Spells`, the monster's
  own repertoire, with the Bestiary reveal repointed at per-enemy `ObservedSpellKeys`; `ProgressionMap`
  rebuilt on grid `MagicSource`/`PathCost`. Coverage 17/17 magic, 4/4 combos; the balance suite stayed
  green. §4c still owes the actual specializations.
- **Boss encounters** (2026-08) — `EnemySO.IsBoss` + `BossBehavior` (telegraphed party-wide signature,
  enrage under 30%), placed via `RunLevelEntry.BossEnemy`, alone in a sealed exit room. `BossAdds`
  escorts added 2026-08-30; every boss now has one.
- **Balance analyzer** — `Assets/Scripts/Balance/` + the `Tools ▸ Balance Analyzer` window +
  `BalanceRegressionTests`. *(No `BalanceRules.asset` is checked in; the window's "Create rules asset"
  button writes one. Until then it runs on code defaults.)*
- **Elements & Unlocks tab** — `ProgressionMap` models the **sphere grids** as a supply chain: unlock
  timeline, magic × hero availability matrix with the cheapest XP route, per-level elemental coverage.
  *(Modelled the Draw tables until 2026-09-04.)*
- **Test suite repair + headless runner** (2026-08-21) — 46 red of 339 → 1, every one a stale *test*.
  `ExecutionSettings.runSynchronously = true` runs the whole suite in-process in about a second.
- **Room events** (2026-08-21) — `Assets/Scripts/Rooms/Events/`; stat-gated, weighted-outcome gambles
  behind the room's Action button, priced by `RoomEventModel`.
- **Sphere grid** (2026-08-22) — `SphereGridSO` + pure `SphereGridOps`; XP is a per-hero bank spent on
  nodes at the hub. `LevelConfiguration` deleted. Doubled and repriced by depth in §5s (2026-09-02).
- **Roster progression** (2026-08-20/22) — solo start, tavern + rescue acquisition, `PartySelectUI`,
  bought party-slot cap (`PartySlots`, base 2 / max 4, 300 then 600 gold), even-split XP (`XpSplit`).
- **Three new stats + the generic stat model** (2026-08-21) — Intelligence / Spirit / Luck;
  `StatType` / `StatBlock` / `StatCatalog`; `Attack`→`Strength`, `Defense`→`Endurance`; per-hero
  `AttackStat`; Luck-driven crit on a diminishing curve.
- **Room kinds** (2026-08-25) — Combat / Connector / Treasure / Rest, placed per instance on a
  per-level quota by the pure `RoomKindPlanner`.
- **Elemental defence** (2026-08-25) — resistance buffs actually work; `PowerMode`,
  `SpellEffectType.HealthCost` + two-pass benefits-then-costs resolution, four cloak cards.
- **Enemy behaviour as authored data** (2026-08-25) — `EnemyBehaviorSO` replaced five hardcoded
  `IEnemyBehavior` classes; provably behaviour-preserving.
- **Enemies cast their drawable magic** (2026-08-25) — a `CastMagic` action gated by `ChanceGate`;
  charges never spent; spell power scales with the level, not the asset.
- **Run chaining / campaign graph** (2026-08-25) — `CampaignSO` + `CampaignOps` + `CampaignMapUI`;
  five runs, one secret gated on both branches.
- **Floor simulation** (2026-08-26) — `RunFloor` fights a whole floor off one pool of health, potions
  and charges. Replaced the per-room measurement that reported 63/63 wins.
- **Discovery-gated reveal** (2026-08-29) — `MetaProgressSaveData.Bestiary` + `BestiaryOps`,
  in-combat **Inspect** (free, FFX-style Scan), hub **Bestiary**. *(The masked-magic half was
  repointed on 2026-09-04: it now hides spells this enemy has not been seen to cast, per enemy.)*
- **Investment frontier** (2026-08-28 → 2026-09-02) — party width × grid XP × gold, Pareto-minimal
  mixes per floor; `GearLoadout`, `InvestmentPointsPerGold`, `IncomingDamageMix`.
- **Audio** (2026-09-01) — `Assets/Scripts/Audio/`: SFX banks, crossfading music bed with per-level
  overrides, Master/Music/SFX volume + mute persisted to `savedata/Audio.json`, reachable from a hub
  Options screen.
- **Battle polish tiers 1–4** — turn indicator, idle motion, projectiles, crits, resistance popups,
  boss telegraphs, combo flourish, victory/defeat framing, camera zoom-punch, per-level backdrops.
- **Deferred persistence** — mid-level hero HP (`PartyHealthSnapshot`), the consumable ledger
  (`ConsumablesSpent`, a delta not a snapshot, idempotent) and level afflictions. *(Cross-run magic
  loadouts were the fourth until 2026-09-04: with magic on the grid there is nothing to bank, so
  `MagicLoadout.json` holds only the player's hub-side choice and is written immediately.)*
