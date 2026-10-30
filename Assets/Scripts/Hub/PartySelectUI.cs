using System;
using System.Collections.Generic;
using Assets.Scripts.Balance;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies.UI;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.Items;
using Assets.Scripts.Items.UI;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.UIElements;
using ImmoralityGaming.Localization;

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
    ///
    /// <para><b>Laid out on the inventory's frame since 2026-09-30</b> (menu review):
    /// four seats across the top, the roster, the chosen hero's detail and the XP split. Choosing a
    /// hero and acting on them are separate on purpose - a roster row selects, the detail column's
    /// buttons field, bench or crown - so a row is never a misclick that sends someone home.</para>
    /// </summary>
    public class PartySelectUI
    {
        private const int SeatCount = 4;

        private readonly VisualElement _root;
        private readonly PartyRosterSO _catalog;
        private readonly FileHandler _files = new FileHandler();

        private readonly Label _goldLabel;
        private readonly Label _capLabel;
        private readonly Label _shareLabel;
        private readonly Label _feedbackLabel;
        private readonly Button[] _seats = new Button[SeatCount];
        private readonly List<Button> _beltSlots = new List<Button>();
        private readonly Label _beltDesc;
        private readonly ScrollView _rosterList;

        private readonly VisualElement _detailIcon;
        private readonly Label _detailName;
        private readonly Label _detailSub;
        private readonly Button _toggleButton;
        private readonly Button _leadButton;
        private readonly Label _reasonLabel;
        private readonly ScrollView _detailBody;

        private readonly Button _splitEvenButton;
        private readonly Button _splitMentorButton;
        private readonly Button _splitCatchUpButton;
        private readonly Button _splitFocusButton;
        private readonly Label _splitDescLabel;
        private readonly Button _closeButton;

        private readonly Dictionary<string, Button> _rosterRows = new Dictionary<string, Button>();
        private MagicLoadoutSaveData _loadout;
        private string _selectedKey;

        // Why the lineup cannot change right now, or null when it can. Set while a run is underway:
        // who marches out is fixed from the first floor until the run is cleared or lost.
        private string _lockedReason;

        public event Action OnClosed;

        public PartySelectUI(VisualElement root, PartyRosterSO catalog)
        {
            _root = root;
            _catalog = catalog;

            _goldLabel = root.Q<Label>("party-gold");
            _capLabel = root.Q<Label>("party-cap");
            _shareLabel = root.Q<Label>("party-share");
            _feedbackLabel = root.Q<Label>("party-feedback");
            _rosterList = root.Q<ScrollView>("party-roster");

            _detailIcon = root.Q<VisualElement>("party-detail-icon");
            _detailName = root.Q<Label>("party-detail-name");
            _detailSub = root.Q<Label>("party-detail-sub");
            _toggleButton = root.Q<Button>("party-toggle");
            _leadButton = root.Q<Button>("party-lead");
            _reasonLabel = root.Q<Label>("party-reason");
            _detailBody = root.Q<ScrollView>("party-detail-body");

            _splitEvenButton = root.Q<Button>("party-split-even");
            _splitMentorButton = root.Q<Button>("party-split-mentor");
            _splitCatchUpButton = root.Q<Button>("party-split-catchup");
            _splitFocusButton = root.Q<Button>("party-split-focus");
            _splitDescLabel = root.Q<Label>("party-split-desc");
            _closeButton = root.Q<Button>("party-close");
            _beltDesc = root.Q<Label>("party-belt-desc");

            // As many slot buttons as the markup authors; a belt with fewer slots hides the rest.
            for (int i = 0; root.Q<Button>("party-belt-" + i) != null; i++)
            {
                var slot = root.Q<Button>("party-belt-" + i);
                int index = i;
                slot.clicked += () => OnBeltSlotClicked(index);
                _beltSlots.Add(slot);
            }

            for (int i = 0; i < SeatCount; i++)
            {
                _seats[i] = root.Q<Button>("party-seat-" + i);
                int seat = i;
                if (_seats[i] != null)
                {
                    _seats[i].clicked += () => OnSeatClicked(seat);
                }
            }

            if (_toggleButton != null)
            {
                _toggleButton.clicked += OnToggleSelected;
            }
            if (_leadButton != null)
            {
                _leadButton.clicked += OnLeadSelected;
            }
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

        /// <param name="lockedReason">Non-null while a run is underway: who marches out cannot
        /// change, and this says why. The leader and the XP split still can.</param>
        public void Show(string lockedReason = null)
        {
            _lockedReason = lockedReason;
            _root.style.display = DisplayStyle.Flex;
            _loadout = _files.Load<MagicLoadoutSaveData>();
            _selectedKey = null;
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
                return Loc.Get("hub.progress.no-party");
            }

            var names = new List<string>();
            foreach (var hero in fielded)
            {
                names.Add(hero.DisplayName);
            }
            return Loc.Format("hub.camp.fielded-summary", ("count", fielded.Count), ("names", string.Join(", ", names)));
        }

        // --- Refresh -----------------------------------------------------------

        private void Refresh()
        {
            int cap = Cap();
            var owned = HeroRoster.GetOwnedHeroes(_catalog);
            var fieldedKeys = HeroRoster.GetSelectedKeys(_catalog, cap);

            // Nobody chosen yet (or the chosen one is gone): start on the leader.
            if (FindOwned(owned, _selectedKey) == null)
            {
                _selectedKey = fieldedKeys.Count > 0 ? fieldedKeys[0] : (owned.Count > 0 ? owned[0].SaveKey : null);
            }

            SetText(_goldLabel, Loc.Format("hub.camp.gold", ("gold", MetaProgressManager.Instance.Gold)));
            SetText(_capLabel, owned.Count > fieldedKeys.Count
                ? Loc.Format("hub.camp.marching-roster", ("count", fieldedKeys.Count), ("cap", cap), ("owned", owned.Count))
                : Loc.Format("hub.camp.marching", ("count", fieldedKeys.Count), ("cap", cap)));

            int size = Mathf.Max(1, fieldedKeys.Count);
            var mode = MetaProgressManager.Instance.GetXpSplitMode();
            SetText(_shareLabel, mode == XpSplitMode.Even
                ? Loc.Format("hub.camp.share-even", ("percent", 100 / size))
                : size == 1
                    ? Loc.Format("hub.camp.share-mode-one", ("count", size), ("mode", CampfireOps.Label(mode)))
                    : Loc.Format("hub.camp.share-mode", ("count", size), ("mode", CampfireOps.Label(mode))));

            RefreshSeats(fieldedKeys);
            BuildRoster(owned, fieldedKeys);
            RefreshDetail(owned, fieldedKeys, cap);
            RefreshSplitControls(fieldedKeys);
            RefreshBelt();
        }

        /// <summary>The four seats, leader first. An empty seat is a frame the cursor skips.</summary>
        private void RefreshSeats(List<string> fieldedKeys)
        {
            for (int i = 0; i < SeatCount; i++)
            {
                var seat = _seats[i];
                if (seat == null)
                {
                    continue;
                }
                seat.Clear();
                seat.text = string.Empty;

                var hero = i < fieldedKeys.Count && _catalog != null ? _catalog.Find(fieldedKeys[i]) : null;
                bool empty = hero == null;
                seat.EnableInClassList("cd-camp-seat--empty", empty);
                seat.EnableInClassList(KeyboardNavigator.SkipClass, empty);
                seat.EnableInClassList("cd-camp-seat--active", !empty && hero.SaveKey == _selectedKey);
                seat.EnableInClassList("cd-camp-seat--lead", !empty && i == 0);
                seat.pickingMode = empty ? PickingMode.Ignore : PickingMode.Position;
                seat.tooltip = empty ? string.Empty : hero.DisplayName;

                if (empty)
                {
                    seat.Add(MakeLabel(Loc.Get("hub.camp.empty-seat"), "cd-camp-seat__name"));
                    continue;
                }

                var sprite = new VisualElement { pickingMode = PickingMode.Ignore };
                sprite.AddToClassList("cd-camp-seat__sprite");
                if (hero.Sprite != null)
                {
                    sprite.style.backgroundImage = new StyleBackground(hero.Sprite);
                }
                seat.Add(sprite);
                seat.Add(MakeLabel(hero.DisplayName, "cd-camp-seat__name"));
                if (i == 0)
                {
                    seat.Add(MakeLabel(Loc.Get("hub.camp.leads"), "cd-camp-seat__lead"));
                }
            }
        }

        /// <summary>
        /// Everyone owned, marching heroes first in seat order. A row only <i>selects</i>; what to do
        /// with the hero is the detail column's, so a stray click never benches anybody.
        /// </summary>
        private void BuildRoster(List<HeroSO> owned, List<string> fieldedKeys)
        {
            if (_rosterList == null)
            {
                return;
            }
            _rosterList.Clear();
            _rosterRows.Clear();

            var ordered = new List<HeroSO>();
            foreach (var key in fieldedKeys)
            {
                var hero = FindOwned(owned, key);
                if (hero != null)
                {
                    ordered.Add(hero);
                }
            }
            foreach (var hero in owned)
            {
                if (hero != null && !ordered.Contains(hero))
                {
                    ordered.Add(hero);
                }
            }

            foreach (var hero in ordered)
            {
                int seat = fieldedKeys.IndexOf(hero.SaveKey);
                string key = hero.SaveKey;
                var row = new Button(() => SelectHero(key)) { text = string.Empty };
                row.AddToClassList("cd-inv-row");
                row.AddToClassList("cd-camp-row");
                row.AddToClassList(seat >= 0 ? "cd-camp-row--fielded" : "cd-camp-row--benched");
                row.EnableInClassList("cd-inv-row--selected", key == _selectedKey);

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("cd-inv-row__icon");
                if (hero.Sprite != null)
                {
                    icon.style.backgroundImage = new StyleBackground(hero.Sprite);
                }
                row.Add(icon);

                var text = new VisualElement { pickingMode = PickingMode.Ignore };
                text.AddToClassList("cd-inv-row__text");
                // The tag shares the name's line, so the stat caption gets the row's full width - beside
                // the tag it was cut off at "STR 10 · EN…" (playtest 2 finding 9).
                var top = new VisualElement { pickingMode = PickingMode.Ignore };
                top.AddToClassList("cd-camp-row__top");
                top.Add(MakeLabel(hero.DisplayName, "cd-inv-row__name"));
                // The gold bar already says "marching"; the tag only names the leader and the benched.
                if (seat <= 0)
                {
                    top.Add(MakeLabel(seat == 0 ? Loc.Get("hub.camp.leads") : Loc.Get("hub.camp.resting"), "cd-inv-row__meta"));
                }
                text.Add(top);
                text.Add(MakeLabel(RoleLine(StatsOf(hero)), "cd-inv-row__caption"));
                row.Add(text);

                _rosterList.Add(row);
                _rosterRows[key] = row;
            }
        }

        /// <summary>HP and the two stats the hero leans on most - the full grid is in the detail.</summary>
        private static string RoleLine(StatBlock stats)
        {
            var best = new List<StatType>();
            foreach (var stat in StatCatalog.Types)
            {
                if (stat != StatType.MaxHealth && stats[stat] > 0)
                {
                    best.Add(stat);
                }
            }
            best.Sort((a, b) => stats[b].CompareTo(stats[a]));

            var parts = new List<string> { Loc.Format("hub.camp.role.hp", ("hp", stats[StatType.MaxHealth])) };
            for (int i = 0; i < best.Count && i < 2; i++)
            {
                parts.Add(Loc.Format("hub.camp.role.stat", ("stat", StatCatalog.ShortName(best[i])), ("value", stats[best[i]])));
            }
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// The chosen hero: who they are, the two things the fire can do with them, why either is
        /// dimmed, and then what they would march out with - stats and carried abilities.
        /// </summary>
        private void RefreshDetail(List<HeroSO> owned, List<string> fieldedKeys, int cap)
        {
            _detailBody?.Clear();
            var hero = FindOwned(owned, _selectedKey);
            if (hero == null)
            {
                SetText(_detailName, Loc.Get("hub.camp.nobody-here"));
                SetText(_detailSub, string.Empty);
                SetText(_reasonLabel, string.Empty);
                _toggleButton?.SetEnabled(false);
                _leadButton?.SetEnabled(false);
                return;
            }

            int seat = fieldedKeys.IndexOf(hero.SaveKey);
            bool fielded = seat >= 0;

            if (_detailIcon != null)
            {
                _detailIcon.style.backgroundImage = hero.Sprite != null ? new StyleBackground(hero.Sprite) : null;
            }
            SetText(_detailName, hero.DisplayName);
            SetText(_detailSub, seat == 0 ? Loc.Get("hub.camp.leads-the-party")
                              : fielded ? Loc.Format("hub.camp.marching-seat", ("seat", seat + 1))
                              : Loc.Get("hub.camp.staying-behind"));

            // The one action, and - when it is dimmed - the reason, always in the same place.
            string reason = string.Empty;
            if (_toggleButton != null)
            {
                bool canToggle;
                if (_lockedReason != null)
                {
                    _toggleButton.text = fielded ? Loc.Get("hub.camp.stay-behind") : Loc.Get("hub.camp.march-out");
                    canToggle = false;
                    reason = _lockedReason;
                }
                else if (fielded)
                {
                    _toggleButton.text = Loc.Get("hub.camp.stay-behind");
                    canToggle = fieldedKeys.Count > 1;
                    if (!canToggle)
                    {
                        reason = Loc.Get("hub.camp.somebody-has-to-go");
                    }
                }
                else
                {
                    _toggleButton.text = Loc.Get("hub.camp.march-out");
                    canToggle = fieldedKeys.Count < cap;
                    if (!canToggle)
                    {
                        reason = Loc.Format("hub.camp.seats-taken", ("cap", cap));
                    }
                }
                _toggleButton.SetEnabled(canToggle);
            }
            if (_leadButton != null)
            {
                _leadButton.text = seat == 0 ? Loc.Get("hub.camp.leads-the-party") : Loc.Get("hub.camp.lead-the-party");
                _leadButton.SetEnabled(fielded && seat > 0);
            }
            SetText(_reasonLabel, reason);
            // The run's lock is a rule, not a mistake: neutral ink, not the error red a full party gets
            // (playtest 2 finding 11).
            _reasonLabel?.EnableInClassList("cd-reason--info", _lockedReason != null);

            if (_detailBody == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(hero.Blurb))
            {
                _detailBody.Add(MakeLabel(hero.Blurb, "cd-inv-detail__desc"));
            }

            var stats = StatsOf(hero);
            _detailBody.Add(BestiaryLineView.Section(Loc.Get("hub.camp.section.stats")));
            var grid = new VisualElement();
            grid.AddToClassList("cd-inv-stats");
            foreach (var stat in StatCatalog.Types)
            {
                grid.Add(InventoryHubUI.StatCell(StatCatalog.ShortName(stat), stats[stat].ToString(), 0,
                    StatCatalog.Explain(stat)));
            }
            _detailBody.Add(grid);
            AddCombatNumbers(hero, stats);
            AddSignature(hero);

            var save = HeroRoster.GetHeroSave(hero);
            int xp = save != null ? save.CurrentXp : 0;
            _detailBody.Add(MakeLabel(Loc.Format("hub.camp.xp-to-spend", ("xp", xp)), "cd-inv-detail__line"));

            _detailBody.Add(BestiaryLineView.Section(Loc.Get("hub.camp.section.carried")));
            var nodes = NodesOf(hero);
            var known = SphereGridOps.KnownMagicForNodes(hero.SphereGrid, nodes);
            int slots = EquippedMagicState.DefaultSlotCount + SphereGridOps.SlotBonusForNodes(hero.SphereGrid, nodes);
            // Mid-run, what the run holds with what it has left - not the hub loadout at full
            // (playtest 2 finding 2).
            var runEntry = RunKitSource.EntryFor(RunKitSource.Load(_files), hero.SaveKey);
            var carried = MagicLoadoutOps.RunKit(runEntry, known, _loadout?.ChosenFor(hero.SaveKey), slots);
            if (carried.Count == 0)
            {
                _detailBody.Add(MakeLabel(Loc.Get("hub.camp.nothing-carried"), "cd-inv-detail__line"));
            }
            foreach (var slot in carried)
            {
                var magic = MagicCatalog.HasInstance ? MagicCatalog.Instance.GetMagic(slot.Key) : null;
                string name = magic != null ? magic.DisplayName : slot.Key;
                // The town never shows a run's spent charges (playtest 2026-10-30), only the allowance.
                string charges = Loc.Format("hub.camp.charges", ("max", slot.MaxCharges));
                _detailBody.Add(MakeLabel(Loc.Format("hub.camp.carried-line", ("name", name), ("charges", charges)), "cd-inv-detail__line"));
            }

            if (seat == 0)
            {
                _detailBody.Add(MakeLabel(Loc.Get("hub.camp.leader-note"),
                    "cd-inv-detail__note"));
            }
        }

        // --- The hero's status (playtest 2026-10-30: "what do the stats do?") ---------------

        /// <summary>
        /// What the stats add up to in a fight, worked out by the same rules the fight uses
        /// (<see cref="CritRules"/>, <see cref="DefenseRules"/>, <see cref="DamageCalculator"/>), so the
        /// screen cannot promise a number combat does not deliver.
        /// </summary>
        private void AddCombatNumbers(HeroSO hero, StatBlock stats)
        {
            var unit = new HeroSnapshotUnit(hero, stats, null, NodesOf(hero));
            _detailBody.Add(BestiaryLineView.Section(Loc.Get("hub.camp.section.combat")));
            var grid = new VisualElement();
            grid.AddToClassList("cd-inv-stats");
            grid.Add(InventoryHubUI.StatCell(Loc.Get("hub.camp.combat.attack"), unit.GetEffectiveAttackPower().ToString(), 0,
                Loc.Format("hub.camp.combat.attack-tip", ("stat", StatCatalog.DisplayName(unit.AttackStat)))));
            grid.Add(InventoryHubUI.StatCell(Loc.Get("hub.camp.combat.crit"), Percent(CritRules.ChanceFor(unit)), 0,
                Loc.Get("hub.camp.combat.crit-tip")));
            grid.Add(InventoryHubUI.StatCell(Loc.Get("hub.camp.combat.dodge"), Percent(DefenseRules.DodgeChanceFor(unit)), 0,
                Loc.Get("hub.camp.combat.dodge-tip")));
            grid.Add(InventoryHubUI.StatCell(Loc.Get("hub.camp.combat.physical"), Percent(Reduction(stats[StatType.Endurance])), 0,
                Loc.Get("hub.camp.combat.physical-tip")));
            grid.Add(InventoryHubUI.StatCell(Loc.Get("hub.camp.combat.magic"), Percent(Reduction(stats[StatType.Spirit])), 0,
                Loc.Get("hub.camp.combat.magic-tip")));
            _detailBody.Add(grid);
        }

        /// <summary>The share of a hit a defense stat takes off, as <see cref="DamageCalculator"/> works it.</summary>
        private static float Reduction(int defense)
        {
            float d = Mathf.Max(0, defense);
            return d / (d + DamageCalculator.EnduranceConstant);
        }

        private static string Percent(float fraction)
        {
            return Loc.Format("hub.camp.combat.percent", ("value", Mathf.RoundToInt(fraction * 100f)));
        }

        /// <summary>The hero's signature mechanic in one line, for the heroes who have one.</summary>
        private void AddSignature(HeroSO hero)
        {
            if (hero.Momentum != null && hero.Momentum.IsActive)
            {
                _detailBody.Add(MakeLabel(Loc.Format("hub.camp.signature.momentum",
                    ("percent", hero.Momentum.PercentPerStack), ("stat", StatCatalog.DisplayName(StatType.Strength)),
                    ("max", hero.Momentum.MaxStacks)), "cd-inv-detail__line"));
            }
            if (hero.Focus != null && hero.Focus.IsActive)
            {
                _detailBody.Add(MakeLabel(Loc.Format("hub.camp.signature.focus",
                    ("per", hero.Focus.PerCast), ("max", hero.Focus.Max)), "cd-inv-detail__line"));
            }
        }

        /// <summary>
        /// The campfire's XP split: three modes stacked, the chosen one filled in.
        ///
        /// <para>A mode the fire cannot grant yet is shown <b>dimmed, naming the level that would
        /// buy it</b> rather than hidden - the same bargain the Ability Forge makes, and the reason
        /// a disabled button here never reads as a bug. The chosen mode stays enabled so the
        /// keyboard cursor can land on it.</para>
        /// </summary>
        // --- The potion belt --------------------------------------------------

        /// <summary>
        /// One button per slot the belt has: the potion in it and how many a run starts with, or
        /// "Empty". Packed for the run once it is underway, like the lineup.
        /// </summary>
        private void RefreshBelt()
        {
            var belt = PotionBeltSO.Load();
            int slots = belt != null ? PotionBeltState.Slots(belt) : 0;
            var loadout = belt != null ? PotionBeltState.Loadout(belt) : new List<string>();
            for (int i = 0; i < _beltSlots.Count; i++)
            {
                var button = _beltSlots[i];
                bool shown = i < slots;
                button.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown)
                {
                    continue;
                }
                var type = belt.Find(loadout[i]);
                button.text = type != null ? Loc.Format("hub.camp.belt-slot", ("potion", type.DisplayName), ("count", PotionBeltState.Capacity(type))) : Loc.Get("hub.camp.belt-empty");
                button.tooltip = type != null ? ItemPresenter.ConsumableEffectLine(type.Potion) : string.Empty;
                button.EnableInClassList("cd-camp-mode--chosen", type != null);
            }
            SetText(_beltDesc, _lockedReason != null
                ? Loc.Get("hub.camp.belt-packed")
                : Loc.Get("hub.camp.belt-desc"));
        }

        private void OnBeltSlotClicked(int index)
        {
            if (_lockedReason != null)
            {
                SetFeedback(Loc.Get("hub.camp.belt-packed-feedback"));
                return;
            }
            var belt = PotionBeltSO.Load();
            if (belt == null)
            {
                return;
            }
            var loadout = PotionBeltState.Loadout(belt);
            if (index >= loadout.Count)
            {
                return;
            }
            loadout[index] = PotionBeltOps.Cycle(belt, loadout, index, key => PotionBeltState.Unlocked(belt.Find(key)));
            PotionBeltState.SetLoadout(loadout);
            SetFeedback(string.Empty);
            RefreshBelt();
        }

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
                    ? Loc.Get("hub.camp.choose-mentored")
                    : Loc.Format("hub.camp.mentoring", ("hero", focus.DisplayName));
                _splitFocusButton.SetEnabled(fieldedKeys.Count > 0);
            }

            SetText(_splitDescLabel, CampfireOps.Describe(chosen));
        }

        private static void PaintModeButton(Button button, XpSplitMode mode, XpSplitMode chosen,
                                            int campfireLevel, int levelNeeded)
        {
            if (button == null)
            {
                return;
            }

            bool offered = CampfireOps.Offers(campfireLevel, mode);
            button.text = !offered
                ? Loc.Format("hub.camp.mode-needs-level", ("mode", CampfireOps.Label(mode)), ("level", levelNeeded))
                : chosen == mode ? Loc.Format("hub.camp.mode-chosen", ("mode", CampfireOps.Label(mode))) : CampfireOps.Label(mode);
            button.SetEnabled(offered);
            button.EnableInClassList("cd-camp-mode--chosen", chosen == mode);
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
            return FindOwned(HeroRoster.GetOwnedHeroes(_catalog), key);
        }

        // --- Actions -----------------------------------------------------------

        /// <summary>Selecting is cheap: repaint the highlight and the detail, leave the rows alone,
        /// so the keyboard cursor stays on the row it just pressed.</summary>
        private void SelectHero(string key)
        {
            _selectedKey = key;
            foreach (var pair in _rosterRows)
            {
                pair.Value.EnableInClassList("cd-inv-row--selected", pair.Key == key);
            }
            int cap = Cap();
            var fieldedKeys = HeroRoster.GetSelectedKeys(_catalog, cap);
            RefreshSeats(fieldedKeys);
            RefreshDetail(HeroRoster.GetOwnedHeroes(_catalog), fieldedKeys, cap);
        }

        private void OnSeatClicked(int seat)
        {
            var fieldedKeys = HeroRoster.GetSelectedKeys(_catalog, Cap());
            if (seat < fieldedKeys.Count)
            {
                SelectHero(fieldedKeys[seat]);
            }
        }

        private void OnToggleSelected()
        {
            var hero = FindOwned(HeroRoster.GetOwnedHeroes(_catalog), _selectedKey);
            if (hero == null)
            {
                return;
            }
            if (_lockedReason != null)
            {
                SetFeedback(_lockedReason);
                return;
            }

            int cap = Cap();
            var keys = new List<string>(HeroRoster.GetSelectedKeys(_catalog, cap));
            if (keys.Contains(hero.SaveKey))
            {
                if (keys.Count <= 1)
                {
                    SetFeedback(Loc.Get("hub.camp.somebody-has-to-go"));
                    return;
                }
                keys.Remove(hero.SaveKey);
                SetFeedback(Loc.Format("hub.camp.feedback.stays-behind", ("hero", hero.DisplayName)));
            }
            else
            {
                if (keys.Count >= cap)
                {
                    SetFeedback(Loc.Format("hub.camp.feedback.only-cap", ("cap", cap)));
                    return;
                }
                keys.Add(hero.SaveKey);
                SetFeedback(Loc.Format("hub.camp.feedback.marches-out", ("hero", hero.DisplayName)));
            }

            HeroRoster.SetSelectedKeys(_catalog, keys, cap);
            Refresh();
        }

        /// <summary>Moves the chosen hero to seat 1. The leader walks in front and carries the
        /// XP remainder, which used to be settable only by benching everyone ahead of them.</summary>
        private void OnLeadSelected()
        {
            int cap = Cap();
            var keys = new List<string>(HeroRoster.GetSelectedKeys(_catalog, cap));
            int index = keys.IndexOf(_selectedKey);
            if (index <= 0)
            {
                return;
            }

            keys.RemoveAt(index);
            keys.Insert(0, _selectedKey);
            HeroRoster.SetSelectedKeys(_catalog, keys, cap);
            var hero = FindOwned(HeroRoster.GetOwnedHeroes(_catalog), _selectedKey);
            SetFeedback(hero != null ? Loc.Format("hub.camp.feedback.leads", ("hero", hero.DisplayName)) : Loc.Get("hub.camp.feedback.they-lead"));
            Refresh();
        }

        private void ChooseMode(XpSplitMode mode)
        {
            int campfireLevel = HubState.LevelOf(HubService.Party);
            if (!CampfireOps.Offers(campfireLevel, mode))
            {
                SetFeedback(Loc.Get("hub.camp.feedback.too-small"));
                return;
            }
            if (MetaProgressManager.Instance.GetXpSplitMode() == mode)
            {
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
            SetFeedback(Loc.Format("hub.camp.feedback.xp-shared", ("mode", CampfireOps.Label(mode))));
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
                SetFeedback(Loc.Get("hub.camp.feedback.nobody-to-mentor"));
                return;
            }

            int current = fieldedKeys.IndexOf(MetaProgressManager.Instance.GetXpFocusHeroKey());
            string next = fieldedKeys[(current + 1) % fieldedKeys.Count];
            MetaProgressManager.Instance.SetXpSplit(XpSplitMode.Mentor, next);
            Refresh();
        }

        // --- Helpers -----------------------------------------------------------

        /// <summary>
        /// The stats the hero fights with - base, bought grid nodes, gear - not the bare HeroSO
        /// block, which understated every hero who had spent XP (the inventory had the same bug).
        /// </summary>
        private static StatBlock StatsOf(HeroSO hero)
        {
            return HeroStatCalculator.WithGear(
                HeroStatCalculator.BaseStatsForNodes(hero, NodesOf(hero)),
                InventoryManager.Instance.GetEquippedGear(hero.SaveKey));
        }

        private static List<string> NodesOf(HeroSO hero)
        {
            var save = HeroRoster.GetHeroSave(hero);
            return save != null && save.ActivatedNodes != null ? save.ActivatedNodes : new List<string>();
        }

        private static HeroSO FindOwned(List<HeroSO> owned, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }
            foreach (var hero in owned)
            {
                if (hero != null && hero.SaveKey == key)
                {
                    return hero;
                }
            }
            return null;
        }

        private static Label MakeLabel(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private static void SetShown(VisualElement element, bool shown)
        {
            if (element != null)
            {
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static int Cap()
        {
            return MetaProgressManager.Instance.GetPartyCap();
        }

        private void SetFeedback(string message)
        {
            SetText(_feedbackLabel, message);
        }
    }
}
