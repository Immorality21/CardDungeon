# Revisits — re-clearing a run at a difficulty the player picks

Cleared runs come back as repeatable content, at a difficulty chosen at the run's start, made of
conditions and limits. Opened 2026-10-06 after a gameplay-loop review.

> **Reads with:** [NEXT_STEPS.md](../NEXT_STEPS.md) (the index, and the **do-not-relitigate** list — check it before reopening anything here) · [Specialization](SPECIALIZATION.md) · [Combat Depth](COMBAT_DEPTH.md) · [Hub](HUB.md) · [Balance Open](BALANCE_OPEN.md) · [Polish Content](POLISH_CONTENT.md)

> **The campaign is not finished.** Today's seven runs are playtest content, and the real campaign
> will be much longer. Everything here is about the *shape* of the loop. Do not size it against
> today's run count.

---

### Why — what the loop review found (2026-10-06)

- **Replay was inverted.** A cleared run closes for good (`Repeatable` is set only on The Warrens),
  but a run still in progress replays its early floors after every death. The run you have not
  beaten was the best farm, and clearing it reduced income.
- **Build content outruns XP supply.** One grid is ~5,200 XP and a playthrough pays a fielded hero a
  fraction of that. With eight heroes and a summon per branch, most of the grid content had no
  renewable source except a tier-1, two-floor run.
- **Benched and late heroes had nowhere to play.** XP goes only to the fielded party. The Warlock
  and the Cultist are rescued at the end of the content that exists today.
- **The Warrens boss was an infinite Void Shard tap.** Fixed the same day: the Gilded Hoarder no
  longer drops Void Shard, and The Gilded Vault (the Hollow Vault floor, a one-time run) guarantees
  the 1–2 instead.

### Decided (owner, 2026-10-06)

- **Some runs are re-clearable: for now every run but the tutorial, the challenge run and the secret
  runs.** Which ones is a per-run authoring choice (`RunDefinitionSO.Repeatable`), and
  `RevisitTests.RevisitableRuns_AreEverythingButTheTutorialChallengeAndSecretRuns` pins the list.
  Today that means The Drowned March, The Warrens and The Ashen Deep.
- **Composable, like Hades’ Heat.** The player toggles conditions; the total sets the reward.
- **Every revisit starts at +50% to every enemy stat** — health, Strength, Endurance, Agility, Intelligence,
  Spirit, Luck, plus the base power of its spells (owner, revised the same day: "stats affect everything";
  it was +50% health and damage until then). A stat the enemy lacks stays 0. Conditions go on top.
- **New conditions must be easy to add.** Examples the owner gave: no healing, more enemies.
- **It is called the Fear level** (owner, 2026-10-06): "something unique to us", not Hades' Heat. Code
  says `RunFear` / `RunFear.Level`, the asset `FearPerRank`, the save `BestFear`.
- **Revisits pace the campaign** (owner, 2026-10-06): from the moment a run can be replayed, the content
  after it is tuned harder, so revisiting is how a player catches up. `BALANCING.md` §0 rule 6 is the
  guideline; `RevisitModel` + `EvaluateRevisits` measure it (§5ab).
- **Rewards are Essence plus gold, XP and items.** Essence becomes the revisit's own reward (HUB.md §3c
  direction 2). That also sets up a **player guide around Essence later**: finish the tutorial, then
  the next run, then the game suggests replaying one and a guide explains what that grants.

### Built (2026-10-06) — the first version

| piece | where |
|---|---|
| the resolved numbers, read by combat and spawns | `Combat/RunFear` (`RunFear.Current`, set by `DungeonManager.ApplyRunFear` on every level build) |
| the rules asset: base, reward curve, scarce items, conditions | `Dungeon/Revisits/RevisitRulesSO` → `Resources/Revisits.asset` |
| a condition, its effects, a saved choice | `Dungeon/Revisits/RunModifier` (`RunModifier`, `RunModifierEffect`, `RunModifierEffectKind`, `RunModifierSelection`) |
| the pure rules: resolve, scarcity, picker edits | `Dungeon/Revisits/RevisitOps`, covered by `RevisitTests` |
| the picker | `CampaignMapUI`: **Revisit…** on a cleared run opens the conditions in the detail column (Up/Down walk, Left/Right rank, Enter begins, Esc back; a click cycles a rank) |
| what a run carries | `RunSaveData.IsRevisit` + `Modifiers`, written by `HubManager.OnRunChosen` |
| the record | `MetaProgressSaveData.RevisitRecords` (best Fear level, clears), `MetaProgressManager.RecordRevisitFear` |

