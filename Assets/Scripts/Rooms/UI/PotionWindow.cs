using System;
using System.Collections.Generic;
using Assets.Scripts.Audio;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using ImmoralityGaming.Localization;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Rooms.UI
{
    /// <summary>
    /// Drinking while the party walks (owner, 2026-11-01): health carries between floors, so a hurt
    /// party must not have to wait for the next fight. Pick a potion, then who drinks it. No turn is
    /// spent, and that is the whole point of drinking here rather than in a fight.
    /// </summary>
    public class PotionWindow
    {
        private readonly VisualElement _root;
        private readonly Label _prompt;
        private readonly ScrollView _list;
        private readonly KeyboardNavigator _nav;
        private Party _party;
        private ItemSO _potion;

        /// <summary>The window closed.</summary>
        public event Action Closed;

        /// <summary>A potion was drunk. The caller saves, so a quit cannot take it back.</summary>
        public event Action Drank;

        public bool IsOpen => !_root.ClassListContains("cd-hidden");

        public PotionWindow(VisualElement root)
        {
            _root = root;
            _prompt = root.Q<Label>("potion-prompt");
            _list = root.Q<ScrollView>("potion-list");
            _nav = new KeyboardNavigator(root);
            _nav.Cancelled += Back;
            root.Q<Button>("potion-back").clicked += Back;
        }

        public void Open(Party party)
        {
            _party = party;
            _potion = null;
            _root.RemoveFromClassList("cd-hidden");
            ShowPotions();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            _root.AddToClassList("cd-hidden");
            _nav.Reset();
            Closed?.Invoke();
        }

        public void HandleKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.P)
            {
                Close();
                return;
            }
            _nav.HandleKey(evt);
        }

        /// <summary>Whether the party carries anything worth opening the window for.</summary>
        public static bool AnyPotion()
        {
            return InventoryManager.HasInstance && Usable().Count > 0;
        }

        private static List<ItemSaveData> Usable()
        {
            var inventory = InventoryManager.Instance;
            return inventory.GetConsumables().FindAll(stack => PotionUse.WorksOutsideCombat(inventory.GetItemSO(stack.ItemKey)));
        }

        private void Back()
        {
            if (_potion == null)
            {
                Close();
                return;
            }
            _potion = null;
            ShowPotions();
        }

        private void ShowPotions()
        {
            _list.Clear();
            var stacks = Usable();
            _prompt.text = Loc.Get(stacks.Count > 0 ? "room.potions.pick-potion" : "room.potions.none");
            foreach (var stack in stacks)
            {
                var item = InventoryManager.Instance.GetItemSO(stack.ItemKey);
                AddRow(Loc.Format("room.potions.potion-row", ("potion", item.DisplayName), ("count", stack.Quantity)),
                    true, () => ShowHeroes(item));
            }
            _nav.Reset();
            _nav.SelectFirst();
        }

        private void ShowHeroes(ItemSO item)
        {
            _potion = item;
            _list.Clear();
            _prompt.text = Loc.Format("room.potions.pick-hero", ("potion", item.DisplayName));
            foreach (var hero in _party.Heroes)
            {
                if (hero != null && hero.Stats != null)
                {
                    var drinker = hero;
                    AddRow(Loc.Format("room.potions.hero-row", ("hero", hero.DisplayName),
                            ("health", hero.Stats.Health), ("max", hero.GetEffectiveMaxHealth())),
                        Helps(item, hero), () => Drink(item, drinker));
                }
            }
            _nav.Reset();
            _nav.SelectFirst();
        }

        private void AddRow(string text, bool enabled, Action pressed)
        {
            var row = new Button(pressed) { text = text, focusable = false };
            row.AddToClassList("cd-list-button");
            row.SetEnabled(enabled);
            _list.Add(row);
        }

        private static bool Helps(ItemSO item, Hero hero)
        {
            return PotionUse.Helps(item, hero.IsAlive, hero.Stats.Health, hero.GetEffectiveMaxHealth(), ChargesSpent(hero));
        }

        private static bool ChargesSpent(Hero hero)
        {
            var slots = DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null
                ? DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey)
                : null;
            return slots != null && slots.Exists(slot => slot.Magic != null && slot.Charges < slot.MaxCharges);
        }

        private void Drink(ItemSO item, Hero hero)
        {
            if (!InventoryManager.Instance.TryConsume(item.Key))
            {
                return;
            }

            CombatAudio.Play(CombatSound.ItemUse);
            if (PotionUse.Heals(item))
            {
                HealthOps.Heal(hero, PotionUse.HealAmount(item, hero), new HealthSource(hero, HealthCause.Item), null);
                CombatAudio.Play(CombatSound.Heal);
            }
            if (PotionUse.RestoresCharges(item))
            {
                new EquippedChargeBank(DungeonManager.Instance.MagicState)
                    .Restore(hero, SpellcastAction.AnyChargeSlot, PotionUse.ChargesOf(item), -1);
            }
            Drank?.Invoke();

            if (InventoryManager.Instance.GetConsumableQuantity(item.Key) > 0)
            {
                ShowHeroes(item);
                return;
            }
            _potion = null;
            ShowPotions();
        }
    }
}
