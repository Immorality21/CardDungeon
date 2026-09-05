using System;
using System.Collections.Generic;
using Assets.Scripts.Heroes;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// Party select (UI Toolkit view-controller): which of the owned heroes actually march out.
    ///
    /// <para>This screen exists because party width stopped being free. Every hero added roughly
    /// halves per-enemy danger, and since XP splits evenly (<see cref="XpSplit"/>) every hero added
    /// also cuts each one's share - so going wide buys safety and faster clears while going narrow
    /// buys depth. Neither dominates, which is the whole point, and it only works if the player can
    /// choose. The share line is shown as a percentage precisely so the trade is on screen while the
    /// choice is being made.</para>
    ///
    /// <para>The cap itself (<see cref="PartySlots"/>) is a Gold sink bought here, so the price of
    /// going wider is visible next to the reason not to. Operates on a VisualElement subtree owned by
    /// the menu's UIDocument - not a MonoBehaviour, same as the merchant.</para>
    /// </summary>
    public class PartySelectUI
    {
        private readonly VisualElement _root;
        private readonly PartyRosterSO _catalog;
        private readonly Label _goldLabel;
        private readonly Label _capLabel;
        private readonly Label _shareLabel;
        private readonly Label _feedbackLabel;
        private readonly ScrollView _fieldedList;
        private readonly ScrollView _benchList;
        private readonly Button _buySlotButton;
        private readonly Button _closeButton;

        public event Action OnClosed;

        public PartySelectUI(VisualElement root, PartyRosterSO catalog)
        {
            _root = root;
            _catalog = catalog;

            _goldLabel = root.Q<Label>("party-gold");
            _capLabel = root.Q<Label>("party-cap");
            _shareLabel = root.Q<Label>("party-share");
            _feedbackLabel = root.Q<Label>("party-feedback");
            _fieldedList = root.Q<ScrollView>("party-fielded");
            _benchList = root.Q<ScrollView>("party-bench");
            _buySlotButton = root.Q<Button>("party-buy-slot");
            _closeButton = root.Q<Button>("party-close");

            if (_buySlotButton != null)
            {
                _buySlotButton.clicked += OnBuySlot;
            }
            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
            }

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            SetFeedback(string.Empty);
            Refresh();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        /// <summary>
        /// The fielded lineup as display names, for the run-progress screen - so "Enter Dungeon" is
        /// never pressed without knowing who is going.
        /// </summary>
        public string FieldedSummary()
        {
            var fielded = HeroRoster.GetSelectedHeroes(_catalog, Cap());
            if (fielded.Count == 0)
            {
                return "No party selected.";
            }

            var names = new List<string>();
            foreach (var hero in fielded)
            {
                names.Add(hero.DisplayName);
            }
            return $"Party ({fielded.Count}): {string.Join(", ", names)}";
        }

        // --- Refresh -----------------------------------------------------------

        private void Refresh()
        {
            int cap = Cap();
            var owned = HeroRoster.GetOwnedHeroes(_catalog);
            var fieldedKeys = HeroRoster.GetSelectedKeys(_catalog, cap);

            // The screen sells a slot, so it has to show the purse — a price with no purse beside it
            // is the merchant's mistake not to repeat.
            if (_goldLabel != null)
            {
                _goldLabel.text = $"Gold: {MetaProgressManager.Instance.Gold}";
            }

            _capLabel.text = $"Marching out: {fieldedKeys.Count} of {cap}"
                           + (owned.Count > fieldedKeys.Count ? $"   ·   {owned.Count} in the roster" : string.Empty);

            int size = Mathf.Max(1, fieldedKeys.Count);
            _shareLabel.text = $"Each hero earns {100 / size}% of every kill's XP, and a bigger party "
                             + "spreads the damage thinner. Wide clears faster; narrow levels faster.";

            BuildLists(owned, fieldedKeys, cap);
            RefreshBuySlotButton(cap);
        }

        private void RefreshBuySlotButton(int cap)
        {
            if (_buySlotButton == null)
            {
                return;
            }

            int cost = MetaProgressManager.Instance.GetPartySlotCost();
            if (cost <= 0)
            {
                _buySlotButton.text = $"Party is as wide as it gets ({PartySlots.MaxCap})";
                _buySlotButton.SetEnabled(false);
                return;
            }

            _buySlotButton.text = $"Field a {Ordinal(cap + 1)} hero — {cost} gold";
            _buySlotButton.SetEnabled(MetaProgressManager.Instance.CanBuyPartySlot());
        }

        private void BuildLists(List<HeroSO> owned, List<string> fieldedKeys, int cap)
        {
            if (_fieldedList == null || _benchList == null)
            {
                return;
            }
            _fieldedList.Clear();
            _benchList.Clear();

            // Fielded in selection order — index 0 is the leader, and the leader carries the XP
            // remainder and lends the party its sprite, so the order is worth showing.
            int fieldedCount = 0;
            foreach (var key in fieldedKeys)
            {
                var hero = _catalog != null ? _catalog.Find(key) : null;
                if (hero == null)
                {
                    continue;
                }
                bool isOnly = fieldedKeys.Count == 1;
                var captured = hero;
                _fieldedList.Add(MakeRow(hero, true, fieldedCount == 0, !isOnly,
                    isOnly ? "Only hero" : "Bench", () => OnBench(captured)));
                fieldedCount++;
            }

            if (fieldedCount == 0)
            {
                _fieldedList.Add(MakeEmptyLabel("Nobody is marching out."));
            }

            int benched = 0;
            foreach (var hero in owned)
            {
                if (fieldedKeys.Contains(hero.SaveKey))
                {
                    continue;
                }
                bool room = fieldedKeys.Count < cap;
                var captured = hero;
                _benchList.Add(MakeRow(hero, false, false, room,
                    room ? "Field" : "Party full", () => OnField(captured)));
                benched++;
            }

            if (benched == 0)
            {
                _benchList.Add(MakeEmptyLabel(owned.Count >= PartySlots.MaxCap
                    ? "Everyone you own is marching out."
                    : "Nobody in reserve — rescue a captive to grow the roster."));
            }
        }

        // --- Actions -----------------------------------------------------------

        private void OnField(HeroSO hero)
        {
            int cap = Cap();
            var keys = new List<string>(HeroRoster.GetSelectedKeys(_catalog, cap));
            if (keys.Count >= cap)
            {
                SetFeedback($"Only {cap} can march out — bench someone first, or buy a slot.");
                return;
            }

            keys.Add(hero.SaveKey);
            HeroRoster.SetSelectedKeys(_catalog, keys, cap);
            SetFeedback($"{hero.DisplayName} marches out.");
            Refresh();
        }

        private void OnBench(HeroSO hero)
        {
            int cap = Cap();
            var keys = new List<string>(HeroRoster.GetSelectedKeys(_catalog, cap));
            if (keys.Count <= 1)
            {
                SetFeedback("Somebody has to go.");
                return;
            }

            keys.Remove(hero.SaveKey);
            HeroRoster.SetSelectedKeys(_catalog, keys, cap);
            SetFeedback($"{hero.DisplayName} stays behind.");
            Refresh();
        }

        private void OnBuySlot()
        {
            if (!MetaProgressManager.Instance.TryBuyPartySlot())
            {
                SetFeedback(MetaProgressManager.Instance.GetPartySlotCost() <= 0
                    ? "The party is already as wide as it gets."
                    : "Not enough gold.");
                return;
            }

            SetFeedback($"Room for {Cap()} in the marching order now.");
            Refresh();
        }

        // --- Rows --------------------------------------------------------------

        private static VisualElement MakeRow(HeroSO hero, bool fielded, bool isLeader, bool actionEnabled,
                                             string actionText, Action onClick)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-shop-row");
            row.AddToClassList(fielded ? "cd-party-row--fielded" : "cd-party-row--benched");

            var icon = new VisualElement();
            icon.AddToClassList("cd-shop-row__icon");
            if (hero.Sprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(hero.Sprite);
            }

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1f;

            var statParts = new List<string>();
            foreach (var stat in StatCatalog.Types)
            {
                int value = hero.BaseStats[stat];
                if (value != 0)
                {
                    statParts.Add(StatCatalog.ShortName(stat) + " " + value);
                }
            }

            var title = hero.DisplayName + (isLeader ? " (leads)" : string.Empty);
            var name = new Label(title + "   " + string.Join(" · ", statParts));
            name.AddToClassList("cd-shop-row__name");
            textCol.Add(name);

            if (!string.IsNullOrEmpty(hero.Blurb))
            {
                var blurb = new Label(hero.Blurb);
                blurb.AddToClassList("cd-shop-row__sub");
                textCol.Add(blurb);
            }

            var button = new Button(() => onClick()) { text = actionText };
            button.AddToClassList("cd-button");
            button.AddToClassList("cd-shop-row__btn");
            button.SetEnabled(actionEnabled);

            row.Add(icon);
            row.Add(textCol);
            row.Add(button);
            return row;
        }

        private static Label MakeEmptyLabel(string text)
        {
            var label = new Label(text);
            label.AddToClassList("cd-shop-empty");
            return label;
        }

        private static int Cap()
        {
            return MetaProgressManager.Instance.GetPartyCap();
        }

        private static string Ordinal(int value)
        {
            switch (value)
            {
                case 2: return "second";
                case 3: return "third";
                case 4: return "fourth";
                default: return value.ToString();
            }
        }

        private void SetFeedback(string message)
        {
            if (_feedbackLabel != null)
            {
                _feedbackLabel.text = message;
            }
        }
    }
}
