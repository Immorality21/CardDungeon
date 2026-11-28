using System;
using System.Collections.Generic;
using Assets.Scripts.Items;
using Assets.Scripts.Items.UI;
using Assets.Scripts.Progression;
using ImmoralityGaming.Localization;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The potion belt on the level-entry screen (UI Toolkit view-controller): one button per slot the
    /// belt has, and the picker a slot opens.
    ///
    /// <para><b>Packed on the way in since 2026-11-27</b> (owner): it used to sit in the campfire, a
    /// building the player had to remember to visit before a run. It now sits under the party on the
    /// screen every run passes through, so what goes in the belt is chosen at the moment it matters,
    /// the way the party is seen there. Once a run is underway the belt is packed, like the lineup.</para>
    ///
    /// <para>A slot press opens a list of every potion type known plus Empty (playtest 4 finding 50: a
    /// press used to cycle the slot). A type in another slot is marked and swaps places
    /// (<see cref="PotionBeltOps.Assign"/>). The rest of <c>host</c> is disabled beneath the picker, so
    /// the hub's shared cursor walks only its rows.</para>
    /// </summary>
    public class PotionBeltPanel
    {
        private readonly VisualElement _host;
        private readonly VisualElement _section;
        private readonly List<Button> _slots = new List<Button>();
        private readonly List<VisualElement> _slotIcons = new List<VisualElement>();
        private readonly List<Label> _slotCounts = new List<Label>();
        private readonly Label _desc;
        private readonly VisualElement _picker;
        private readonly Label _pickerTitle;
        private readonly VisualElement _pickerList;
        private readonly Button _pickerCancel;

        private int _pickingSlot = -1;
        private bool _locked;

        /// <summary>A slot changed.</summary>
        public event Action Changed;

        /// <param name="host">The screen the belt sits on; its other children are disabled while the picker is up.</param>
        /// <param name="prefix">The element names' prefix: <c>{prefix}-belt</c>, <c>{prefix}-belt-0..n</c>,
        /// <c>{prefix}-belt-desc</c> and <c>{prefix}-belt-picker*</c>.</param>
        public PotionBeltPanel(VisualElement host, string prefix)
        {
            _host = host;
            _section = host.Q<VisualElement>(prefix + "-belt");
            _desc = host.Q<Label>(prefix + "-belt-desc");

            // As many slot buttons as the markup authors; a belt with fewer slots hides the rest.
            for (int i = 0; host.Q<Button>(prefix + "-belt-" + i) != null; i++)
            {
                var slot = host.Q<Button>(prefix + "-belt-" + i);
                int index = i;
                slot.clicked += () => OnSlotClicked(index);
                _slots.Add(slot);

                // A filled slot shows the potion's icon and how many, not its name (playtest
                // 2026-11-28 finding 5); the name and effect are its tooltip.
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("hub-entry__belt-icon");
                var count = new Label { pickingMode = PickingMode.Ignore };
                count.AddToClassList("hub-entry__belt-count");
                slot.Add(icon);
                slot.Add(count);
                _slotIcons.Add(icon);
                _slotCounts.Add(count);
            }

            _picker = host.Q<VisualElement>(prefix + "-belt-picker");
            _pickerTitle = host.Q<Label>(prefix + "-belt-picker-title");
            _pickerList = host.Q<VisualElement>(prefix + "-belt-picker-list");
            _pickerCancel = host.Q<Button>(prefix + "-belt-picker-cancel");
            if (_pickerCancel != null)
            {
                _pickerCancel.clicked += Close;
            }
        }

        /// <summary>Whether the potion picker is up over the screen.</summary>
        public bool IsPicking => _pickingSlot >= 0;

        /// <summary>The picker's Back while it is up, else null: Escape closes the picker before the screen.</summary>
        public Button CancelButton => IsPicking ? _pickerCancel : null;

        /// <summary>
        /// Repaints every slot: the potion in it and how many a run starts with, or "Empty". With no
        /// slot at all the section is hidden.
        /// </summary>
        /// <param name="locked">The run is underway: the belt is packed and the slots only say so.</param>
        public void Refresh(bool locked)
        {
            _locked = locked;
            var belt = PotionBeltSO.Load();
            int slots = belt != null ? PotionBeltState.Slots(belt) : 0;
            var loadout = belt != null ? PotionBeltState.Loadout(belt) : new List<string>();
            if (_section != null)
            {
                _section.style.display = slots > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            for (int i = 0; i < _slots.Count; i++)
            {
                var button = _slots[i];
                bool shown = i < slots && i < loadout.Count;
                button.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!shown)
                {
                    continue;
                }
                var type = belt.Find(loadout[i]);
                var sprite = type != null && type.Potion != null ? type.Potion.Icon : null;
                bool showIcon = sprite != null;
                // No icon to show (an empty slot, or a potion without art): the words, as before.
                button.text = type == null
                    ? Loc.Get("hub.camp.belt-empty")
                    : showIcon ? string.Empty : Loc.Format("hub.camp.belt-slot", ("potion", type.DisplayName), ("count", PotionBeltState.Capacity(type)));
                _slotIcons[i].style.backgroundImage = showIcon ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
                _slotIcons[i].style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;
                _slotCounts[i].text = showIcon ? Loc.Format("hub.camp.belt-slot-count", ("count", PotionBeltState.Capacity(type))) : string.Empty;
                _slotCounts[i].style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;
                button.EnableInClassList("hub-entry__belt-slot--icon", showIcon);
                button.tooltip = type != null
                    ? Loc.Format("hub.camp.belt-slot-tip", ("potion", type.DisplayName), ("effect", ItemPresenter.ConsumableEffectLine(type.Potion)))
                    : string.Empty;
                button.EnableInClassList("cd-camp-mode--chosen", type != null);
            }
            SetText(_desc, locked ? Loc.Get("hub.camp.belt-packed") : Loc.Get("hub.camp.belt-desc"));
        }

        /// <summary>The first slot with nothing in it, for the tutorial to point at; null when every slot is filled.</summary>
        public Button FirstEmptySlot()
        {
            var belt = PotionBeltSO.Load();
            if (belt == null)
            {
                return null;
            }
            var loadout = PotionBeltState.Loadout(belt);
            int slots = PotionBeltState.Slots(belt);
            for (int i = 0; i < _slots.Count && i < slots && i < loadout.Count; i++)
            {
                if (belt.Find(loadout[i]) == null)
                {
                    return _slots[i];
                }
            }
            return null;
        }

        private void OnSlotClicked(int index)
        {
            if (_locked)
            {
                SetText(_desc, Loc.Get("hub.camp.belt-packed-feedback"));
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
            Open(belt, loadout, index);
        }

        private void Open(PotionBeltSO belt, List<string> loadout, int index)
        {
            if (_picker == null || _pickerList == null)
            {
                return;
            }
            _pickingSlot = index;
            _pickerList.Clear();
            SetText(_pickerTitle, Loc.Format("hub.camp.belt-pick-title", ("slot", index + 1)));

            foreach (var type in belt.Types)
            {
                if (type == null || !PotionBeltState.Unlocked(type))
                {
                    continue;
                }
                int elsewhere = -1;
                for (int other = 0; other < loadout.Count; other++)
                {
                    if (other != index && loadout[other] == type.Key)
                    {
                        elsewhere = other;
                    }
                }
                string label = Loc.Format("hub.camp.belt-slot", ("potion", type.DisplayName), ("count", PotionBeltState.Capacity(type)));
                if (elsewhere >= 0)
                {
                    label = Loc.Format("hub.camp.belt-pick-swap", ("potion", label), ("slot", elsewhere + 1));
                }
                AddOption(label, type.Key, loadout[index] == type.Key,
                    type.Potion != null ? ItemPresenter.ConsumableEffectLine(type.Potion) : string.Empty);
            }
            AddOption(Loc.Get("hub.camp.belt-empty"), PotionBeltOps.Empty,
                string.IsNullOrEmpty(loadout[index]), string.Empty);

            foreach (var child in _host.Children())
            {
                if (child != _picker)
                {
                    child.SetEnabled(false);
                }
            }
            _picker.RemoveFromClassList("cd-hidden");
        }

        private void AddOption(string label, string key, bool chosen, string tooltip)
        {
            var button = new Button(() => Pick(key)) { text = label, focusable = false, tooltip = tooltip };
            button.AddToClassList("cd-inv-tab");
            button.AddToClassList("cd-camp-mode");
            button.EnableInClassList("cd-camp-mode--chosen", chosen);
            _pickerList.Add(button);
        }

        private void Pick(string key)
        {
            int index = _pickingSlot;
            Close();
            var belt = PotionBeltSO.Load();
            if (belt == null || index < 0)
            {
                return;
            }
            PotionBeltState.SetLoadout(PotionBeltOps.Assign(PotionBeltState.Loadout(belt), index, key));
            Refresh(_locked);
            Changed?.Invoke();
        }

        /// <summary>Takes the picker down, if it is up, and gives the screen beneath back.</summary>
        public void Close()
        {
            if (_picker == null || _pickingSlot < 0)
            {
                return;
            }
            _pickingSlot = -1;
            _picker.AddToClassList("cd-hidden");
            foreach (var child in _host.Children())
            {
                child.SetEnabled(true);
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
