using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Heroes;

namespace Assets.Scripts.Cards
{
    public class DungeonDeckState
    {
        private Dictionary<string, List<string>> _heroDecks = new Dictionary<string, List<string>>();
        private Dictionary<string, HashSet<string>> _usedCards = new Dictionary<string, HashSet<string>>();

        public void Initialize(List<Hero> heroes, CardCollectionManager cardManager)
        {
            _heroDecks.Clear();
            _usedCards.Clear();

            foreach (var hero in heroes)
            {
                var assigned = cardManager.GetCardsForHero(hero.HeroKey);
                var cardKeys = assigned.Select(c => c.CardKey).ToList();
                _heroDecks[hero.HeroKey] = cardKeys;
                _usedCards[hero.HeroKey] = new HashSet<string>();
            }
        }

        public void RestoreUsedCards(List<DeckSaveData> usedCardData)
        {
            if (usedCardData == null)
            {
                return;
            }

            foreach (var entry in usedCardData)
            {
                if (_usedCards.ContainsKey(entry.HeroKey))
                {
                    foreach (var key in entry.UsedCardKeys)
                    {
                        _usedCards[entry.HeroKey].Add(key);
                    }
                }
            }
        }

        public List<CardSO> GetAvailableCards(string heroKey, CardCollectionManager cardManager)
        {
            if (!_heroDecks.TryGetValue(heroKey, out var deck))
            {
                return new List<CardSO>();
            }

            var used = _usedCards.ContainsKey(heroKey) ? _usedCards[heroKey] : new HashSet<string>();
            var available = new List<CardSO>();

            foreach (var cardKey in deck)
            {
                if (!used.Contains(cardKey))
                {
                    var so = cardManager.GetCardSO(cardKey);
                    if (so != null)
                    {
                        available.Add(so);
                    }
                }
            }

            return available;
        }

        public void MarkCardUsed(string heroKey, string cardKey)
        {
            if (!_usedCards.ContainsKey(heroKey))
            {
                _usedCards[heroKey] = new HashSet<string>();
            }
            _usedCards[heroKey].Add(cardKey);
        }

        public bool IsCardAvailable(string heroKey, string cardKey)
        {
            if (!_heroDecks.TryGetValue(heroKey, out var deck))
            {
                return false;
            }

            if (!deck.Contains(cardKey))
            {
                return false;
            }

            if (_usedCards.TryGetValue(heroKey, out var used))
            {
                return !used.Contains(cardKey);
            }

            return true;
        }

        public List<DeckSaveData> GetSaveData()
        {
            var result = new List<DeckSaveData>();

            foreach (var kvp in _usedCards)
            {
                if (kvp.Value.Count > 0)
                {
                    result.Add(new DeckSaveData
                    {
                        HeroKey = kvp.Key,
                        UsedCardKeys = kvp.Value.ToList()
                    });
                }
            }

            return result;
        }
    }
}