**What each piece of the Fear level touches:**
- **Enemy health:** `Enemy.Initialize`, *after* the level's tuning, so absolute boss overrides scale too.
- **Enemy damage:** raw damage in `CombatManager.ExecuteAttack` and `DamageEffectExecutor`. Damage-over-time ticks are **not** scaled yet, because a debuff's magnitude is a stat delta as often as a tick.
- **Extra enemies:** `EnemyManager.AddRevisitExtras` draws from the room's own spawn table, only in a room that already rolled a fight, and caps at `EnemyFormation.DesignMax` (5). The boss room is rebuilt afterwards, so it gets none.
- **Hero healing:** heal spells, regeneration ticks, Drain, potions and refuge rests. Not the full heal at the start of a floor.
- **Rewards:** XP and gold per kill (`Enemy.XpReward`/`GoldReward`), and the level-clear gold bonus and Essence (`AwardLevelClear(gold, essence)`). **Items are not scaled.**
- **Scarce materials** (`RevisitRulesSO.NewBestFearOnly`, Void Shard today): a revisit drops them only when its Fear level beats the run's best cleared Fear level. The first revisit always pays them. This applies to enemy loot, caches and guaranteed materials alike. It is what kept "make the Drowned March repeatable" from reopening the leak the Warrens had.

**Enemy damage-over-time** (2026-10-06): a poison or burn an enemy lays on has its authored base
power scaled like its spells (`RunFear.ScaleEnemyOverTimePower`, in `DebuffEffectExecutor`). The
caster's stat part already rides the scaled stats. The damage condition (Sharpened Blades) now works
through the stats too (Strength, Intelligence and spell base power), not as a per-hit multiplier, so
the balance model sees it.

**The balance model knows revisits** (2026-10-06): the Fear level rides on the enemy tuning
(`LevelEnemyTuning.WithFear`), the same call in the game and in `RunCurve.Build(..., fear)`.
`RevisitModel` reports what each revisit pays, and judges the runs after a replayable one against the
pacing guideline. See `BALANCING.md` §5ab for today's numbers.

**Adding a condition:** if it uses an existing kind, add a list entry in `Revisits.asset`. A new kind
of effect is one `RunModifierEffectKind` member (append only: it is serialized by ordinal), one case
in `RevisitOps.Resolve`, one field on `RunFear`, and whatever reads that field.

**First-draft numbers** (not measured): base +50% to every stat (`BaseEnemyStatPercent`), rewards +25% at Fear level 0 and +10% per Fear level.
Hardened Foes (+25% health ×3, 1 Fear level each), Sharpened Blades (+25% damage ×3, 1 Fear level each),
Quickened Foes (+25% Agility ×2, 2 Fear level each — added after the first playtest, finding 12), Swarming
Halls (+1 foe ×2, 2 Fear level each), No Respite (−50% healing ×2, 2 Fear level each). The maximum is Fear level 18,
which pays +205%. The conditions multiply on top of the base: Hardened Foes rank 2 is 1.5 × 1.5 = 2.25×
the floor's health. Rounding is to the nearest whole point, so a small stat can move by more than 50%
(Endurance 1 → 2, Agility 5 → 8).

Verified 2026-10-06 in play mode, against a throwaway save folder: picker by keyboard, `Run.json`,
the entry screen ("Revisit, Fear level 6"), and a live floor where enemy health was ×2, every fight had
its extra foe, and XP per kill was ×1.85. Suite 1,329 / 0.

**The first playtest** (2026-10-06, `docs/playtest-revisit/TODO.md`) found Fear level 0 trivial for the party
that had just cleared the run, and nothing tying the revisit or a loss to the sphere grid. Fixed the same
day: the picker defines Fear level and names the Hall of Progression, the entry screen spells out the Fear level
(`RevisitOps.Summary`), the run-complete screen names the Fear level and the bonus instead of re-listing
unlocks, a cleared run says whether it can be revisited and its best Fear level, the defeat screen points at
the banked XP and the Hall, and the in-combat Inspect page shows what the revisit added to each enemy.
Finding 12 (Fear level 0 trivial because speed never rose) was answered by the owner: the base became +50% to
**every** stat, Agility included.

