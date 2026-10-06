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
- **Composable, like Hades' Heat.** The player toggles conditions; the total sets the reward.
- **Every revisit starts at +50% enemy health and damage.** That is the base, and conditions go on top.
- **New conditions must be easy to add.** Examples the owner gave: no healing, more enemies.
- **Rewards are Essence plus gold, XP and items.** Essence becomes the revisit's own reward (HUB.md §3c
  direction 2). That also sets up a **player guide around Essence later**: finish the tutorial, then
  the next run, then the game suggests replaying one and a guide explains what that grants.

### Built (2026-10-06) — the first version

| piece | where |
|---|---|
| the resolved numbers, read by combat and spawns | `Combat/RunHeat` (`RunHeat.Current`, set by `DungeonManager.ApplyRunHeat` on every level build) |
| the rules asset: base, reward curve, scarce items, conditions | `Dungeon/Revisits/RevisitRulesSO` → `Resources/Revisits.asset` |
| a condition, its effects, a saved choice | `Dungeon/Revisits/RunModifier` (`RunModifier`, `RunModifierEffect`, `RunModifierEffectKind`, `RunModifierSelection`) |
| the pure rules: resolve, scarcity, picker edits | `Dungeon/Revisits/RevisitOps`, covered by `RevisitTests` |
| the picker | `CampaignMapUI`: **Revisit…** on a cleared run opens the conditions in the detail column (Up/Down walk, Left/Right rank, Enter begins, Esc back; a click cycles a rank) |
| what a run carries | `RunSaveData.IsRevisit` + `Modifiers`, written by `HubManager.OnRunChosen` |
| the record | `MetaProgressSaveData.RevisitRecords` (best heat, clears), `MetaProgressManager.RecordRevisitHeat` |

**What each piece of the heat touches:**
- **Enemy health:** `Enemy.Initialize`, *after* the level's tuning, so absolute boss overrides scale too.
- **Enemy damage:** raw damage in `CombatManager.ExecuteAttack` and `DamageEffectExecutor`. Damage-over-time ticks are **not** scaled yet, because a debuff's magnitude is a stat delta as often as a tick.
- **Extra enemies:** `EnemyManager.AddRevisitExtras` draws from the room's own spawn table, only in a room that already rolled a fight, and caps at `EnemyFormation.DesignMax` (5). The boss room is rebuilt afterwards, so it gets none.
- **Hero healing:** heal spells, regeneration ticks, Drain, potions and refuge rests. Not the full heal at the start of a floor.
- **Rewards:** XP and gold per kill (`Enemy.XpReward`/`GoldReward`), and the level-clear gold bonus and Essence (`AwardLevelClear(gold, essence)`). **Items are not scaled.**
- **Scarce materials** (`RevisitRulesSO.NewBestHeatOnly`, Void Shard today): a revisit drops them only when its heat beats the run's best cleared heat. The first revisit always pays them. This applies to enemy loot, caches and guaranteed materials alike. It is what kept "make the Drowned March repeatable" from reopening the leak the Warrens had.

**Adding a condition:** if it uses an existing kind, add a list entry in `Revisits.asset`. A new kind
of effect is one `RunModifierEffectKind` member (append only: it is serialized by ordinal), one case
in `RevisitOps.Resolve`, one field on `RunHeat`, and whatever reads that field.

**First-draft numbers** (not measured): base +50% / +50%, rewards +25% at heat 0 and +10% per heat.
Hardened Foes (+25% health ×3, 1 heat each), Sharpened Blades (+25% damage ×3, 1 heat each),
Swarming Halls (+1 foe ×2, 2 heat each), No Respite (−50% healing ×2, 2 heat each). The maximum is
heat 14, which pays +165%.

Verified 2026-10-06 in play mode, against a throwaway save folder: picker by keyboard, `Run.json`,
the entry screen ("Revisit, heat 6"), and a live floor where enemy health was ×2, every fight had
its extra foe, and XP per kill was ×1.85. Suite 1,329 / 0.

### Still to do

- **The Essence guide** (owner, 2026-10-06): after the tutorial and the next run, point the player at a
  revisit and explain what it grants. That belongs with `docs/TUTORIAL.md`, and it waits on §3c settling
  what Essence buys.
- **The picker needs a scroll** once there are more than about six conditions. The detail column fits
  four with room for two more.
- **More condition kinds:** elites, a starting affliction, a smaller party, no refuges, no summons/Ultra,
  a required or banned hero (or leave that last one to bounties, HUB.md §3d).
- **Scale damage-over-time** from enemies, and decide whether **items** scale (more rolls, or better
  rarity at high heat).
- **The balance model knows nothing about heat.** `RunCurveModel` has no revisit; measure what a party
  that cleared a run can take before tuning the numbers above.
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

**Recommended shape: composable conditions with a heat score** (as in Hades' Pact of Punishment).
Each condition is worth points. The player toggles conditions on the run screen, and rewards scale
with the total. The alternative is a fixed authored ladder per run (Normal / Hard / Brutal …), which
is simpler to build and balance but less interesting to choose. A middle option: a ladder of named
tiers, each a preset of conditions, with free composition unlocked later (a hub building level —
*access*, which is legal under phase 6's rule).

**Rewards.**
- XP, gold and material quantity scale with heat.
- **The scarce materials pay on the first clear of each heat threshold**, not every clear. That makes
  Void Shard a reward for doing something harder, and keeps its supply bounded rather than grindable.
- Clearing a threshold unlocks the next one (best heat cleared is recorded per run in
  `MetaProgressSaveData`).

**Unchanged.** Death works as §3b says (forfeit the floor, restart the run, and dying never costs
more). Only one run may be in progress at a time. Materials still gate on *where* you have been,
because a revisit has to go to that biome.

### Open questions

1. **Composable heat, a fixed ladder, or the ladder first and composition later?**
2. **Which runs are re-clearable?** All but the tutorial? Not the challenge runs? Not the secret runs?
3. **Do limits that force a hero choice** (required or banned hero, party cap) **belong here or
   in bounties** (HUB.md §3d)? Bounties are the natural home for "bring this hero".
4. **Does a revisit roll a fresh seed?** Procedural floors do. Hand-drawn floors (The Blood Stair,
   The Reedcage) replay identically, so they may want conditions that change them more.
5. **Should XP at low heat taper** once the party is far stronger than the run, so farming a trivial
   run does not out-earn a fair fight? (Watch out: §3b forbids paying less for dying, not for easy
   content.)

### Touch points

`RunDefinitionSO` (`Repeatable`), `CampaignOps` (start/continue, revisit state), `CampaignMapUI`
(the condition picker in the detail column), `RunSaveData` (the chosen conditions travel with the
run), `MetaProgressSaveData` (best heat per run), `DungeonManager` (first-clear vs revisit rewards,
captive/join skips), `MetaProgressManager` awards, and the balance model: `RunCurveModel` has no
notion of an *n*-th attempt or a heat level (`BALANCE_OPEN.md` §3b).
