using System;
using Assets.Scripts.Enemies;
using Assets.Scripts.Heroes;
using UnityEngine;

namespace Assets.Scripts.Dungeon
{
    [Serializable]
    public class RunLevelEntry
    {
        public LevelDefinitionSO LevelTemplate;
        public string LevelName;
        public ManualLevelLayoutSO ManualLayout;

        [Tooltip("Optional boss for this level. When set, this enemy is guaranteed (alone) in " +
                 "the exit room, making the level's climax a boss fight. Leave null for a normal level.")]
        public EnemySO BossEnemy;

        [Tooltip("Optional captive hero for this level. When set — and the player does not already " +
                 "own them — they are placed in a room off the start/exit rooms and can be freed " +
                 "for nothing, joining the party mid-run. Like XP, the rescue is only committed on " +
                 "level clear: die first and they are lost with the run.")]
        public HeroSO RescueHero;
    }
}
