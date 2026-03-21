using System;
using System.Collections.Generic;
using Assets.Scripts.IO;

namespace Assets.Scripts.Cards
{
    [Serializable]
    public class CardCollectionSaveData : IWriteable
    {
        public List<CardSaveData> Cards = new List<CardSaveData>();

        public string GetFileName()
        {
            return "CardCollection";
        }
    }
}
