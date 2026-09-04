# Polish, information and content

Everything not on a live thread: battle and room polish, the information the player never gets, and the content-volume gap.

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index, and the **do-not-relitigate** list — check it before reopening anything here) · [Specialization](SPECIALIZATION.md) · [Combat Depth](COMBAT_DEPTH.md) · [Hub](HUB.md) · [Balance Open](BALANCE_OPEN.md) · [Polish Content](POLISH_CONTENT.md)

---

### 1. Battle polish — remaining follow-ups

Tiers 1–4 shipped (audio + music bed + volume options; turn indicator, idle motion, projectiles;
crits, resistance popups, boss telegraphs, combo flourish; victory/defeat framing, zoom-punch,
per-level backdrops). What is left:

- **A dedicated heal/buff flourish.** Heals still just show green rising text.
- **Element-tinted damage numbers** per `DamageType`, and richer per-element cast visuals. Pair with
  the colourblind check in §19.
- **A bigger on-screen combo banner** beyond the floating name.
- **True desaturate on defeat** needs post-processing.
- **Real per-biome background art** — `LevelDefinitionSO.CombatBackground` is wired and mostly unset.
- **Per-boss music.** A boss theme is game-wide; per-boss would want a field on `RunLevelEntry`
  beside `BossAdds`.
- **Dedicated combat SFX.** The current clips are repurposed interface foley.
- **All motion is procedural.** No Animator anywhere — lunge/flash/shake/floating text via
  `CombatFeedback` + `EffectPresenter`. Sprites are otherwise frozen: **no hit reaction on the
  receiving unit and no death animation.**
- **Hub screens toggle instantly.** Every view swap in `MainMenuManager` is a `display` flip; the
  theme stylesheet already drives the whole game's look from one file, so transitions belong there.

Touch points: `Assets/Scripts/Combat/CombatFeedback.cs`, `Assets/Scripts/Cards/EffectPresenter.cs`,
`Assets/Scripts/Rooms/CombatManager.cs`, `Assets/Scripts/Combat/UI/UnitHealthBar.cs`,
`Assets/Scripts/Combat/CombatStage.cs`, `Assets/Scripts/Audio/`, `Assets/UI/Theme/CardDungeon.uss`.

### 2. Room variety — the branching half has not shipped

`RoomKind` (Combat / Connector / Treasure / Rest) and stat-driven room events both shipped. Members
exist only when they *do* something — an enum entry no code acts on is the dead content this project
keeps finding. Open:

- **Path/branch choice at generation** (`RoomManager`) so the player picks *which* rooms to enter,
  trading safety for reward. Untouched — **this is the half that makes a level a route rather than a
  sweep**, and it is the largest open item in §2. It also depends on §14's map: a fork the player
  cannot see is not a choice.
- **Elite, Merchant and Shrine kinds.** Elite wants a danger multiplier and a loot table to justify
  it; Merchant wants an in-run shop screen (the hub Merchant is not reusable as-is); a Shrine is a
  refuge with a cost, which `HealthCost` now makes authorable.
- **Marker art.** Both markers are the exit-room sprite under a tint (gold / teal).
- **No per-room kind or event in a manual layout.** `ManualRoomEntry` has neither field, so a
  hand-authored level takes the level's quotas at random like a generated one.
- **The refuge is nearly single-instance content.** Only Upper Halls is long enough to earn one under
  the shipped rule, so `RoomKind.Rest` is reachable on one floor of one run.
- **An event behind a fight is never a decision *before* the fight** — the main bar replaces the
  Fight bar, so a room's event only appears once the room is won. Worth a look if events should ever
  be a way to *avoid* a fight.
- **Nothing stops the same event appearing twice in one level** since the per-level budget was
  dropped for per-event odds. Uncommon at current numbers, but no longer impossible.
- **Only `MustyTome` and `TreasuryHoard` have outcome weight modifiers authored.** The rest are 0 —
  a balancing pass, not a code change.

Touch points: `Assets/Scripts/Rooms/RoomManager.cs` (branch choice), `Assets/Scripts/Rooms/RoomSO.cs`,
`Assets/Scripts/Rooms/UI/RoomActionUI.cs`, `Assets/Scripts/Items/LootRoller.cs`.

