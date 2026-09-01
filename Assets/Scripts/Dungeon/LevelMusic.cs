using System;
using Assets.Scripts.Audio;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    /// <summary>
    /// Which music the dungeon asks for. This is where the "what floor am I on" knowledge lives, so
    /// <see cref="MusicPlayer"/> stays a generic player with no idea a dungeon exists — the same split
    /// <c>CombatStage</c> uses for the per-level battle backdrop.
    ///
    /// <para>A level's own clip wins over the <c>MusicBank</c>'s track; without one, the bank's
    /// track plays, and with neither the bed fades to silence.</para>
    /// </summary>
    public static class LevelMusic
    {
        /// <summary>Walking the floor: the level's own theme, else the bank's Exploration track.</summary>
        public static void PlayExploration()
        {
            MusicPlayer.Play(MusicTrack.Exploration, ClipOf(level => level.ExplorationMusic));
        }

        /// <summary>
        /// A fight. A boss takes the bank's own climax track and ignores the level's combat theme —
        /// the point of a boss theme is that it is not the floor's.
        /// </summary>
        public static void PlayCombat(bool hasBoss)
        {
            MusicPlayer.Play(
                hasBoss ? MusicTrack.BossCombat : MusicTrack.Combat,
                hasBoss ? null : ClipOf(level => level.CombatMusic),
                fadeSeconds: 0.5f);
        }

        private static AudioClip ClipOf(Func<LevelDefinitionSO, AudioClip> pick)
        {
            if (!DungeonManager.HasInstance)
            {
                return null;
            }
            var level = DungeonManager.Instance.CurrentLevel;
            return level != null ? pick(level) : null;
        }
    }
}
