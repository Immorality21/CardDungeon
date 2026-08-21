using System.Collections.Generic;
using Assets.Scripts.Enemies;
using Assets.Scripts.Rooms;
using Assets.Scripts.UnitStats;
using UnityEngine;

namespace Assets.Scripts.Balance
{
    /// <summary>
    /// One enemy type present in an encounter with a fractional multiplicity. Spawn tables roll per
    /// entry (<see cref="EnemySpawnEntry.SpawnChance"/> x <see cref="EnemySpawnEntry.EvaluationCount"/>),
    /// so "1.5 Floating Eyes" is the honest expectation — rounding it to 1 or 2 would misreport the
    /// room. The weighted math below keeps the fraction all the way through.
    /// </summary>
    public class WeightedEnemy
    {
        public EnemySO Definition;
        public SimUnit Unit;
        public float Weight;
    }

    /// <summary>A fractional enemy group, and the pool/output arithmetic that goes with it.</summary>
    public class WeightedEnemyGroup
    {
        public List<WeightedEnemy> Members = new List<WeightedEnemy>();

        public float TotalCount
        {
            get
            {
                float total = 0f;
                foreach (var member in Members)
                {
                    total += member.Weight;
                }
                return total;
            }
        }

        public bool IsEmpty => TotalCount <= 0f;

        public void Add(EnemySO enemy, float weight)
        {
            if (enemy == null || weight <= 0f)
            {
                return;
            }

            var existing = Members.Find(m => m.Definition == enemy);
            if (existing != null)
            {
                existing.Weight += weight;
                return;
            }

            Members.Add(new WeightedEnemy
            {
                Definition = enemy,
                Unit = SimUnit.FromEnemy(enemy),
                Weight = weight
            });
        }

        public float HealthPool
        {
            get
            {
                float total = 0f;
                foreach (var member in Members)
                {
                    total += member.Weight * member.Definition.BaseStats[StatType.MaxHealth];
                }
                return total;
            }
        }

        public float ExpectedXp
        {
            get
            {
                float total = 0f;
                foreach (var member in Members)
                {
                    total += member.Weight * member.Definition.XpReward;
                }
                return total;
            }
        }

        public float ExpectedGold
        {
            get
            {
                float total = 0f;
                foreach (var member in Members)
                {
                    total += member.Weight * member.Definition.GoldReward;
                }
                return total;
            }
        }

        /// <summary>Damage this group lands per CTB tick on the party, archetype cadence included.</summary>
        public float DamagePerTickAgainst(PartyBaseline party)
        {
            if (party == null || party.Size == 0)
            {
                return 0f;
            }

            var partyUnits = party.Units;
            float total = 0f;
            foreach (var member in Members)
            {
                total += member.Weight * BalanceMath.DamagePerTick(member.Unit, partyUnits, party.Size);
            }
            return total;
        }

        /// <summary>Damage the party lands per CTB tick on this group, averaged over its composition.</summary>
        public float PartyDamagePerTick(PartyBaseline party)
        {
            if (party == null || party.Size == 0 || IsEmpty)
            {
                return 0f;
            }

            float weightTotal = TotalCount;
            float total = 0f;

            foreach (var hero in party.Heroes)
            {
                if (hero.Unit == null)
                {
                    continue;
                }

                // Average damage per hero turn against a weighted-random member of the group.
                float perTurn = 0f;
                foreach (var member in Members)
                {
                    perTurn += (member.Weight / weightTotal)
                             * BalanceMath.AverageDamage(
                                 hero.Unit.GetEffectiveAttackPower(), member.Unit, hero.Unit.AttackDamageType,
                                 1f, hero.Unit);
                }

                total += perTurn * BalanceMath.TurnsPerTick(hero.Unit);
            }

            return total;
        }

        /// <summary>CTB ticks the party needs to clear the group.</summary>
        public float TicksToClear(PartyBaseline party)
        {
            float dps = PartyDamagePerTick(party);
            if (dps <= 0f)
            {
                return float.PositiveInfinity;
            }
            return HealthPool / dps;
        }

        /// <summary>Party HP spent clearing the group, ignoring healing.</summary>
        public float ExpectedPartyHealthCost(PartyBaseline party)
        {
            float ticks = TicksToClear(party);
            if (float.IsInfinity(ticks))
            {
                return float.PositiveInfinity;
            }
            return ticks * DamagePerTickAgainst(party);
        }

