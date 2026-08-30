# Enemy System (`Assets.Scripts.Enemies`)

> **An `EnemySO` is a template, not a stat block.** The same enemy appears all over the campaign -
> Floating Eye and Dragon are in every authored level - against parties that range from 40 HP and no
> spent XP to 64 HP and 176. One authored stat block provably cannot be right in both places, so the
> template carries the enemy's **identity** and the level it appears in carries its **numbers**. See
> *Per-level tuning* below; the `EnemySO` inspector deliberately has no balance footer any more,
> because the numbers it would show belong to a level. Use `Tools > Balance > Balance Analyzer`,
> whose Enemies tab is one row per enemy **per level**.

- **EnemySO** (ScriptableObject, `SO/Enemy`, assets in `Assets/ScriptableObjects/Enemies/`): the **definition of an enemy type** — `Key`, `DisplayName`, `Sprite`, base stats (`Attack`/`Defense`/`Health`/`Agility`), **kill rewards** (`XpReward`, `GoldReward`), `Archetype`, `DrawableMagics` (the **Draw list**), `Resistances`, `LootItem`. This is the single source of truth for what an enemy *is*. On death (`CombatManager.HandleEnemyDeath`): loot drops, `XpReward` is split evenly across the fielded party immediately (`Party.DistributeXp`), and `GoldReward` accumulates into `MetaProgressManager` pending gold (banked only on level-clear — see the Progression guide).
- **Identity: `Key` is write-once, `DisplayName` is free.** `EnemySO.Key` is the identifier persistent knowledge about an enemy is filed under — what the player has learned of its resistances and weaknesses — and `SaveKey` falls back to `DisplayName`, then the asset name, so an enemy authored before the field existed still resolves. The same contract as `HeroSO.Key`: **always persist off `SaveKey`, never off `DisplayName`**, because a display name is meant to be renameable and keying off it orphans the record the moment someone retitles an enemy. Keys are authored as the **asset** name (`EyeBall`, not `Floating Eye`) for exactly that reason. `EnemyIdentityTests` fails on a blank or duplicated key, so the fallback stays a migration path rather than somewhere to leave a new enemy.
- **`EnemySO.Label`** is the display name or the asset name — use it for anything on screen or in a report. It exists because that ternary had been copy-pasted into five places in the balance model; there is now one.
- **One shared Enemy prefab** lives at `Assets/Resources/Enemy.prefab`. `EnemyManager` loads it once (`Resources.Load<GameObject>("Enemy")`) and stamps each instance with an `EnemySO` plus the level's tuning via `Enemy.Initialize(so, tuning)` — so there is exactly one prefab, and the SO drives the sprite/stats/name. (The old per-type `EyeBall.prefab` under `Assets/Prefabs/` is no longer referenced.)
- **EnemySpawnEntry** (in `RoomSO.EnemySpawnTable`): now just `Enemy` (an `EnemySO`) + the per-room roll params `SpawnChance` and `EvaluationCount`. All identity/stats moved to the `EnemySO`.
- **DrawableMagicEntry**: one offering on an enemy's Draw list — a `MagicSO` plus the `Charges` (1–9) a successful draw grants. It is also what a `CastMagic` action draws from when it names no specific magic: `CastWeight` biases which entry, and casting never touches `Charges` (those are the player's grant only). **`CastWeight: 0` means drawable but never cast** — a zero-weight entry is skipped by `EnemyMagicPlan.Select` while every other entry has a weight, which is how the defensive cloaks sit on an enemy's list: a cloak wards the element that enemy *deals*, never what threatens it, so casting one would spend its turn and a slice of its health on nothing. (Beware the edge: if *every* entry on an enemy is 0 the pick is uniform, so 0 only means "never" alongside non-zero siblings.)
- **EnemyMagicPlan**: the pure helpers a `CastMagic` action uses — which magic (weighted by `CastWeight`) and at whom. `MagicTargetType` is authored from the *player's* side, so for an enemy "enemy" means the hero side and "ally" means the other monsters (a single-ally cast picks the most wounded). Called by `EnemyActionPlanner`, covered by `EnemyCastingTests`.
- **EnemyBehaviorSO** is an enemy's repertoire as data — see *Behaviors* below. `EnemySO.Behavior` points at one; `ResolvedBehavior` falls back to the built-in preset for `Archetype`, and `ArchetypeOf` reports the assigned behaviour's label so the two cannot drift.
- **EnemyManager** spawns enemies into rooms (with optional manual-layout overrides) and tracks/cleans up live enemies. For each entry it instantiates the shared prefab and calls `Enemy.Initialize(entry.Enemy)`.
- **Enemy** implements `ICombatUnit` (see the Combat guide). `Initialize(EnemySO)` applies the definition (sprite, `Stats`, archetype, Draw list, resistances, loot, and `gameObject.name`); `DisplayName` comes from `Definition.DisplayName` (so it's the SO's name, **not** "Prefab(Clone)"). `GetEffectiveAttackPower()`/`GetEffectiveDefense()` return raw stats (no item bonuses). Runtime charge state (`ChargingEntryIndex`, `ChargeTarget`) is not persisted.

