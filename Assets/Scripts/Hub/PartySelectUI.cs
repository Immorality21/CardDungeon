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
    /// <para>This screen exists because party width is a trade, not an upgrade. Every hero added
    /// roughly halves per-enemy danger, and since XP splits across the lineup
    /// (<see cref="XpSplit"/>) every hero added also cuts each one's share - so going wide buys
    /// safety and faster clears while going narrow buys depth. Neither dominates, which is the whole
    /// point, and it only works if the player can choose. The share line is shown as a percentage
    /// precisely so the trade is on screen while the choice is being made.</para>
    ///
    /// <para><b>The slot purchase was removed on 2026-09-17</b> - the party is four wide from the
    /// start and what paces it is the roster, not gold (see <see cref="PartySlots"/>). What the fire
    /// sells instead is <b>how the XP is divided</b>: the same question one step further on, and the
    /// one thing a campfire level grants (<see cref="CampfireOps"/>). Operates on a VisualElement
    /// subtree owned by the menu's UIDocument - not a MonoBehaviour, same as the merchant.</para>
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
        private readonly Button _splitEvenButton;
        private readonly Button _splitMentorButton;
        private readonly Button _splitCatchUpButton;
        private readonly Button _splitFocusButton;
        private readonly Label _splitDescLabel;
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
            _splitEvenButton = root.Q<Button>("party-split-even");
            _splitMentorButton = root.Q<Button>("party-split-mentor");
            _splitCatchUpButton = root.Q<Button>("party-split-catchup");
            _splitFocusButton = root.Q<Button>("party-split-focus");
            _splitDescLabel = root.Q<Label>("party-split-desc");
            _closeButton = root.Q<Button>("party-close");

            if (_splitEvenButton != null)
            {
                _splitEvenButton.clicked += () => ChooseMode(XpSplitMode.Even);
            }
            if (_splitMentorButton != null)
            {
                _splitMentorButton.clicked += () => ChooseMode(XpSplitMode.Mentor);
            }
            if (_splitCatchUpButton != null)
            {
                _splitCatchUpButton.clicked += () => ChooseMode(XpSplitMode.CatchUp);
            }
            if (_splitFocusButton != null)
            {
                _splitFocusButton.clicked += CycleFocusHero;
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
            RefreshSplitControls(fieldedKeys);
        }

        /// <summary>
        /// The campfire's XP split: three modes side by side, the chosen one filled in.
        ///
        /// <para>A mode the fire cannot grant yet is shown <b>dimmed, naming the level that would
        /// buy it</b> rather than hidden - the same bargain the Ability Forge makes, and the reason
        /// a disabled button here never reads as a bug.</para>
        /// </summary>
        private void RefreshSplitControls(List<string> fieldedKeys)
        {
            int campfireLevel = HubState.LevelOf(HubService.Party);
            var chosen = MetaProgressManager.Instance.GetXpSplitMode();

            PaintModeButton(_splitEvenButton, XpSplitMode.Even, chosen, campfireLevel, 1);
            PaintModeButton(_splitMentorButton, XpSplitMode.Mentor, chosen, campfireLevel, 2);
            PaintModeButton(_splitCatchUpButton, XpSplitMode.CatchUp, chosen, campfireLevel, 3);

            // Only Mentor needs a name attached to it; CatchUp picks its own, fresh, every award.
            bool needsFocus = chosen == XpSplitMode.Mentor;
            SetShown(_splitFocusButton, needsFocus);
            if (needsFocus && _splitFocusButton != null)
            {
                var focus = FocusHero(fieldedKeys);
                _splitFocusButton.text = focus == null
                    ? "Tap to choose who is mentored"
                    : $"Mentoring {focus.DisplayName} — tap to change";
                _splitFocusButton.SetEnabled(fieldedKeys.Count > 0);
            }

            if (_splitDescLabel != null)
            {
                _splitDescLabel.text = CampfireOps.Describe(chosen);
            }
        }

        private static void PaintModeButton(Button button, XpSplitMode mode, XpSplitMode chosen,
                                            int campfireLevel, int levelNeeded)
        {
            if (button == null)
            {
                return;
            }

            // A locked mode names the campfire level that would buy it rather than hiding - the
            // Ability Forge's bargain, and the reason a disabled button here never reads as a bug.
            bool offered = CampfireOps.Offers(campfireLevel, mode);
            button.text = offered
                ? CampfireOps.Label(mode)
                : $"{CampfireOps.Label(mode)} · Lv {levelNeeded}";
            button.SetEnabled(offered && chosen != mode);
            button.EnableInClassList("cd-split-option--chosen", chosen == mode);
        }

        /// <summary>The fielded hero the Mentor mode is pointed at, or null when nobody is named or
        /// the named one is no longer marching.</summary>
        private HeroSO FocusHero(List<string> fieldedKeys)
        {
            string key = MetaProgressManager.Instance.GetXpFocusHeroKey();
            if (string.IsNullOrEmpty(key) || !fieldedKeys.Contains(key))
            {
                return null;
            }
            foreach (var hero in HeroRoster.GetOwnedHeroes(_catalog))
            {
                if (hero != null && hero.SaveKey == key)
                {
                    return hero;
                }
            }
            return null;
        }

        private void ChooseMode(XpSplitMode mode)
        {
            int campfireLevel = HubState.LevelOf(HubService.Party);
            if (!CampfireOps.Offers(campfireLevel, mode))
            {
                SetFeedback("The campfire is not big enough for that yet.");
                return;
            }

            var fieldedKeys = HeroRoster.GetSelectedKeys(_catalog, Cap());
            string focus = MetaProgressManager.Instance.GetXpFocusHeroKey();

            // Switching to Mentor with nobody named would silently fall back to an even split, so
            // name the leader - the choice is then visible, and one tap from being changed.
            if (mode == XpSplitMode.Mentor && (string.IsNullOrEmpty(focus) || !fieldedKeys.Contains(focus)))
            {
                focus = fieldedKeys.Count > 0 ? fieldedKeys[0] : "";
            }

            MetaProgressManager.Instance.SetXpSplit(mode, focus);
            // Just what changed - the rule itself is already on screen under the buttons, and
            // printing it twice makes the panel look like it is arguing with itself.
            SetFeedback($"XP is now shared: {CampfireOps.Label(mode)}.");
            Refresh();
        }

        /// <summary>Steps the mentored hero to the next one marching out, wrapping at the end. A
        /// cycle rather than a second button per row: the lineup is at most four, and a row that
        /// grows a second action is a row that gets misclicked.</summary>
        private void CycleFocusHero()
        {
            var fieldedKeys = HeroRoster.GetSelectedKeys(_catalog, Cap());
            if (fieldedKeys.Count == 0)
            {
                SetFeedback("Nobody is marching out to mentor.");
                return;
            }

            int current = fieldedKeys.IndexOf(MetaProgressManager.Instance.GetXpFocusHeroKey());
            string next = fieldedKeys[(current + 1) % fieldedKeys.Count];
            MetaProgressManager.Instance.SetXpSplit(XpSplitMode.Mentor, next);
            Refresh();
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
