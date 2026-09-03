using Assets.Scripts.Combat;

namespace Assets.Scripts.Cards.Buffs
{
    /// <summary>
    /// Blocks casting. A silenced hero's <b>Magic</b> command is disabled
    /// (<c>RoomActionUI.BuildCommandMenu</c>) and a silenced enemy's <c>CastMagic</c> actions become
    /// ineligible (<c>EnemyActionPlanner</c>), so it is the only answer to a caster that is not
    /// "kill it first".
    ///
    /// <para><b>Draw is deliberately not gated.</b> Draw is how the player <i>acquires</i>, and a
    /// magic taken off an enemy is carried for the rest of the run — blocking acquisition for three
    /// turns costs far more than blocking three casts, and it would make Silence the most punishing
    /// status in the game by accident.</para>
    ///
    /// <para>It does not skip the turn: a silenced unit still attacks, which is what makes it a
    /// <i>redirection</i> rather than a stun.</para>
    /// </summary>
    public class SilencedBuffHandler : IBuffHandler
    {
        public void Apply(ICombatUnit target, int power, int duration, CombatBuffTracker buffTracker)
        {
            if (target == null || buffTracker == null || duration <= 0)
            {
                return;
            }

            buffTracker.ApplyStatusEffect(target, BuffType.Silenced, duration);
        }

        public string GetDisplayText(int power)
        {
            return "Silenced!";
        }

        public bool SkipsTurn => false;

        public string GetSkipTurnMessage(ICombatUnit unit)
        {
            return null;
        }

        public bool IsRemovedByDamageType(DamageType damageType)
        {
            return false;
        }
    }
}
