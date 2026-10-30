using Assets.Scripts.IO;
using ImmoralityGaming.Localization;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// The player's combat speed (playtest 3 finding 21): x1, x2 or x3, picked in Options (title screen
    /// and the dungeon's pause overlay) and saved to <c>savedata/Gameplay.json</c>, a machine preference
    /// like <c>Audio.json</c>.
    ///
    /// <para>It is applied as <c>Time.timeScale</c> while a fight runs and put back to 1 when it ends
    /// (<c>CombatManager</c>), so walking the floor, the hub and the victory summary always run at normal
    /// speed. Everything paced on scaled time speeds up with it - turn delays, lunges, projectiles, idle
    /// loops. The short hit-stops and the hit-frame wince run on unscaled time and stay their length,
    /// which keeps a hit readable at x3.</para>
    /// </summary>
    public static class CombatSpeed
    {
        /// <summary>The speeds on offer, in the order the Options button cycles them.</summary>
        public static readonly int[] Steps = { 1, 2, 3 };

        private static GameplayOptionsSaveData _data;
        private static FileHandler _files;

        private static GameplayOptionsSaveData Data
        {
            get
            {
                if (_data == null)
                {
                    _files = new FileHandler();
                    _data = _files.Load<GameplayOptionsSaveData>();
                    _data.CombatSpeed = Clamp(_data.CombatSpeed);
                }
                return _data;
            }
        }

        /// <summary>The speed fights run at.</summary>
        public static int Current => Data.CombatSpeed;

        /// <summary>Moves to the next speed (x3 wraps to x1), saves it, and applies it at once if a fight is running.</summary>
        public static int Cycle(bool inCombat)
        {
            Data.CombatSpeed = Next(Data.CombatSpeed);
            _files.Save(Data);
            Apply(inCombat);
            return Data.CombatSpeed;
        }

        /// <summary>Sets the engine's time scale for being in a fight or not.</summary>
        public static void Apply(bool inCombat)
        {
            Time.timeScale = inCombat ? Current : 1f;
        }

        /// <summary>The speed after <paramref name="speed"/> in the cycle; anything unknown starts it again.</summary>
        public static int Next(int speed)
        {
            for (int i = 0; i < Steps.Length; i++)
            {
                if (Steps[i] == speed)
                {
                    return Steps[(i + 1) % Steps.Length];
                }
            }
            return Steps[0];
        }

        /// <summary>A stored value outside the steps (a hand-edited file) reads as x1.</summary>
        public static int Clamp(int speed)
        {
            foreach (int step in Steps)
            {
                if (step == speed)
                {
                    return speed;
                }
            }
            return Steps[0];
        }

        /// <summary>What the Options button says: "x1", "x2", "x3".</summary>
        public static string Label(int speed)
        {
            return Loc.Format("combat.speed.label", ("speed", speed));
        }
    }

    /// <summary>
    /// Gameplay preferences on disk (<c>savedata/Gameplay.json</c>). Its own file for the same reason the
    /// audio settings are: a preference, not progress, so it survives a wiped save or an abandoned run.
    /// </summary>
    [System.Serializable]
    public class GameplayOptionsSaveData : IWriteable
    {
        public int CombatSpeed = 1;

        public string GetFileName()
        {
            return "Gameplay";
        }
    }
}
