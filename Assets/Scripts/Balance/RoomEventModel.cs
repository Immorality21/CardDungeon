using System.Collections.Generic;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Items;
using Assets.Scripts.Rooms;
using Assets.Scripts.Rooms.Events;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>One outcome of one option, costed against a party.</summary>
    public class EventOutcomeCost
    {
        public RoomEventOutcome Outcome;

        /// <summary>Chance of being the outcome drawn, within its own pool. Sums to 1 across a pool.</summary>
        public float Probability;

        /// <summary>HP the outcome's immediate effects spend, damage net of healing.</summary>
        public float HealthCost;

        /// <summary>Healing lost with a consumable this outcome spends — sustain, not health.</summary>
        public float HealingLost;

        /// <summary>Party HP spent clearing whatever the outcome wakes up.</summary>
        public float AwakenedHealthCost;

        /// <summary>Everything the outcome takes out of the level's HP + healing pool.</summary>
        public float SustainCost => HealthCost + HealingLost + AwakenedHealthCost;

        public int Gold;
        public float AwakenedGold;
        public float AwakenedXp;

        /// <summary>Danger index of the fight this outcome starts, 0 when it wakes nothing.</summary>
        public float AwakenedDanger;

        /// <summary>Expected items dropped — the sum of each loot entry's <c>LootRoller</c> chance.</summary>
        public float LootDrops;

        /// <summary>
        /// Buffs and debuffs hung on the party for the rest of the level. Counted, not priced — see
        /// <see cref="RoomEventModel"/> on what the closed form leaves out.
        /// </summary>
        public int Afflictions;

        public bool CostsSomething => SustainCost > 0f || Afflictions > 0 || AwakenedDanger > 0f;
    }

    /// <summary>One <see cref="RoomEventOption"/>, folded over its weighted outcome pools.</summary>
    public class EventOptionModel
    {
        public RoomEventOption Option;
        public string Label = "";
        public RoomEventOptionKind Kind;

        /// <summary>Chance the check passes. 1 for a Guaranteed option, which never rolls.</summary>
        public float SuccessChance = 1f;

        public List<EventOutcomeCost> Success = new List<EventOutcomeCost>();
        public List<EventOutcomeCost> Failure = new List<EventOutcomeCost>();

        public float ExpectedSustainCost;
        public float ExpectedGold;
        public float ExpectedXp;
        public float ExpectedLootDrops;
        public float ExpectedAfflictions;

        /// <summary>The single most expensive outcome either pool can produce.</summary>
        public float WorstSustainCost;

        /// <summary>The most damage a single outcome lands on one hero — what the 1-HP floor clamps.</summary>
        public float WorstSingleHeroDamage;

        public bool IsEngageable => Kind != RoomEventOptionKind.Decline;

        /// <summary>True when some outcome can cost the party something. A gamble that cannot is a button.</summary>
        public bool HasDownside
        {
            get
            {
                foreach (var outcome in Success)
                {
                    if (outcome.CostsSomething)
                    {
                        return true;
                    }
                }
                foreach (var outcome in Failure)
                {
                    if (outcome.CostsSomething)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>
    /// One event as offered by one room template: how often it turns up there, and what engaging with
    /// it is expected to cost and pay.
    /// </summary>
    public class RoomEventEncounter
    {
        public RoomEventSO Event;
        public string Name = "";
        public RoomSO Room;
        public string RoomName = "";

        /// <summary>Requirements the party cannot meet. Non-empty means the event never appears.</summary>
        public List<UnitStat> UnmetRequirements = new List<UnitStat>();

        public bool RequirementsMet => UnmetRequirements.Count == 0;

        /// <summary>
        /// Chance this event wins one instance of this room — its own roll, after every candidate
        /// authored ahead of it in the room's list has failed theirs.
        /// </summary>
        public float AppearChancePerRoom;

        /// <summary>Expected instances of this event in the level: eligible room instances x the above.</summary>
        public float Occurrences;

        /// <summary>The hero the check resolves against — the party's best at the governing stat.</summary>
        public HeroBaseline Actor;

        public int GoverningStatValue;

        /// <summary>Odds a StatCheck option passes, from the same curve the game rolls against.</summary>
        public float CheckSuccessChance;

        public OddsClarity Clarity;

        public List<EventOptionModel> Options = new List<EventOptionModel>();

        /// <summary>The option the model assumes is taken — see <see cref="RoomEventModel"/>.</summary>
        public EventOptionModel Engaged;

        /// <summary>The cheapest way to engage, for the spread between playing safe and gambling.</summary>
        public EventOptionModel Safest;

        public float ExpectedSustainCost => Occurrences * (Engaged != null ? Engaged.ExpectedSustainCost : 0f);
        public float ExpectedGold => Occurrences * (Engaged != null ? Engaged.ExpectedGold : 0f);
        public float ExpectedXp => Occurrences * (Engaged != null ? Engaged.ExpectedXp : 0f);
        public float ExpectedLootDrops => Occurrences * (Engaged != null ? Engaged.ExpectedLootDrops : 0f);
        public float ExpectedAfflictions => Occurrences * (Engaged != null ? Engaged.ExpectedAfflictions : 0f);

        public string Reference => Name + " in " + RoomName;
    }

    /// <summary>
    /// What a level's room events are expected to cost and pay.
    ///
    /// <para>The analyzer modelled combat only, so a level's attrition curve read as if walking into a
    /// sprung needle-trap were free. Events are authored gambles that spend HP, potions and the odd
    /// woken enemy, and they pay gold and loot back — the Treasury hoard alone is worth roughly a
    /// fifth of a level's gold — so leaving them out made the curve optimistic in one direction and
    /// the economy pessimistic in the other.</para>
    ///
    /// <para><b>Every number here comes from the code the game rolls against.</b> Placement odds are
    /// <see cref="RoomEventSpawn"/>, check odds are <see cref="RoomEventResolver.SuccessChance"/>,
    /// outcome weights are <see cref="RoomEventResolver.EffectiveWeight"/>, damage is
    /// <see cref="DamageCalculator"/> down the same flat-power path the executors take, and loot is
    /// <see cref="LootRoller.DropChance"/>. Nothing is re-derived.</para>
    ///
    /// <para><b>The one assumption: the player engages.</b> Declining is free and always available, so
    /// a model of a cautious player is the model the analyzer already had — zero. What is worth
    /// measuring is what the events cost when they are played, so the engaged option is the
    /// <i>most expensive</i> non-Decline one (ties to authored order) and
    /// <see cref="RoomEventEncounter.Safest"/> carries the cheapest, for the spread between playing
    /// safe and gambling. <c>BalanceRulesSO.EventEngagementRate</c> scales the whole contribution for
    /// a designer who wants to model something between the two.</para>
    ///
    /// <para><b>Left out, deliberately.</b> The 1-HP floor (<c>RoomEventRunner.KeepEveryoneStanding</c>)
    /// is not applied: it clamps against a hero's <i>current</i> health, which a closed-form pass does
    /// not track, so an outcome authored above a hero's whole bar is costed at face value. That
    /// over-states rather than under-states, and the analyzer reports the authoring instead. Level
    /// afflictions are counted, not priced — a -2 Endurance for the rest of the level does raise every
    /// later fight's cost, but pricing it would mean re-measuring the level against a second party.</para>
    /// </summary>
    public static class RoomEventModel
    {
        /// <summary>
        /// Models every event a level's rooms can offer.
        ///
        /// <para><paramref name="rooms"/> is the level's room encounters, whose <c>Occurrences</c>
        /// already exclude the party's starting room — the same room
        /// <c>DungeonManager.IsEventEligible</c> skips. <paramref name="eligibilityFactor"/> takes out
        /// anything else that cannot hold an event (the captive's room).
        /// <paramref name="runLevelIndex"/> is the 0-based depth <c>LootRoller</c> scales drops
        /// against.</para>
        /// </summary>
        public static List<RoomEventEncounter> BuildForLevel(
            IList<RoomEncounter> rooms,
            PartyBaseline party,
            BalanceRulesSO rules,
            int runLevelIndex,
            float eligibilityFactor = 1f)
        {
            var encounters = new List<RoomEventEncounter>();
            if (rooms == null || party == null || party.Size == 0)
            {
                return encounters;
            }

            var best = party.BestStats;

            foreach (var roomEncounter in rooms)
            {
                var room = roomEncounter != null ? roomEncounter.Room : null;
                if (room == null || room.IsConnectorRoom || room.PossibleEvents == null)
                {
                    continue;
                }

                float eligible = Mathf.Max(0f, roomEncounter.Occurrences * eligibilityFactor);

                // Placement is one pass per room: each candidate rolls in authored order and the
                // first to pass takes it, so a later candidate only gets its roll when every earlier
                // one missed. That is why listing two events raises the odds of the room offering
                // *something* rather than splitting them between the two.
                float unclaimed = 1f;

                foreach (var definition in room.PossibleEvents)
                {
                    if (definition == null)
                    {
                        continue;
                    }

                    var encounter = Build(definition, party, best, runLevelIndex);
                    encounter.Room = room;
                    encounter.RoomName = roomEncounter.RoomName;

                    float chance = encounter.RequirementsMet
                        ? RoomEventSpawn.ChancePercent(
                              definition.SpawnChancePercent,
                              best[definition.SpawnModifierStat],
                              definition.SpawnModifierRate) / 100f
                        : 0f;

                    encounter.AppearChancePerRoom = unclaimed * chance;
                    encounter.Occurrences = eligible * encounter.AppearChancePerRoom;
                    unclaimed *= 1f - chance;

                    encounters.Add(encounter);
                }
            }

            return encounters;
        }

        /// <summary>
        /// Costs one event against a party, placement aside. Public so the analyzer can audit an event
        /// asset that no room happens to offer.
        /// </summary>
        public static RoomEventEncounter Build(
            RoomEventSO definition,
            PartyBaseline party,
            StatBlock partyBest,
            int runLevelIndex)
        {
            var encounter = new RoomEventEncounter
            {
                Event = definition,
                Name = definition != null
                    ? (string.IsNullOrEmpty(definition.Title) ? definition.name : definition.Title)
                    : "(none)"
            };

            if (definition == null || party == null || party.Size == 0)
            {
                return encounter;
            }

            var best = partyBest ?? party.BestStats;
            encounter.UnmetRequirements = UnmetRequirements(definition.SpawnRequirements, best);

            encounter.Actor = party.BestAt(definition.GoverningStat);
            encounter.GoverningStatValue = best[definition.GoverningStat];
            encounter.CheckSuccessChance =
                RoomEventResolver.SuccessChance(encounter.GoverningStatValue, definition.Difficulty);
            encounter.Clarity =
                RoomEventResolver.ClarityFor(encounter.GoverningStatValue, definition.Difficulty);

            var actor = encounter.Actor != null ? encounter.Actor.Unit : null;

            if (definition.Options != null)
            {
                foreach (var option in definition.Options)
                {
                    if (option == null)
                    {
                        continue;
                    }
                    encounter.Options.Add(BuildOption(
                        option, encounter.CheckSuccessChance, party, actor, runLevelIndex));
                }
            }

            foreach (var option in encounter.Options)
            {
                if (!option.IsEngageable)
                {
                    continue;
                }
                if (encounter.Engaged == null
                    || option.ExpectedSustainCost > encounter.Engaged.ExpectedSustainCost)
                {
                    encounter.Engaged = option;
                }
                if (encounter.Safest == null
                    || option.ExpectedSustainCost < encounter.Safest.ExpectedSustainCost)
                {
                    encounter.Safest = option;
                }
            }

            return encounter;
        }

        /// <summary>Requirements no hero in the party covers. Empty means the gate is open.</summary>
        public static List<UnitStat> UnmetRequirements(IReadOnlyList<UnitStat> requirements, StatBlock partyBest)
        {
            var unmet = new List<UnitStat>();
            if (requirements == null || partyBest == null)
            {
                return unmet;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];

                // StatType.None rows are half-authored inspector rows, which RoomEventSpawn skips
                // rather than treating as impossible. Same rule here, or the model would report an
                // event as unreachable the moment somebody clicked +.
                if (requirement == null || requirement.Type == StatType.None)
                {
                    continue;
                }

                if (partyBest[requirement.Type] < requirement.Amount)
                {
                    unmet.Add(requirement);
                }
            }

            return unmet;
        }

        private static EventOptionModel BuildOption(
            RoomEventOption option,
            float successChance,
            PartyBaseline party,
            ICombatUnit actor,
            int runLevelIndex)
        {
            var model = new EventOptionModel
            {
                Option = option,
                Label = option.Label ?? "",
                Kind = option.Kind,
                // A Guaranteed option is a known trade: its success pool always applies, no roll.
                SuccessChance = option.Kind == RoomEventOptionKind.StatCheck ? successChance : 1f
            };

            model.Success = CostPool(option.Success, party, actor, runLevelIndex);
            model.Failure = CostPool(option.Failure, party, actor, runLevelIndex);

            if (option.Kind == RoomEventOptionKind.Decline)
            {
                return model;
            }

            float pass = model.SuccessChance;
            Accumulate(model, model.Success, pass);
            Accumulate(model, model.Failure, 1f - pass);

            foreach (var outcome in model.Success)
            {
                model.WorstSustainCost = Mathf.Max(model.WorstSustainCost, outcome.SustainCost);
            }
            foreach (var outcome in model.Failure)
            {
                model.WorstSustainCost = Mathf.Max(model.WorstSustainCost, outcome.SustainCost);
            }

            model.WorstSingleHeroDamage = WorstSingleHeroDamage(option, party, actor);
            return model;
        }

        private static void Accumulate(EventOptionModel model, List<EventOutcomeCost> pool, float branchChance)
        {
            if (branchChance <= 0f)
            {
                return;
            }

            foreach (var outcome in pool)
            {
                float weight = branchChance * outcome.Probability;
                model.ExpectedSustainCost += weight * outcome.SustainCost;
                model.ExpectedGold += weight * (outcome.Gold + outcome.AwakenedGold);
                model.ExpectedXp += weight * outcome.AwakenedXp;
                model.ExpectedLootDrops += weight * outcome.LootDrops;
                model.ExpectedAfflictions += weight * outcome.Afflictions;
            }
        }

        /// <summary>
        /// Costs a weighted outcome pool using the game's own weighting, so a Luck-biased pool is
        /// measured the way the party would actually roll it. An empty pool costs nothing; a pool
        /// whose weights are all non-positive resolves to its first entry, matching
        /// <see cref="RoomEventResolver.PickOutcomeIndex"/>.
        /// </summary>
        private static List<EventOutcomeCost> CostPool(
            List<RoomEventOutcome> pool,
            PartyBaseline party,
            ICombatUnit actor,
            int runLevelIndex)
        {
            var costs = new List<EventOutcomeCost>();
            if (pool == null || pool.Count == 0)
            {
                return costs;
            }

            float total = 0f;
            foreach (var outcome in pool)
            {
                total += RoomEventResolver.EffectiveWeight(outcome, actor);
            }

            for (int i = 0; i < pool.Count; i++)
            {
                var cost = CostOutcome(pool[i], party, actor, runLevelIndex);
                cost.Probability = total > 0f
                    ? RoomEventResolver.EffectiveWeight(pool[i], actor) / total
                    : (i == 0 ? 1f : 0f);
                costs.Add(cost);
            }

            return costs;
        }

        private static EventOutcomeCost CostOutcome(
            RoomEventOutcome outcome,
            PartyBaseline party,
            ICombatUnit actor,
            int runLevelIndex)
        {
            var cost = new EventOutcomeCost { Outcome = outcome };
            if (outcome == null)
            {
                return cost;
            }

            var targets = TargetsOf(outcome, party, actor);

            if (outcome.Effects != null)
            {
                foreach (var effect in outcome.Effects)
                {
                    if (effect == null)
                    {
                        continue;
                    }

                    if (effect.EffectType == SpellEffectType.Buff || effect.EffectType == SpellEffectType.Debuff)
                    {
                        // Recorded as a level affliction rather than applied, so it has no HP number
                        // here — only a count. It is still a real cost, paid in every later fight.
                        if (effect.BuffType != BuffType.None && effect.Power != 0)
                        {
                            cost.Afflictions += targets.Count;
                        }
                        continue;
                    }

                    foreach (var target in targets)
                    {
                        cost.HealthCost += EffectHealthCost(effect, target);
                    }
                }
            }

            cost.Gold = Mathf.Max(0, outcome.Gold);

            if (outcome.LootTable != null)
            {
                foreach (var item in outcome.LootTable)
                {
                    cost.LootDrops += LootRoller.DropChance(item, runLevelIndex);
                }
            }

            if (outcome.LoseAConsumable)
            {
                // A potion is worth exactly its restore amount out of the sustain pool the attrition
                // curve divides by, which is what makes this a cost rather than an inventory line.
                cost.HealingLost = party.PotionCount > 0 ? party.PotionHealAmount : 0f;
            }

            if (outcome.AwakenedEnemies != null && outcome.AwakenedEnemies.Count > 0)
            {
                var group = new WeightedEnemyGroup();
                foreach (var definition in outcome.AwakenedEnemies)
                {
                    group.Add(definition, 1f);
                }

                if (!group.IsEmpty)
                {
                    float health = group.ExpectedPartyHealthCost(party);
                    cost.AwakenedHealthCost = float.IsInfinity(health) ? 0f : health;
                    cost.AwakenedXp = group.ExpectedXp;
                    cost.AwakenedGold = group.ExpectedGold;
                    cost.AwakenedDanger = group.DangerIndex(party);
                }
            }

            return cost;
        }

        /// <summary>
        /// One effect's HP cost on one hero, through the same arithmetic the executors use with
        /// <c>flatPower</c>: the authored Power, the target's Endurance, its resistances. Healing is a
        /// negative cost, capped at the bar — it cannot restore more than a hero has to lose.
        /// </summary>
        private static float EffectHealthCost(SpellEffect effect, HeroBaseline hero)
        {
            if (hero == null || hero.Unit == null)
            {
                return 0f;
            }

            int maxHealth = hero.Effective[StatType.MaxHealth];

            if (effect.EffectType == SpellEffectType.Heal)
            {
                return -Mathf.Min(effect.Power, maxHealth);
            }

            int damage = DamageCalculator.Calculate(
                effect.Power,
                hero.Unit.GetEffectiveStat(StatType.Endurance),
                effect.DamageType,
                hero.Unit.Resistances);

            // Absorption reads as negative damage in the calculator, i.e. a heal — same cap.
            return damage < 0 ? -Mathf.Min(-damage, maxHealth) : damage;
        }

        /// <summary>
        /// The most damage any single outcome of this option lands on one hero. This is the number the
        /// 1-HP floor clamps, so an outcome above a hero's whole bar is authoring the model cannot
        /// honour and the analyzer reports instead.
        /// </summary>
        private static float WorstSingleHeroDamage(RoomEventOption option, PartyBaseline party, ICombatUnit actor)
        {
            float worst = WorstInPool(option.Success, party, actor);
            return Mathf.Max(worst, WorstInPool(option.Failure, party, actor));
        }

        private static float WorstInPool(List<RoomEventOutcome> pool, PartyBaseline party, ICombatUnit actor)
        {
            float worst = 0f;
            if (pool == null)
            {
                return worst;
            }

            foreach (var outcome in pool)
            {
                if (outcome == null || outcome.Effects == null)
                {
                    continue;
                }

                foreach (var hero in TargetsOf(outcome, party, actor))
                {
                    float onThisHero = 0f;
                    foreach (var effect in outcome.Effects)
                    {
                        if (effect == null
                            || effect.EffectType == SpellEffectType.Buff
                            || effect.EffectType == SpellEffectType.Debuff)
                        {
                            continue;
                        }
                        onThisHero += EffectHealthCost(effect, hero);
                    }
                    worst = Mathf.Max(worst, onThisHero);
                }
            }

            return worst;
        }

        /// <summary>
        /// Who an outcome lands on, mirroring <c>RoomEventRunner.ResolveTargets</c>: the whole party,
        /// or the hero whose hand is in the chest. Falls back to the party's first hero when the event
        /// has no governing stat to pick an actor with.
        /// </summary>
        private static List<HeroBaseline> TargetsOf(
            RoomEventOutcome outcome, PartyBaseline party, ICombatUnit actor)
        {
            var targets = new List<HeroBaseline>();
            if (party == null)
            {
                return targets;
            }

            if (outcome != null && outcome.Targets == RoomEventTargets.WholeParty)
            {
                targets.AddRange(party.Heroes);
                return targets;
            }

            foreach (var hero in party.Heroes)
            {
                if (hero != null && hero.Unit == actor)
                {
                    targets.Add(hero);
                    return targets;
                }
            }

            if (party.Heroes.Count > 0)
            {
                targets.Add(party.Heroes[0]);
            }
            return targets;
        }
    }
}
