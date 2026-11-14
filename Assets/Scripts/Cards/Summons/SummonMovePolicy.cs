using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Cards
{
    /// <summary>
    /// Which of a unit summon's moves (<see cref="SummonSO.Actions"/>) it uses on a turn its Signature is
    /// not ready: the one rule the bot (<c>BotCombat.ActAsSummon</c>) and the balance simulator
    /// (<c>EncounterSimulator</c>) both play, so a gap between them is never two different summons.
    /// In order:
    /// <list type="number">
    /// <item>at <see cref="MendAtHealthShare"/> of its health or under, a move that mends or shields it;</item>
    /// <item>a move that does no damage and no healing (a buff, a debuff) it has not used in its last
    /// <see cref="SupportEveryTurns"/> turns;</item>
    /// <item>a damaging move - the one that hits every enemy when there are several;</item>
    /// <item>else null: its Attack.</item>
    /// </list>
    /// A rotating summon (<see cref="SummonSO.RotateActions"/>) has no choice to make, and a silenced one
    /// has only its Attack, so both get null. Pure: <c>SummonMovePolicyTests</c>.
    /// </summary>
    public static class SummonMovePolicy
    {
        /// <summary>The balance model's and the bot's heal threshold.</summary>
        public const float MendAtHealthShare = 0.45f;

        /// <summary>A buff or debuff is used again once this many of the summon's turns have passed.</summary>
        public const int SupportEveryTurns = 3;

        /// <param name="lastUsed">Move key to the turn it was last used, per summoned unit; the pick is
        /// written into it.</param>
        /// <param name="turn">The summon's turns taken so far (<see cref="SummonStay.TurnsTaken"/>).</param>
        public static MagicSO Pick(SummonSO summon, bool silenced, float healthShare, int livingEnemies, int turn,
            IDictionary<string, int> lastUsed)
        {
            if (silenced || summon == null || summon.RotateActions || summon.Actions == null)
            {
                return null;
            }
            var moves = summon.Actions.Where(a => a != null && a.Effects != null).ToList();
            if (moves.Count == 0)
            {
                return null;
            }

            MagicSO pick = null;
            if (healthShare <= MendAtHealthShare)
            {
                pick = moves.FirstOrDefault(m => m.Effects.Any(e => e != null
                    && (e.EffectType == SpellEffectType.Heal || e.EffectType == SpellEffectType.Drain
                        || (e.EffectType == SpellEffectType.Buff && e.BuffType == BuffType.Barrier))));
            }
            if (pick == null)
            {
                pick = moves.FirstOrDefault(m => !m.Effects.Any(e => e != null
                        && (e.EffectType == SpellEffectType.Damage || e.EffectType == SpellEffectType.Heal))
                    && (lastUsed == null || !lastUsed.TryGetValue(m.Key, out int last) || turn - last >= SupportEveryTurns));
            }
            if (pick == null)
            {
                var damaging = moves.Where(m => m.Effects.Any(e => e != null && e.EffectType == SpellEffectType.Damage)).ToList();
                pick = livingEnemies > 1
                    ? damaging.FirstOrDefault(m => m.TargetType == MagicTargetType.AllEnemies) ?? damaging.FirstOrDefault()
                    : damaging.FirstOrDefault();
            }
            if (pick != null && lastUsed != null)
            {
                lastUsed[pick.Key] = turn;
            }
            return pick;
        }
    }
}
