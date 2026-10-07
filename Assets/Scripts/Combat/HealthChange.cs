namespace Assets.Scripts.Combat
{
    /// <summary>
    /// What moved a unit's health. Carried on every <see cref="HealthChange"/> so a listener can tell
    /// a sword from a poison from a blood price without parsing a log line.
    /// </summary>
    public enum HealthCause
    {
        /// <summary>A basic attack: a hero's Attack, an enemy's swing, heavy blow or area blow.</summary>
        Attack,

        /// <summary>An ability's effect: a cast, a combo, a summon's or an Ultra's effects.</summary>
        Ability,

        /// <summary>A damage- or healing-over-time tick (burn, poison, bleed, regeneration).</summary>
        OverTime,

        /// <summary>A price paid in health: an ability's HealthCost, a summon's blood price.</summary>
        Cost,

        /// <summary>A consumable (a potion).</summary>
        Item,

        /// <summary>An enemy's own Heal action.</summary>
        EnemyHeal,

        /// <summary>A Sacrifice: the hero falls outright.</summary>
        Sacrifice,

        /// <summary>A form starting or ending rescales the bar (the share filled is kept).</summary>
        Form,

        /// <summary>A summon whose health ran out is clamped to zero as it leaves.</summary>
        Clamp,

        /// <summary>A room event's outcome, outside combat.</summary>
        RoomEvent,

        /// <summary>Resting at a refuge, outside combat.</summary>
        Rest,

        /// <summary>Bookkeeping, not an event in the game: a fresh floor's full heal, a hero joining at
        /// full health, a saved bar restored, the balance model resetting a clone.</summary>
        Refill,

        /// <summary>The Tinkerer's Disassemble took a machine apart: it falls outright, and its salvage
        /// is paid on top of the kill (<c>EnemySO.SalvageTable</c>).</summary>
        Disassemble
    }

    /// <summary>
    /// Who and what is behind a health change: the acting unit (null when there is none - a room
    /// event, a tick nobody is credited with), the cause, the element and whether it was a crit.
    /// A value type, so building one at every call site costs nothing.
    /// </summary>
    public struct HealthSource
    {
        public ICombatUnit Source;
        public HealthCause Cause;
        public DamageType DamageType;
        public bool Critical;

        /// <summary>
        /// Set by <see cref="HealthOps"/> on everything a triggered reaction does (see
        /// <see cref="HealthOps.ReactionScope"/>). A reaction's hits never set off another hit reaction,
        /// which is what keeps two "when I deal damage, deal damage" items from feeding each other
        /// forever; its kills still count, because deaths run out.
        /// </summary>
        public bool Reaction;

        public HealthSource(ICombatUnit source, HealthCause cause, DamageType damageType = DamageType.Normal,
            bool critical = false)
        {
            Source = source;
            Cause = cause;
            DamageType = damageType;
            Critical = critical;
            Reaction = false;
        }
    }

    /// <summary>
    /// One change to one unit's health, as it happened: the bar before and after, what was asked for
    /// and what landed. Raised on the fight's <see cref="CombatEvents"/> by <see cref="HealthOps"/>,
    /// the only code that writes <c>Stats.Health</c> in combat.
    /// </summary>
    public class HealthChange
    {
        public ICombatUnit Target;
        public HealthSource Source;
        public int Before;
        public int After;

        /// <summary>Health actually taken off the target: the hit less any overkill. 0 for a heal.</summary>
        public int Landed;

        /// <summary>Health actually restored (a heal, or an absorbed hit). 0 for damage.</summary>
        public int Healed;

        /// <summary>The bar crossed from standing to down with this change.</summary>
        public bool Killed => Before > 0 && After <= 0;

        public ICombatUnit Attacker => Source.Source;
        public HealthCause Cause => Source.Cause;
    }

    /// <summary>
    /// A unit went down. Raised once, at the moment its bar crossed zero, with whoever struck the blow
    /// (null when nobody can be credited). The rewards, removal and presentation follow later, at the
    /// end of the action - see <c>CombatManager.ResolveDeaths</c>.
    /// </summary>
    public class UnitDefeated
    {
        public ICombatUnit Victim;
        public ICombatUnit Killer;
        public HealthCause Cause;
        public DamageType DamageType;

        /// <summary>The blow was a triggered reaction's (see <see cref="HealthSource.Reaction"/>).</summary>
        public bool ByReaction;
    }
}
