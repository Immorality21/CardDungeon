using System.Collections.Generic;
using ImmoralityGaming.Fundamentals;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// One fight's event stream: what happened, typed, in the order it happened (the dispatch rules -
    /// fixed order, re-entrant events queued, a capped chain - are <see cref="EventStream"/>'s).
    /// Created by whoever owns the fight - <c>CombatManager.RunCombat</c> and the simulator's encounter
    /// loop alike - and thrown away with it, so a subscription can never outlive the fight it was made
    /// for and nothing has to unsubscribe.
    ///
    /// <para><b>Rules, not presentation.</b> Events are raised when the <i>state</i> changes (by
    /// <see cref="HealthOps"/>, the death sweep, the turn loop), never when an animation plays, and a
    /// handler runs synchronously and may not yield. That is what lets the same handler run in the
    /// live fight and in the balance simulator, which has no animations at all.</para>
    /// </summary>
    public sealed class CombatEvents : EventStream
    {
        /// <summary>Who is shielding whom in this fight (a mech its rider). Lives with the stream so
        /// the live fight and the simulator share it, and goes with the fight.</summary>
        public GuardTable Guards { get; } = new GuardTable();
    }

    // ------------------------------------------------------------------ the events themselves
    //
    // HealthChange and UnitDefeated live beside HealthOps (HealthChange.cs). The rest are raised by the
    // fight's owner. Each is a plain class, not an interface hierarchy: a subscriber names the one type
    // it wants and the bus routes by exact type.

    /// <summary>The fight has begun: every combatant is on the clock.</summary>
    public class CombatStarted
    {
        public List<ICombatUnit> Heroes = new List<ICombatUnit>();
        public List<ICombatUnit> Enemies = new List<ICombatUnit>();
    }

    /// <summary>The fight is over. <see cref="PartyWon"/> is false for a wipe (and for a stalemate in
    /// the simulator).</summary>
    public class CombatEnded
    {
        public bool PartyWon;
    }

    /// <summary>A unit's turn has opened, before its start-of-turn ticks.</summary>
    public class TurnStarted
    {
        public ICombatUnit Unit;
    }

    /// <summary>A unit's turn is over, after its upkeep.</summary>
    public class TurnEnded
    {
        public ICombatUnit Unit;
    }

    /// <summary>A unit used an ability - a cast, an enemy spell, a summoning, a summon's ability, an
    /// Ultra's strike - after its effects resolved. Raised by <c>EffectResolver.Execute</c> itself, so
    /// the live fight and the simulator raise it identically. <see cref="Ability"/> is the castable that
    /// resolved; for a summon or an Ultra that is a throwaway, valid only while handlers run (a summon
    /// with a random-target part raises it twice, once per castable).</summary>
    public class AbilityUsed
    {
        public ICombatUnit Caster;
        public Cards.MagicSO Ability;
        public List<ICombatUnit> Targets = new List<ICombatUnit>();
    }

    /// <summary>A consumable was used in combat, after its effect.</summary>
    public class ItemUsed
    {
        public ICombatUnit User;
        public ICombatUnit Target;
        public Items.ItemSO Item;
    }
}
