using System;
using System.Collections.Generic;

namespace Assets.Scripts.Cards
{
    [Serializable]
    public class DeckSaveData
    {
        public string HeroKey;
        public List<string> UsedCardKeys = new List<string>();
    }
}