### Still to do

- **The Essence guide** (owner, 2026-10-06): after the tutorial and the next run, point the player at a
  revisit and explain what it grants. That belongs with `docs/TUTORIAL.md`, and it waits on §3c settling
  what Essence buys.
- **More condition kinds:** elites, a starting affliction, a smaller party, no refuges, no summons/Ultra,
  a required or banned hero (or leave that last one to bounties, HUB.md §3d).
- **Decide whether items scale** (more rolls, or better rarity at higher Fear levels).
- **The model's revisit judgement uses the closed form.** Switch it to the floor simulator when
  `Simulate` is on, model Swarming Halls, and measure the Hollow Vault from a party that has done both
  its prerequisites (`BALANCING.md` §5ab).
- **XP taper** for trivial revisits (open question 5 below).

### The original proposal (kept for its reasoning; superseded where the sections above differ)

**Story pays once; a revisit pays in resources.** Captives are skipped once owned, which already
happens. `JoinsOnClear` and run-completion unlocks fire on the first clear only, and
`CompletedRunKeys` is unchanged. A revisit pays XP, gold, loot and the run's materials, all scaled
by its difficulty.

**Two kinds of condition.**

| kind | examples | existing seam |
|---|---|---|
| **raises** — the dungeon gets harder | enemy scaling (+Difficulty), extra boss adds, an elite per floor, a level affliction from the start | `LevelEnemyTuning.Difficulty`, `RunLevelEntry.BossAdds`, `LevelAfflictionTracker` |
| **limits** — the party gets less | party cap 2 or 3, no potion belt, no refuges, half ability charges, no summons / no Ultra, a hero required or banned | `PartySlots`, belt cap (`PartyResourceManager`), `RestRooms` / `RoomKindPlanner`, `EquippedMagicState` charges |

**Recommended shape: composable conditions with a Fear level score** (as in Hades' Pact of Punishment).
Each condition is worth points. The player toggles conditions on the run screen, and rewards scale
with the total. The alternative is a fixed authored ladder per run (Normal / Hard / Brutal …), which
is simpler to build and balance but less interesting to choose. A middle option: a ladder of named
tiers, each a preset of conditions, with free composition unlocked later (a hub building level —
*access*, which is legal under phase 6's rule).

**Rewards.**
- XP, gold and material quantity scale with Fear level.
- **The scarce materials pay on the first clear of each Fear level threshold**, not every clear. That makes
  Void Shard a reward for doing something harder, and keeps its supply bounded rather than grindable.
- Clearing a threshold unlocks the next one (best Fear level cleared is recorded per run in
  `MetaProgressSaveData`).

**Unchanged.** Death works as §3b says (forfeit the floor, restart the run, and dying never costs
more). Only one run may be in progress at a time. Materials still gate on *where* you have been,
because a revisit has to go to that biome.

### Open questions

1. **Composable Fear level, a fixed ladder, or the ladder first and composition later?**
2. **Which runs are re-clearable?** All but the tutorial? Not the challenge runs? Not the secret runs?
3. **Do limits that force a hero choice** (required or banned hero, party cap) **belong here or
   in bounties** (HUB.md §3d)? Bounties are the natural home for "bring this hero".
4. **Does a revisit roll a fresh seed?** Procedural floors do. Hand-drawn floors (The Blood Stair,
   The Reedcage) replay identically, so they may want conditions that change them more.
5. **Should XP at low Fear level taper** once the party is far stronger than the run, so farming a trivial
   run does not out-earn a fair fight? (Watch out: §3b forbids paying less for dying, not for easy
   content.)

### Touch points

`RunDefinitionSO` (`Repeatable`), `CampaignOps` (start/continue, revisit state), `CampaignMapUI`
(the condition picker in the detail column), `RunSaveData` (the chosen conditions travel with the
run), `MetaProgressSaveData` (best Fear level per run), `DungeonManager` (first-clear vs revisit rewards,
captive/join skips), `MetaProgressManager` awards, and the balance model: `RunCurveModel` has no
notion of an *n*-th attempt or a Fear level level (`BALANCE_OPEN.md` §3b).
