using System.Collections.Generic;
using Assets.Scripts.Items;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// The rules of a revisit: what every re-clear does on its own, how rewards scale with heat, and
    /// every condition the player can add (<c>docs/plans/REVISITS.md</c>). One asset,
    /// <c>Resources/Revisits.asset</c>, loaded like <c>Campaign.asset</c> so neither the hub nor the
    /// dungeon needs it wired.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Revisit Rules")]
    public class RevisitRulesSO : ScriptableObject
    {
        public const string ResourcePath = "Revisits";

        [Header("Every revisit")]
        [Tooltip("Enemy MaxHealth added on every revisit, before any condition. 50 = +50%.")]
        public int BaseEnemyHealthPercent = 50;

        [Tooltip("Enemy damage added on every revisit, before any condition. 50 = +50%.")]
        public int BaseEnemyDamagePercent = 50;

        [Header("Rewards")]
        [Tooltip("Extra XP, gold and Essence on every revisit at heat 0, paying for the base above.")]
        public int BaseRewardPercent = 25;

        [Tooltip("Extra XP, gold and Essence per point of heat.")]
        public int RewardPercentPerHeat = 10;

        [Tooltip("Scarce materials a revisit drops only when it is beating the run's best cleared " +
                 "heat. Without this a repeatable run whose boss guarantees one is an infinite tap; " +
                 "with it, more of them is a reward for doing something harder.")]
        public List<ItemSO> NewBestHeatOnly = new List<ItemSO>();

        [Header("Conditions")]
        public List<RunModifier> Modifiers = new List<RunModifier>();

        public RunModifier Find(string key)
        {
            if (string.IsNullOrEmpty(key) || Modifiers == null)
            {
                return null;
            }
            return Modifiers.Find(m => m != null && m.Key == key);
        }

        private static RevisitRulesSO _cached;

        /// <summary>The project's rules, or null when no asset is authored (revisits then change nothing).</summary>
        public static RevisitRulesSO Load()
        {
            if (_cached == null)
            {
                // Qualified: the game has its own Assets.Scripts.Resources namespace.
                _cached = UnityEngine.Resources.Load<RevisitRulesSO>(ResourcePath);
            }
            return _cached;
        }
    }
}
