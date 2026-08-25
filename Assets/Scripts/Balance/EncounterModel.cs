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

        /// <summary>The level tuning this member was built under; null for the template's own numbers.</summary>
        public LevelEnemyTuning Tuning;

        /// <summary>MaxHealth after the level's tuning — <b>not</b> the template's authored value.</summary>
        public int MaxHealth => Unit != null ? Unit.Stats.MaxHealth : 0;

        /// <summary>XP this level pays for the kill.</summary>
        public int Xp => LevelEnemyTuning.XpFor(Definition, Tuning);

        /// <summary>Gold this level pays for the kill.</summary>
        public int Gold => LevelEnemyTuning.GoldFor(Definition, Tuning);
    }

    /// <summary>A fractional enemy group, and the pool/output arithmetic that goes with it.</summary>
    public class WeightedEnemyGroup
    {
        public List<WeightedEnemy> Members = new List<WeightedEnemy>();

        /// <summary>
        /// The level tuning every member of this group is built under. Set once by whoever builds the
        /// group, so <see cref="Add"/> stays a two-argument call at its call sites.
        /// </summary>
        public LevelEnemyTuning Tuning;

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
                Unit = SimUnit.FromEnemy(enemy, Tuning),
                Weight = weight,
                Tuning = Tuning
            });
        }

        public float HealthPool
        {
            get
            {
                float total = 0f;
                foreach (var member in Members)
                {
                    total += member.Weight * member.MaxHealth;
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
                    total += member.Weight * member.Xp;
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
                    total += member.Weight * member.Gold;
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

        /// <summary>
        /// CTB ticks the party needs to clear the group, after the group's own support: healing
        /// subtracted and its buffs/debuffs applied. Weighted by each member's expected occurrence, so
        /// half a Bog Shaman heals half as much.
        /// </summary>
        public float TicksToClear(PartyBaseline party)
        {
            float net = NetClearRate(party);
            if (net <= 0f)
            {
                return float.PositiveInfinity;
            }
            return HealthPool / net;
        }

        /// <summary>
        /// Party output against this group after its support, in the same currency as
        /// <see cref="PartyDamagePerTick"/>. Shares one definition with
        /// <see cref="BalanceMath.NetClearRate"/> per member so the whole-unit and fractional-group
        /// paths cannot drift.
        /// </summary>
        public float NetClearRate(PartyBaseline party)
        {
            if (party == null || party.Size == 0 || IsEmpty)
            {
                return 0f;
            }

            float raw = PartyDamagePerTick(party);
            float suppression = 1f;
            float sustain = 0f;

            foreach (var member in Members)
            {
                if (member.Unit == null)
                {
                    continue;
                }

                // A fractional member suppresses and heals in proportion to how often it turns up.
                float share = Mathf.Clamp01(member.Weight);
                float unitFactor = BalanceMath.OutputSuppressionOf(party.Units, member.Unit);
                suppression *= 1f - share * (1f - unitFactor);
                sustain += member.Weight * BalanceMath.SustainPerTick(member.Unit);
            }

            return raw * Mathf.Clamp(suppression, 0.01f, 1f) - sustain;
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

            float partyNet = NetClearRate(party);
            float enemyDps = DamagePerTickAgainst(party);

            if (enemyDps <= 0f)
            {
                return 0f;
            }
            if (partyNet <= 0f)
            {
                // The group out-heals or out-shields the party's whole output: unwinnable on paper.
                return float.PositiveInfinity;
            }

            float partyNeeds = HealthPool / partyNet;
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

        /// <summary>The level tuning this room's enemies were built under.</summary>
        public LevelEnemyTuning Tuning;

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
            BalanceRulesSO rules,
            LevelEnemyTuning tuning = null)
        {
            var encounter = new RoomEncounter
            {
                Room = room,
                RoomName = room != null ? (string.IsNullOrEmpty(room.Name) ? room.name : room.Name) : "(none)",
                GuaranteedSpawns = guaranteeAll,
                Tuning = tuning
            };

            // Both groups fight with the level's numbers, not the templates'.
            encounter.Expected.Tuning = tuning;
            encounter.WorstCase.Tuning = tuning;

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