        /// <summary>See <see cref="BalanceMath.DangerIndex"/> — the fractional-group version.</summary>
        public float DangerIndex(PartyBaseline party)
        {
            if (party == null || party.Size == 0 || IsEmpty)
            {
                return 0f;
            }

            float partyDps = PartyDamagePerTick(party);
            float enemyDps = DamagePerTickAgainst(party);

            if (enemyDps <= 0f)
            {
                return 0f;
            }
            if (partyDps <= 0f)
            {
                return float.PositiveInfinity;
            }

            float partyNeeds = HealthPool / partyDps;
            float enemiesNeed = party.HealthPool / enemyDps;
            return enemiesNeed > 0f ? partyNeeds / enemiesNeed : float.PositiveInfinity;
        }

        /// <summary>Whole-number units for the simulator, rounding each weight to the nearest enemy.</summary>
        public List<SimUnit> ToDiscreteUnits()
        {
            var units = new List<SimUnit>();
            foreach (var member in Members)
            {
                int count = Mathf.RoundToInt(member.Weight);
                for (int i = 0; i < count; i++)
                {
                    units.Add(member.Unit.Clone());
                }
            }
            return units;
        }
    }

    /// <summary>Analysis of one room's spawn table: what it is expected to contain and what that costs.</summary>
    public class RoomEncounter
    {
        public RoomSO Room;
        public string RoomName = "";
        public bool GuaranteedSpawns;

        /// <summary>
        /// How many times this room is expected to appear in the level. A manual layout places
        /// rooms explicitly (1 each); a generated level draws <c>RoomsToGenerate</c> rooms uniformly
        /// from <c>RoomPool</c> (see <c>RoomManager</c>'s TakeRandom), so each pool entry appears
        /// <c>RoomsToGenerate / poolSize</c> times on average.
        /// </summary>
        public float Occurrences = 1f;

        /// <summary>
        /// True when a manual layout's <c>EnemySpawnOverride</c> replaced the room template's own
        /// table. The editor must not offer the shared RoomSO table for editing in that case — the
        /// numbers in play live on the layout entry, not the room.
        /// </summary>
        public bool UsesSpawnOverride;

        public WeightedEnemyGroup Expected = new WeightedEnemyGroup();
        public WeightedEnemyGroup WorstCase = new WeightedEnemyGroup();

        public float ExpectedDanger;
        public float WorstCaseDanger;
        public float ExpectedHealthCost;
        public float ExpectedXp;
        public float ExpectedGold;

        public bool IsCombatRoom => !Expected.IsEmpty;

        /// <summary>
        /// Builds the encounter model for a room. <paramref name="overrideTable"/> is the manual
        /// layout's per-room override when present; <paramref name="guaranteeAll"/> mirrors
        /// <c>ManualRoomEntry.GuaranteeAllSpawns</c>, where every roll is skipped and all spawns land.
        /// </summary>
        public static RoomEncounter Build(
            RoomSO room,
            List<EnemySpawnEntry> overrideTable,
            bool guaranteeAll,
            PartyBaseline party,
            BalanceRulesSO rules)
        {
            var encounter = new RoomEncounter
            {
                Room = room,
                RoomName = room != null ? (string.IsNullOrEmpty(room.Name) ? room.name : room.Name) : "(none)",
                GuaranteedSpawns = guaranteeAll
            };

            if (room == null || party == null)
            {
                return encounter;
            }

            encounter.UsesSpawnOverride = overrideTable != null && overrideTable.Count > 0;
            var table = encounter.UsesSpawnOverride ? overrideTable : room.EnemySpawnTable;
            if (table == null || room.IsConnectorRoom)
            {
                return encounter;
            }

            foreach (var entry in table)
            {
                if (entry == null || entry.Enemy == null)
                {
                    continue;
                }

                int evaluations = Mathf.Max(1, entry.EvaluationCount);
                float expected = guaranteeAll ? evaluations : entry.SpawnChance * evaluations;

                encounter.Expected.Add(entry.Enemy, expected);
                encounter.WorstCase.Add(entry.Enemy, evaluations);
            }

            encounter.ExpectedDanger = encounter.Expected.DangerIndex(party);
            encounter.WorstCaseDanger = encounter.WorstCase.DangerIndex(party);
            encounter.ExpectedHealthCost = encounter.Expected.ExpectedPartyHealthCost(party);
            encounter.ExpectedXp = encounter.Expected.ExpectedXp;
            encounter.ExpectedGold = encounter.Expected.ExpectedGold;

            return encounter;
        }
    }
}