### 6. Stats — one open note

The six-stat model, `StatCatalog`, generic `StatBlock` and spell scaling all shipped. One structural
note remains:

- **`BuffType` is a second per-stat list.** Adding a stat is one `StatType` member plus one
  `StatCatalog` row — *except* that a stat which should be buffable also needs a `BuffType` member,
  because `BuffHandlerRegistry` generates handlers from it and silently skips a stat with no match.
  `BuffHandlerRegistry.StatsWithNoBuffType()` reports the gap and a test asserts it is empty.
  **Collapsing `BuffType` into `Kind + StatType` would remove the exception** and rewrites every
  magic and combo asset, so it stays a separate change. §9 adds several non-stat `BuffType` members,
  which makes this collapse *more* attractive, not less — read §9 before attempting it.

### 8. Migrate to the new Input System — *nice to have*

> **Status: considered and deferred (2026-09-01).** Nothing is broken by staying on legacy input, so
> this earns its place only if gamepad support or rebindable controls become a goal (see §19, which
> argues its value is understated).

**Where the project is.** Entirely on the legacy Input Manager, uniformly. Both scenes carry
identical EventSystem GameObjects with `StandaloneInputModule`, `com.unity.inputsystem` is absent
from `Packages/manifest.json`, `ProjectSettings.asset` has `activeInputHandler: 0`, and no script
references `UnityEngine.InputSystem`.

**What it would take.** Four steps: add the package; set Active Input Handling (needs an editor
restart); swap both scenes to `InputSystemUIInputModule`; rewrite five call sites —
`MainCamera.cs:185-198` (WASD pan) and `MenuManager.cs:35` (Escape).

**The one trap.** `Door.cs:76` uses `OnMouseDown()`, and Unity only sends `OnMouseXXX` under the
legacy backend — switching to *New only* silently kills mouse navigation through dungeon doors, with
no error. Either set Active Input Handling to **Both**, or convert `Door` to a `Physics2D.Raycast`.
Prefer the conversion: the doors are the only world-space mouse input in the game, and "Both" leaves
a hidden dependency on a backend nobody thinks is in use.

**What is *not* at risk.** The keyboard cursor. UI Toolkit's key path does not go through the input
module at all — `PanelEventHandler.Update()` reads keys off the IMGUI queue via `Event.PopEvent`,
gated only on `isCurrentFocusedPanel`, which is exactly what `PanelKeyboard.Claim()` arranges. So
`PanelKeyboard`, `KeyboardNavigator` and every screen built on them survive a module swap untouched.

Touch points: `Packages/manifest.json`, `ProjectSettings/ProjectSettings.asset`, both scenes'
EventSystem, `Assets/Scripts/ImmoralityGaming/Fundamentals/MainCamera.cs`,
`Assets/Scripts/ImmoralityGaming/Menu/MenuManager.cs`, `Assets/Scripts/Rooms/Door.cs`.

---

## Information the player never gets *(added 2026-09-03)*

### 14. The dungeon map, the party bar, and the pause menu

Three separate gaps, grouped because a pause overlay is the natural home for the first two.

**14a. There is no dungeon map.** Rooms are a graph (`RoomNode`), doors are the only navigation, and
nothing in `Assets/Scripts` draws an overview. The player cannot answer *where is the exit*, *have I
searched everything*, or *is this branch a dead end*. Two consequences:

- It is a prerequisite for **§2's branch choice** — a fork the player cannot see is not a fork.
- It is the only place the run's *shape* is legible, which is what makes a 17-room beeline (§0g) read
  as a decision rather than a corridor.

`Room.Reveal()` already shows the current room's doors and leaves unexplored neighbours dark, so the
knowledge model exists; what is missing is a view of it. `SphereGridView` is a node-graph renderer
that already does pan/zoom and Painter2D edges over exactly this shape — unlike §7's painted town,
**a dungeon map is genuinely the same widget**, so this is the one place reusing it is right.

