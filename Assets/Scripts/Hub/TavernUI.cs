using System;
using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.Hub.Quests;
using Assets.Scripts.IO;
using Assets.Scripts.Progression;
using Assets.Scripts.Items.UI;
using ImmoralityGaming.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Hub
{
    /// <summary>
    /// The tavern's quest board (2026-11-02): every quest in <see cref="QuestCatalogSO"/> as a row, its
    /// page beside the list, and one button that does what the quest is waiting for - Accept, or Collect
    /// once it is done. Rules are <see cref="QuestOps"/>, the writes <see cref="QuestRewards"/>; this
    /// class only draws. Keeps its own Up/Down cursor like the Hall of Knowledge, Enter presses the
    /// button.
    /// </summary>
    public class TavernUI
    {
        private readonly VisualElement _root;
        private readonly PartyRosterSO _roster;
        private readonly ScrollView _list;
        private readonly ScrollView _detail;
        private readonly VisualElement _icon;
        private readonly Label _name;
        private readonly Label _sub;
        private readonly Button _action;
        private readonly Button _reroll;
        private readonly Label _gold;
        private readonly Label _feedback;
        private readonly Button _closeButton;

        private readonly List<QuestSO> _quests = new List<QuestSO>();
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private QuestSaveData _save;
        private int _selected = -1;
        private bool _isShown;

        public event Action OnClosed;

        public TavernUI(VisualElement root, PartyRosterSO roster)
        {
            _root = root;
            _roster = roster;
            _list = root.Q<ScrollView>("tavern-list");
            _detail = root.Q<ScrollView>("tavern-detail");
            _icon = root.Q<VisualElement>("tavern-icon");
            _name = root.Q<Label>("tavern-name");
            _sub = root.Q<Label>("tavern-sub");
            _action = root.Q<Button>("tavern-action");
            _reroll = root.Q<Button>("tavern-reroll");
            _gold = root.Q<Label>("tavern-gold");
            _feedback = root.Q<Label>("tavern-feedback");
            _closeButton = root.Q<Button>("tavern-close");

            if (_action != null)
            {
                _action.focusable = false;
                _action.clicked += OnAction;
            }
            if (_reroll != null)
            {
                _reroll.focusable = false;
                _reroll.clicked += OnReroll;
            }
            if (_closeButton != null)
            {
                _closeButton.focusable = false;
                _closeButton.clicked += Hide;
            }
            if (_list != null)
            {
                _list.focusable = false;
            }

            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _root.RegisterCallback<NavigationMoveEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationSubmitEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });
            _root.RegisterCallback<NavigationCancelEvent>(evt => { if (_isShown) { evt.StopPropagation(); } });

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _isShown = true;
            _root.style.display = DisplayStyle.Flex;
            SetFeedback(string.Empty);

            Refresh(0);

            _root.focusable = true;
            if (_root.panel != null)
            {
                _root.Focus();
            }
        }

        public void Hide()
        {
            _isShown = false;
            _root.focusable = false;
            _root.style.display = DisplayStyle.None;
            _list?.Clear();
            _detail?.Clear();
            _rows.Clear();
            OnClosed?.Invoke();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!_isShown)
            {
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.UpArrow:
                    Move(-1);
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    Move(1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                case KeyCode.Z:
                    OnAction();
                    evt.StopPropagation();
                    break;
                case KeyCode.R:
                    OnReroll();
                    evt.StopPropagation();
                    break;
                case KeyCode.Escape:
                case KeyCode.Backspace:
                case KeyCode.X:
                    Hide();
                    evt.StopPropagation();
                    break;
            }
        }

        private void Move(int delta)
        {
            if (_rows.Count == 0)
            {
                return;
            }
            int index = _selected < 0 ? 0 : (_selected + delta + _rows.Count) % _rows.Count;
            Select(index);
            _list?.ScrollTo(_rows[index]);
        }

        /// <summary>Re-reads the save and rebuilds the board, the cursor on <paramref name="select"/>.</summary>
        private void Refresh(int select)
        {
            _save = QuestRewards.LoadBoard();
            _quests.Clear();
            _quests.AddRange(QuestOps.BoardQuests(_save, QuestRewards.Pool()));
            if (_gold != null && MetaProgressManager.Instance != null)
            {
                _gold.text = Loc.Format("items.inv.gold", ("gold", MetaProgressManager.Instance.Gold));
            }
            _list?.Clear();
            _rows.Clear();
            _selected = -1;

            for (int i = 0; i < _quests.Count; i++)
            {
                var quest = _quests[i];
                int index = i;
                var row = MakeRow(quest);
                row.RegisterCallback<ClickEvent>(_ => Select(index));
                _list?.Add(row);
                _rows.Add(row);
            }

            if (_rows.Count == 0)
            {
                SetHead(null, Loc.Get("hub.tavern.empty.title"), Loc.Get("hub.tavern.empty.sub"));
                _detail?.Clear();
                SetAction(null, false);
                SetReroll(false);
                return;
            }
            Select(Mathf.Clamp(select, 0, _rows.Count - 1));
        }

        private VisualElement MakeRow(QuestSO quest)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-inv-row");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("cd-inv-row__icon");
            if (quest.BoardIcon != null)
            {
                icon.style.backgroundImage = new StyleBackground(quest.BoardIcon);
            }
            row.Add(icon);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("cd-inv-row__text");
            var nameLabel = new Label(quest.DisplayName) { pickingMode = PickingMode.Ignore };
            nameLabel.AddToClassList("cd-inv-row__name");
            text.Add(nameLabel);
            var caption = new Label(StateCaption(quest)) { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("cd-inv-row__caption");
            text.Add(caption);
            row.Add(text);

            var state = QuestOps.StateOf(_save, quest);
            row.EnableInClassList("cd-tavern-row--ready", state == QuestState.Ready);
            return row;
        }

        private string StateCaption(QuestSO quest)
        {
            switch (QuestOps.StateOf(_save, quest))
            {
                case QuestState.Accepted:
                    return Loc.Format("hub.tavern.state.progress", ("done", QuestOps.ProgressOf(_save, quest)), ("count", quest.Count));
                case QuestState.Ready:
                    return Loc.Get("hub.tavern.state.ready");
                default:
                    return Loc.Get("hub.tavern.state.offered");
            }
        }

        private void Select(int index)
        {
            _selected = index;
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].EnableInClassList("cd-inv-row--selected", i == _selected);
            }
            ShowQuest(_quests[index]);
        }

        private void ShowQuest(QuestSO quest)
        {
            var state = QuestOps.StateOf(_save, quest);
            SetHead(quest.BoardIcon, quest.DisplayName, StateCaption(quest));

            _detail.Clear();
            _detail.scrollOffset = Vector2.zero;
            if (!string.IsNullOrEmpty(quest.Description))
            {
                AddLine(quest.Description, "cd-inv-detail__desc");
            }

            AddLine(GoalLine(quest), "cd-inv-detail__line");
            if (quest.RevisitOnly)
            {
                AddLine(Loc.Get("hub.tavern.goal-revisit"), "cd-inv-detail__line");
            }
            if (state == QuestState.Accepted)
            {
                AddLine(Loc.Format("hub.tavern.progress", ("done", QuestOps.ProgressOf(_save, quest)), ("count", quest.Count)),
                    "cd-inv-detail__line");
            }

            AddLine(Loc.Get("hub.tavern.reward"), "cd-inv-col__title");
            if (quest.RewardItem != null)
            {
                AddLine(quest.RewardItemCount > 1
                        ? Loc.Format("hub.tavern.reward-count", ("count", quest.RewardItemCount), ("item", quest.RewardItem.DisplayName))
                        : quest.RewardItem.DisplayName,
                    "cd-inv-detail__line");
            }
            if (quest.RewardGold > 0)
            {
                AddLine(Loc.Format("hub.tavern.reward-gold", ("gold", quest.RewardGold)), "cd-inv-detail__line");
            }
            if (quest.RewardXp > 0)
            {
                AddLine(Loc.Format("hub.tavern.reward-xp", ("xp", quest.RewardXp)), "cd-inv-detail__line");
            }

            SetReroll(state == QuestState.Offered);
            switch (state)
            {
                case QuestState.Offered:
                    SetAction(Loc.Get("hub.tavern.action.accept"), true);
                    break;
                case QuestState.Accepted:
                    SetAction(Loc.Get("hub.tavern.action.in-progress"), false);
                    break;
                default:
                    SetAction(Loc.Get("hub.tavern.action.collect"), true);
                    break;
            }
        }

        /// <summary>What the quest asks, in one line.</summary>
        private static string GoalLine(QuestSO quest)
        {
            switch (quest.Kind)
            {
                case QuestKind.Wipes:
                    return Loc.Format("hub.tavern.goal-wipes", ("count", quest.Count));
                case QuestKind.ClearFloors:
                    return Loc.Format("hub.tavern.goal-floors", ("count", quest.Count));
            }
            if (quest.Target != null)
            {
                string target = quest.Count == 1 ? quest.Target.Label : quest.Target.PluralName;
                return Loc.Format("hub.tavern.goal", ("count", quest.Count), ("enemy", target));
            }
            if (quest.TargetTraits != UnitTraits.None)
            {
                return Loc.Format("hub.tavern.goal-kind", ("count", quest.Count), ("kind", UnitTraitOps.DisplayName(quest.TargetTraits)));
            }
            return Loc.Format("hub.tavern.goal-any", ("count", quest.Count));
        }

        /// <summary>A quest not yet taken can be swapped for another, for gold.</summary>
        private void OnReroll()
        {
            if (_selected < 0 || _selected >= _quests.Count || _reroll == null || !_reroll.enabledSelf)
            {
                return;
            }
            if (QuestRewards.Reroll(_quests[_selected]))
            {
                SetFeedback(Loc.Get("hub.tavern.feedback.rerolled"));
            }
            else
            {
                SetFeedback(MetaProgressManager.Instance != null && MetaProgressManager.Instance.Gold < QuestOps.RerollGold
                    ? Loc.Get("hub.tavern.feedback.reroll-gold")
                    : Loc.Get("hub.tavern.feedback.reroll-none"));
            }
            Refresh(_selected);
        }

        private void SetReroll(bool offered)
        {
            if (_reroll == null)
            {
                return;
            }
            _reroll.text = Loc.Format("hub.tavern.action.reroll", ("gold", QuestOps.RerollGold));
            _reroll.style.display = offered ? DisplayStyle.Flex : DisplayStyle.None;
            bool canPay = MetaProgressManager.Instance != null && MetaProgressManager.Instance.Gold >= QuestOps.RerollGold;
            _reroll.SetEnabled(offered && canPay
                && QuestOps.CanReroll(_save, QuestRewards.Pool(), MetaProgressManager.Instance.GetCompletedRunKeys(), _quests[_selected]));
        }

        private void OnAction()
        {
            if (_selected < 0 || _selected >= _quests.Count)
            {
                return;
            }
            var quest = _quests[_selected];
            switch (QuestOps.StateOf(_save, quest))
            {
                case QuestState.Offered:
                    if (QuestRewards.Accept(quest))
                    {
                        SetFeedback(Loc.Format("hub.tavern.feedback.accepted", ("quest", quest.DisplayName)));
                    }
                    break;
                case QuestState.Ready:
                    if (QuestRewards.Collect(quest, _roster))
                    {
                        SetFeedback(Loc.Get("hub.tavern.feedback.collected"));
                    }
                    break;
                default:
                    return;
            }
            Refresh(_selected);
        }

        private void SetHead(Sprite sprite, string name, string sub)
        {
            if (_icon != null)
            {
                _icon.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : StyleKeyword.None;
            }
            if (_name != null)
            {
                _name.text = name;
            }
            if (_sub != null)
            {
                _sub.text = sub;
            }
            InventoryHubUI.ApplyRarity(_name, null);
        }

        private void SetAction(string label, bool enabled)
        {
            if (_action == null)
            {
                return;
            }
            _action.style.display = label != null ? DisplayStyle.Flex : DisplayStyle.None;
            _action.text = label ?? string.Empty;
            _action.SetEnabled(enabled);
        }

        private void SetFeedback(string text)
        {
            if (_feedback != null)
            {
                _feedback.text = text;
            }
        }

        private void AddLine(string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            _detail.Add(label);
        }
    }
}