## Per-level tuning (`LevelEnemyTuning`)

`RunLevelEntry.EnemyTuning` is where a fight's real numbers come from. Resolution order:

```
template BaseStats  ->  x Difficulty  ->  x StatScales[]  ->  Overrides[]  =  what you fight
```

| Field | What it does |
|---|---|
| `Difficulty` | the one-number dial: scales **MaxHealth and Strength**. 1 = the template exactly |
| `StatScales` | per-stat multipliers on top, for a level that wants tanky *without* harder-hitting |
| `Overrides` | absolute per-enemy stats that win outright; only the stats listed are replaced |
| `XpMultiplier` / `GoldMultiplier` | so a harder floor pays more |

`LevelEnemyTuning.StatsFor` / `XpFor` / `GoldFor` are pure and tested (`LevelEnemyTuningTests`), with
static null-taking overloads so no caller has to null-check. Two rounding rules: a stat the template
gives a **positive** value never scales to zero, and a stat the template leaves at **zero** stays zero.

**How it reaches the game.** `DungeonManager` calls `EnemyManager.SetLevelTuning` before generation -
once, rather than threading it through every spawn - because `SpawnSingle` is also reached from a room
event waking something mid-level, and that caller has no idea which run it is in. `Enemy.Initialize`
stamps it and keeps it, so `Enemy.XpReward`/`GoldReward` follow the level too. **Null tuning means the
template as authored**, which is what free-play in the scene gets.

**Two authoring rules worth knowing**, both learned the hard way during the first tuning pass:

- **A boss must not ride the trash dial.** Scaling Mirefather's 74 HP by the level's multiplier made a
  21-turn slog. Every boss gets an `Overrides` row pinning it absolutely.
- **`Difficulty` is capped in practice by hero durability, not by taste.** It scales Strength, and
  `BalanceRules.MinHitsToKillHero` is 3, so past roughly **2.75** the squishiest hero drops below the
  floor - measured, by pushing a level to 3.55 and watching the Warrior fall to 2 hits.
- ~~Buy the rest of a level's difficulty with a `MaxHealth` scale.~~ **Do not.** That advice was
  wrong and every level in the project followed it, which is what produced enemies that were
  simultaneously *"no threat at all"* and *"takes too long to kill"*: health-only scaling buys danger
  out of fight length. Escalate with `Difficulty` and leave the health scales alone. Full reasoning in
  **`docs/BALANCING.md`**.

## Behaviors (`Behaviors/`) — authored data, not classes

**An enemy's repertoire is an `EnemyBehaviorSO`.** Until 2026-08-25 an `EnemyArchetype` selected one
of five hard-coded `IEnemyBehavior` classes whose every number was a compile-time constant, so two
enemies sharing an archetype were the same fight with different stats and a new kind of behaviour
meant writing a class. Those classes are **gone**; the archetype is now only a label.

```
EnemyBehaviorSO
  DisplayName, Archetype (label only)
  Actions: List<EnemyActionEntry>

EnemyActionEntry
  Kind          Attack | HeavyAttack | AoeAttack | Heal | Debuff | CastMagic
  Priority      higher tiers pre-empt lower ones entirely
  Weight        relative likelihood within a tier
  ChanceGate    independent chance the entry is considered at all (0 = no gate)
  Telegraphed   spend a turn winding up, then deliver (Heavy/Aoe only)
  Multiplier / Power / Duration / TargetStat / Magic
  Conditions    every one must hold
```