**14b. Party health is invisible while exploring.** `party-status` in `RoomAction.uxml` is shown by
`ShowCombat` and hidden by `HideAll`, so the panel exists and is deliberately combat-only. But since
the charge/health rework made health a **level-scoped** resource, the whole time the player is
walking the floor — deciding whether to take a fight, spend the refuge, or drink a potion — they
cannot see how hurt anyone is. **This is the single decision the game most wants informed, and it is
made blind.** Small fix, disproportionate payoff; it is the cheapest item in this document.

**14c. There is no in-dungeon pause menu.** Volume can only be changed in the hub, and there is no
quit-to-hub mid-run. A pause overlay is the home for both, plus 14a and 14b, plus §19's
motion-reduction toggle.

Touch points: `Assets/UI/Rooms/RoomAction.uxml`, `Assets/Scripts/Rooms/UI/RoomActionUI.cs`,
`Assets/Scripts/Rooms/RoomManager.cs` / `RoomNode.cs`, `Assets/Scripts/Heroes/UI/SphereGridView.cs`
(reused as the map renderer), `Assets/Scripts/Audio/AudioOptions.cs`,
`Assets/Scripts/ImmoralityGaming/Menu/`.

### 15. Run summary and statistics

`MetaProgressSaveData` records Gold, Essence, upgrades, the Bestiary and completed runs — **nothing
about how a run went.** The death screen says *"Your Party Has Fallen..."* and stops.

§3b's entire design bets on death being **tuition**, but the game teaches nothing at the moment of
death. A summary that says *floor 3 of 4, killed by Mirefather, 47 enemies felled, you never drew
Ward, you have 340 gold and 4 unspent XP* is what converts a wipe into a hub decision — which is the
loop the whole balance thread is built around.

It is also **free telemetry**. §0g's frontier numbers are all model predictions with no measured
counterpart; a per-run record of *floors reached at what party width and what spent XP* is exactly
the observation that would validate or falsify them. `SaveAudit` already reads live saves for the
analyzer, so the consumer exists.

Scope suggestion: a `RunHistorySaveData` (a bounded list — last N runs) written on death and on
completion, surfaced as (a) an expanded death/victory screen and (b) a hub **Records** view. Keep it
out of `MetaProgressSaveData`, which is already doing several jobs.

Touch points: `Assets/Scripts/Progression/` (new save type + manager hook),
`Assets/Scripts/Dungeon/DungeonManager.cs` (`HandlePartyDeath`, `OnDungeonCleared`),
`Assets/Scripts/Rooms/UI/RoomActionUI.cs` (death/victory windows),
`Assets/Scripts/MainMenu/MainMenuManager.cs`, `Assets/Scripts/Balance/SaveAudit.cs`.

### 16. A compendium — explain the systems

Seven stats, five damage types, resistances, fourteen magic tags, combos, charges, upgrade levels —
and **no player-facing place that explains any of it.** `StatCatalog` already holds a `Description`
per stat and nothing displays it. Nobody is told that Luck drives crit, that Spirit scales healing
and protection, or that resistance applies *before* defense.

The Bestiary proved the pattern (a hub collection screen fed by a pure presenter), so this is mostly
authoring plus a screen. Consider folding it into the Bestiary as a second tab rather than an
eleventh home button — home is already at 88% height with room for about one more button.

Touch points: `Assets/Scripts/UnitStats/StatCatalog.cs` (descriptions exist, unused),
`Assets/Scripts/Enemies/UI/BestiaryUI.cs` (pattern + plausible host),
`Assets/UI/MainMenu/MainMenu.uxml`, `Assets/Scripts/Cards/MagicTag.cs`,
`Assets/Scripts/Combat/DamageType.cs`.

---

## Content and production *(added 2026-09-03)*

### 17. Content volume is the biggest single gap

The content-to-systems ratio is the scan's headline finding. Current catalog:

| | count | note |
|---|---|---|
| Magic | 15 + 4 combos | across a 5-run campaign |
| Heroes | 4 | differing only in stats and grid (§13) |
| Enemies | 11 + 2 filler | 8 non-boss enemies sit in a 4× danger band |
| Items | 18 | one `SlotType` (`Hands`) only just filled |
| **Room events** | **6** | **one per stat — they repeat inside a single run** |
| Room templates | 18 | |

