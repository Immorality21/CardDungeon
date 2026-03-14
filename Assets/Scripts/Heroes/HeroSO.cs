using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Heroes
{
    [CreateAssetMenu(fileName = "NewHero", menuName = "Card Dungeon/Hero")]
    public class HeroSO : ScriptableObject
    {
        public string Label;
        public Sprite Sprite;
        public int BaseAttack;
        public int BaseDefense;
        public int BaseHealth;
        public int BaseAgility = 5;
        public List<LevelConfiguration> LevelProgression = new List<LevelConfiguration>();
    }
}