**Selection order is deliver, gate, priority, weight** (`EnemyActionPlanner.Plan`, pure and
roll-injected so combat, `EncounterSimulator` and the tests decide identically):

1. A telegraph already in flight **always** delivers. The player has been shown a wind-up, and
   swallowing it would make the telegraph a lie.
2. An entry is eligible only if every condition holds, its `ChanceGate` roll passes, **and the action
   has somewhere to land** — a Heal with nobody wounded must not win a turn and then do nothing.
3. The highest eligible `Priority` takes the turn outright.
4. `Weight` picks between entries tied at that priority. Nothing eligible means a plain swing, so a
   half-authored behaviour never wastes a turn.

Two knobs rather than one because the old behaviours needed both: a boss is priority logic ("cadence
up, so wind up the signature, else swing") while casting against attacking is a weighted coin flip.

**A telegraphed action is one entry, not two.** `Enemy.ChargingEntryIndex` (was a bare `IsCharging`
bool) records *which* action is in flight — with telegraphs authored per action, knowing that an enemy
is winding up no longer says what it is about to deliver.

**Conditions are a closed enum on purpose**: `SelfHealthBelow/Above`, `AllyWounded`,
`HeroMissingDebuff`, `EveryNthTurn`, `NotFirstTurn`. `BalanceMath` has to price a behaviour in closed
form — every danger and attrition number in the project comes from that — so a condition the analyzer
cannot reason about would silently opt an enemy out of being measured. **Adding a member means
teaching `EnemyBehaviorModel` its expected occupancy in the same change.**

### Presets — duplicate one to make a variant

`Assets/ScriptableObjects/Enemies/Behaviors/` holds `PresetAggressor`, `PresetBruiser`,
`PresetHealer`, `PresetDebuffer`, `PresetBoss` — code-built from `EnemyBehaviorSO.PresetActions` and
reproducing the original five archetypes **exactly**. Copy one and edit it; do not start from an empty
list. `EnemyBehaviorSO.BuiltInPreset` is the same list in code, cached per archetype, and is what an
enemy with no `Behavior` assigned falls back to — so nothing breaks half-way through authoring.

The presets are covered by `EnemyBehaviorTests` and `BossBehaviorTests`, whose assertions are the
*unchanged* ones from when each archetype was a class: 2.5x heavy, heal for 8, debuff 3 for 3 turns,
signature every 3 turns at x1.6, enrage below 30% tightening the cadence to 2 and blows to x1.5.
Those tests are the proof the migration changed nothing.

Each enemy also has its own `Behavior<Enemy>` asset — the archetype preset plus its `CastMagic`
action. That is the "one per enemy, duplicated from a preset" workflow in practice.

### Casting is an action, not a special case

`EnemySO.MagicCastChance` is **gone**. Cast frequency is a `CastMagic` entry with a `ChanceGate`
(`EnemyBehaviorSO.CastFromDrawList`), at a priority above the situational actions because that is what
the old pre-roll did — it was consulted before the behaviour, so a 20% caster cast 20% of the time even
with a wounded ally to mend.

- **Leave `Magic` empty** to draw from this enemy's own `DrawableMagics`, weighted by each entry's
  `CastWeight` — so what it throws is what you can steal from it. Name a magic for a signature the
  player cannot obtain.
- **Charges are never spent.** `DrawableMagicEntry.Charges` is the player's Draw grant.
- **`ChanceGate` 0 means "no gate", not "never"** — an enemy that should not cast simply has no
  `CastMagic` action. `CastFromDrawList` throws on a 0 chance rather than authoring that trap.
- **Spell power is the level's**: `LevelEnemyTuning.MagicPowerScaleFor` returns the level's
  `Difficulty`, passed to `EffectResolver.Execute` as `powerScale`. An enemy with an absolute
  `Overrides` row is exempt, exactly as its stats are — which is why boss casts read weaker than boss
  swings (tracked in `docs/NEXT_STEPS.md`).
- **No tags, no combos.** Enemy casts pass neither `MagicTagTracker` nor `ComboDetector`, so a cast
  resolves its effects and nothing more; combos carry player-facing discovery and upgrades.

### The intent icon only reports what is certain

`CombatManager.PredictIntent` calls `EnemyActionPlanner.PredictCertain`, which returns **null** unless
the answer is determined: a telegraph in flight, or a single ungated action that is the only thing the
enemy can do. Behaviours used to be deterministic so a preview was simply the decision; they are
probabilistic now, and an intent icon that guesses wrong teaches the player to distrust the telegraph —
the one tell the fight depends on.

### Inspector

`EnemyBehaviorSOEditor` draws only the fields each `Kind` actually reads, and lists the actions in the
order the planner resolves them. It also flags the two mistakes that make an action *dead* rather than
mistuned: an ungated, unconditional entry in the top tier (nothing below it can ever run), and
`Telegraphed` on a kind that cannot wind up.

## Bosses

- **`EnemySO.IsBoss`** flags a definition as a boss. It drives the boss-only combat/UI treatment: a larger crimson HP bar (`UnitHealthBar`), the no-flee rule + intro banner (`RoomActionUI`), and the run-complete/`Boss Slain!` victory copy. `Enemy.IsBoss` exposes it at runtime. Pair `IsBoss` with `Archetype = Boss` for the full effect.
- **Placement** is authored on `RunLevelEntry.BossEnemy` (see the Dungeon guide), *not* via spawn tables: `DungeonManager.PlaceBossIfConfigured` guarantees the boss in the exit room, clearing that room's rolled enemies first (`EnemyManager.ClearRoomEnemies` + `SpawnSingle`).
- **A boss need not be alone.** `RunLevelEntry.BossAdds` is an authored escort — guaranteed bodies placed after the boss, in the same sealed room. It was added because a lone boss made `MinBossToTrashRatio` unsatisfiable against dense trash without inflating the boss's stats, and an inflated boss is a *long* fight rather than a hard one. `CombatManager` and `RoomActionUI` already look for *a* boss among the room's enemies (`Any`/`FirstOrDefault` on `IsBoss`), so the banner, the no-flee rule, the crimson HP bar and the victory copy all work unchanged with a boss that has company.
- **The three bosses**, one per run: `AbyssalWarden` (Lightning, `TutorialRun`), **`Mirefather`** (HP 74, Shadow, resists Ice/Shadow and burns — `DrownedMarch`), **`GildedHoarder`** (HP 52, Normal, resists Lightning, **−75% Fire** because it is a wooden chest — `TheWarrens`, and repeatable, so it is the game's gold faucet at 95 Gold a kill).
- **A boss has to be proportionate to its level's trash**, not just survivable: `BalanceRegressionTests.BossesStandProportionateToTheirLevel` fails on a ratio outside **1.8–6.0×**, in *both* directions. The Mirefather first landed at 6.4× - the fix was giving its level hotter trash (`BlueRoom`) rather than inflating the boss. The ratio is measured on the **whole exit room** (boss + adds) against the level's average trash room, so adds are the third way to satisfy it, beside softening the boss and escalating the trash.
- **The boss room is judged against `MaxBossDanger` (1.40), not the 1.0 spawn-tail ceiling.** Its spawns are guaranteed, so its worst case *is* its expected case and it carries no information about a bad roll; `RunCurveModel.Aggregate` keeps it out of `PeakRoomDanger`/`PeakWorstCaseDanger` for that reason, and `EvaluateLevel` checks it against the climax ceiling instead. Before that split, spending danger on adds would have tripped a finding whose text ("a bad spawn roll here is unwinnable") and whose only suggestion ("lower SpawnChance") named a roll the exit room does not have.

## Sprites

Every `EnemySO` now has its **own** sprite. They did not: `AbyssalWarden`/`StoneSentinel` shared one, `BogShaman`/`EyeBall`/`HexWeaver` a second, and `CinderImp`/`Dragon` a third, which made three pairs of enemies visually indistinguishable in combat. `CinderImp`, `BogShaman` and `StoneSentinel` were drawn fresh (32×32 @ 32 PPU, matching the other trash), the two new bosses at 64×64 @ 64 PPU (matching `AbyssalWarden`), and `HexWeaver` took the already-shipped but unused `evil_wizard.png`. Convention: **trash 32px, bosses 64px, both one world unit**, `filterMode: 0`, `alphaIsTransparency: 1`.

## The bestiary — what the player has learned (`BestiaryPresenter`, `UI/`)

Resistances are **hidden until observed**, by design (see `docs/ELEMENTAL_PLAN.md`). This is the
machinery that makes that playable rather than a memory test.

- **The record** lives in `MetaProgressSaveData.Bestiary` — a `List<BestiaryEntry>` keyed by
  `EnemySO.SaveKey` — and the pure `BestiaryOps` holds its rules. See the Progression guide for the
  write path and why every mutator reports whether it changed anything.
- **Only observed *types* are stored, never the percentages.** The numbers are read back off the
  live `EnemySO` at display time, so retuning an enemy can never leave a save quoting figures the
  game no longer uses.
- **`BestiaryPresenter` is the single place that decides wording and tone**, and it is pure: it takes
  an `EnemySO` plus a `BestiaryEntry` (or null, meaning never met) and returns `BestiaryLine`s. Both
  surfaces render through it — the in-combat Inspect page (`MagicSelectionUI`) and the hub Bestiary
  (`UI/BestiaryUI`) — because duplicating "how do I phrase a 120% fire resistance" in two controllers
  is how two screens start disagreeing. `UI/BestiaryLineView` turns a line into a row and owns the
  colour, so the two cannot diverge there either.
- **The classification word comes from `DamageCalculator.Classify`**, not a second set of thresholds
  here, so the bestiary always says what the combat popup will say.
- **`BestiaryTone` is written from the player's side**: `Good` means a weakness to exploit, `Bad`
  means resisted/immune/absorbed or an element aimed at the party.
- **A zero stat earns a row only when the stat is one every unit is authored with**
  (`StatCatalog.AuthoringDefault > 0` — today Strength/Endurance/Agility). "END 0" is a finding worth
  acting on; "INT 0 / SPR 0 / LCK 0" on every melee enemy is noise. Reading it off the catalog rather
  than a hard-coded list means a stat added later sorts itself.
- **The Draw list is gated too, on `MetaProgressManager.IsMagicDiscovered`** — the same permanent
  record the Forge's collection grid reads, which means "drawn at least once, from any enemy". An
  undrawn offering reads `???`, but its **charge count still shows**: what a draw is worth is the
  decision the player is making with their turn; what it is called is the reward for making it.
  The lookup is passed in as a `Func<string, bool>` so the presenter stays pure, and
  `BestiaryPresenter.IsDrawKnown` treats a missing lookup as *not* discovered — a call site that
  forgets to pass the record should over-hide, never leak.
  - **The in-combat Draw picker masks the same entries** (`MagicSelectionUI.PopulateDrawChoiceRows`,
    name and icon both). It has to: a gate on the knowledge pages that the player walks around by
    opening Draw and backing out is decoration. This *does* change play — the first pull off a new
    enemy is a small blind gamble on a turn — which is the FFVIII shape: drawing is the discovery.
    (An earlier version of this guide argued the opposite, that gating here would contradict the
    picker. The resolution was to move the picker, not to drop the gate.)

### `EnemyCatalogSO` — the bestiary's denominator

`Assets/Resources/EnemyCatalog.asset` lists every `EnemySO` in the game, loaded from Resources so the
hub `MenuScene` (which wires no combat managers) can render the screen. It is what "N of M
discovered" counts against, and an enemy missing from it is **invisible in the bestiary however often
it is fought** — the same silent-gap failure that once shipped `MagicComboCatalog` with a duplicate
and a missing combo. `BestiaryTests` fails on a null entry, a duplicate key, or an `EnemySO` asset
that is not listed.

**Edit the asset itself.** Unlike `MagicCatalog` this is a ScriptableObject rather than a prefab
instance, so it has no override trap — but growing the list by size still leaves nulls, and the test
catches that.
