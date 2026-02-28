using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    [CreateAssetMenu(fileName = "NewHero", menuName = "Card Dungeon/Hero")]
    public class HeroSO : ScriptableObject
    {
        public string Label;
        public int BaseAttack;
        public int BaseDefense;
        public int BaseHealth;
        public List<LevelConfiguration> LevelProgression = new List<LevelConfiguration>();
    }
}
