using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.IO;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Cards
{
    public class CardCollectionManager : SingletonBehaviour<CardCollectionManager>
    {
        public const int MaxDeckSize = 5;

        [SerializeField]
        private List<CardSO> _allCards;

        private FileHandler _fileHandler;
        private CardCollectionSaveData _saveData;

        public event Action OnCollectionChanged;

        private void Start()
        {
            _fileHandler = new FileHandler();
            Load();
        }

        public void AddCard(CardSO card)
        {
            _saveData.Cards.Add(new CardSaveData { CardKey = card.Key });
            Save();
            OnCollectionChanged?.Invoke();
        }

        public void RemoveCard(string cardKey)
        {
            var index = _saveData.Cards.FindIndex(x => x.CardKey == cardKey);
            if (index >= 0)
            {
                _saveData.Cards.RemoveAt(index);
                Save();
                OnCollectionChanged?.Invoke();
            }
        }

        public CardSO GetCardSO(string key)
        {
            return _allCards.Find(x => x.Key == key);
        }

        public List<CardSaveData> GetAllCards()
        {
            return _saveData.Cards;
        }

        public List<CardSaveData> GetUnassignedCards()
        {
            return _saveData.Cards.Where(c => string.IsNullOrEmpty(c.AssignedHeroKey)).ToList();
        }

        public List<CardSaveData> GetCardsForHero(string heroKey)
        {
            return _saveData.Cards.Where(c => c.AssignedHeroKey == heroKey).ToList();
        }

        public bool AssignCardToHero(CardSaveData card, string heroKey)
        {
            var currentDeck = GetCardsForHero(heroKey);
            if (currentDeck.Count >= MaxDeckSize)
            {
                return false;
            }

            card.AssignedHeroKey = heroKey;
            Save();
            OnCollectionChanged?.Invoke();
            return true;
        }

        public void UnassignCard(CardSaveData card)
        {
            card.AssignedHeroKey = null;
            Save();
            OnCollectionChanged?.Invoke();
        }

        public void TryDropCard(CardSO card)
        {
            if (card == null)
            {
                return;
            }

            if (UnityEngine.Random.Range(0f, 1f) < 0.5f)
            {
                AddCard(card);
                Debug.Log($"Card dropped: {card.DisplayName} ({card.Key})");
            }
        }

        public void Save()
        {
            _fileHandler.Save(_saveData);
        }

        public void Load()
        {
            _saveData = _fileHandler.Load<CardCollectionSaveData>();
        }
    }
}