**Room events are the worst of these.** They are the main flavour beat and the only non-combat
decision in a floor, and a 4-floor run at ~2 event rooms per floor will show the player most of the
catalog twice. `RoomEventSO` is fully data-driven and the authoring surface is mature (spawn odds,
stat gates, checks, weighted outcomes, level afflictions) — this is pure content work with no code
behind it.

The Elements & Unlocks analyzer tab already flags *unreachable* content; the counterpart finding —
**thin** content — has no check. Worth adding target counts per tier to `BalanceRulesSO` so the tool
reports it, rather than adding content ad hoc.

Also thin and worth naming: **no run declares its own room-event pool**, so every biome draws on the
same six.

### 18. Item and consumable depth

**18a. Gear is a flat stat stick.** `ItemSO` is `Bonuses` (Raw or Percentage) + `Resistances`. No set
bonuses, no on-hit procs, and — most importantly — **no trade-off items.**

Now that §5p/§5q made gear a real balance axis (the frontier's third), *"+6 Strength, −3 Agility"* is
the cheapest possible way to turn gear from a checklist into a choice: it needs no new field at all,
just a negative `ItemBonus`, and it immediately interacts with the CTB turn order. Verify the
frontier's `GearLoadout` greedy spend handles negative bonuses sensibly before authoring any — a
greedy ranker that sums weighted stats will handle it correctly, but it has never been given one.

Set bonuses and procs are larger and want their own decision; procs in particular need a hook point
in `DamageCalculator`/`CombatManager` that does not exist.

**18b. Consumables.** `CureStatus` shipped with §9; `RestoreToFull` and `Revive` are still missing and
pair with §3's death safety-net sink.

**The bigger gap is that consumables cannot be bought at all.** `MerchantUI.GenerateStock` filters
`i.Category == ItemCategory.Equipment`, so every consumable in the game reaches the player only
through a loot roll or a room event. That makes the potion belt and the new Antidote Salve pure luck
rather than preparation — and preparation is exactly what §3 wants more Gold sinks for. A consumables
tab on the Merchant is a small change with a direct line to the retry economy (§3b): restocking before
a re-attempt is the most natural gold sink the game could have.

Touch points: `Assets/Scripts/Items/ItemSO.cs` / `ItemBonus.cs` / `ConsumableEffectType.cs`,
`Assets/Scripts/Items/LootRoller.cs`, `Assets/Scripts/Balance/GearLoadout.cs`,
`Assets/Scripts/Rooms/CombatManager.cs` (consumable use path).

### 19. Shipping surface

Small individually; collectively this is what stands between the project and a build someone else can
play.

- **No quit and no credits.** No `Application.Quit` call anywhere and no quit button in
  `MainMenu.uxml`. A Windows standalone build can currently only be closed with Alt+F4.
- **No graphics or accessibility options.** Options is audio-only. Notably the game now has camera
  shake, zoom-punch and hit-stop with **no way to turn them off** — that is an accessibility need,
  not a preference. Pair the motion-reduction toggle with §1's element-tinted damage numbers, which
  want a colourblind check at the same time. Resolution/fullscreen and text size belong here too.
- **No save management.** One save, no slots, no reset-progress, no NG+. Starting over means
  hand-deleting `savedata/`. `FileHandler` writes to a fixed directory, so slots would be a path
  prefix.
- **Gamepad is closer than §8 implies.** `DirectionalNav` + `KeyboardNavigator` already make every
  screen cursor-navigable by design — that is the hard part, and it is done. §8 prices the migration
  as "nice to have"; for gamepad specifically the remaining work is mostly a mapping job on top of
  architecture that already exists, so **§8's value is understated.**
- **No CI.** The EditMode suite is ~813 tests and runs headlessly in about a second
  (`ExecutionSettings.runSynchronously`, `docs/GAMEPLAY_VALIDATION.md` gotcha 12). A pre-commit hook
  or CI step is nearly free and would have caught the 46-failure rot described in the ledger before
  it reached 46.
- **Localization.** All strings are hardcoded in C# and UXML. Not urgent — but the cost grows
  linearly with §17, so it is a *decide now, do later* note: if strings are ever going to move to a
  table, the cheapest moment is before the content pass, not after.

---
