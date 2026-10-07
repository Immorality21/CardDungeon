using System;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Events
{
    /// <summary>
    /// The game-wide event stream: what happened to the player's stuff and progress, outside the
    /// rules of any one fight - an item picked up, gold banked, a level cleared, a node bought. The
    /// counterpart of a fight's <c>CombatEvents</c>, with the same dispatch rules
    /// (<see cref="EventStream"/>: fixed order, re-entrant events queued, a capped chain).
    ///
    /// <para><b>What it is for.</b> Anything that wants to <i>react</i> to the game without the code
    /// that changed it knowing: an item that grows with its kills (<c>ItemGrowth</c>), and the
    /// achievements and hub quests to come. The code that changes state raises one event at its single
    /// chokepoint (<c>InventoryManager.AddItem</c>, <c>MetaProgressManager.AddGold</c>, ...) and never
    /// calls the listeners itself.</para>
    ///
    /// <para><b>Provisional vs kept.</b> Gains during a level are live but not yet safe: a wipe or a
    /// quit throws the level's items, kill-gold and XP away. Events fire at the live change, so a
    /// listener that counts something permanent (an achievement for gold earned) either counts only
    /// committed changes (<see cref="CurrencyChanged.Pending"/> false) or holds its tally until
    /// <see cref="LevelCleared"/> and drops it on <see cref="LevelForfeited"/>.</para>
    ///
    /// <para><b>Unsubscribe.</b> Unlike a fight's stream, this one outlives scenes, so a subscriber
    /// that is a scene object must unsubscribe when it goes (<c>OnDestroy</c>/<c>OnDisable</c>). It is
    /// cleared on entering play mode, so a session never inherits the last one's listeners.</para>
    /// </summary>
    public static class GameEvents
    {
        private static readonly EventStream Stream = new EventStream();

        public static void Subscribe<T>(Action<T> handler, int order = 0) where T : class
        {
            Stream.Subscribe(handler, order);
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : class
        {
            Stream.Unsubscribe(handler);
        }

        public static void Publish<T>(T evt) where T : class
        {
            Stream.Publish(evt);
        }

        /// <summary>Drops every subscription. For tests, and for entering play mode with domain reload
        /// off, where statics survive from the last session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Stream.Clear();
        }
    }
}
