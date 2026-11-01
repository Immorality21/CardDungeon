using ImmoralityGaming.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Combat;
using Assets.Scripts.Audio;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Assets.Scripts.Events;

namespace Assets.Scripts.Rooms
{
    /// <summary>The room's screen, by job: what a press or a look at the screen does, for the bot (docs/BOT.md).</summary>
    public partial class RoomActionUI
    {
        // ============================================================
        //  BOT (docs/BOT.md): what a press or a look at the screen does
        // ============================================================

        public Room CurrentRoom => _currentRoom;
        public Events.RoomEventSO CurrentEvent => _currentEvent;
        public float CurrentEventChance => _eventChance;
        public bool IsVictoryUp => IsShown(_victoryWindow);
        public bool IsLevelClearUp => IsShown(_levelWindow);
        public bool IsEventUp => IsShown(_eventWindow);
        public bool IsDetailUp => IsShown(_detailWindow);
        public bool IsFightBarUp => IsShown(_combatBar);
        public bool CanWalk => DoorNavActive();

        /// <summary>A step, a path or a click's walk is still under way.</summary>
        public bool IsWalking => _walk != null && (_walk.IsStepping || _walk.HasPath)
            || _walkGoalDoor != null || _walkGoalRoom != null || _walkUseTile != null;

        public void PressFight()
        {
            OnFight();
        }

        /// <summary>A drink from the potion window, without the window.</summary>
        public void DrinkPotion(ItemSO item, Heroes.Hero hero)
        {
            if (UI.PotionWindow.DrinkNow(item, hero))
            {
                OnPotionDrunk();
            }
        }

        public void PressVictoryContinue()
        {
            OnVictoryContinue();
        }

        public void PressDetailOk()
        {
            _detailOkAction?.Invoke();
        }

        /// <summary>The one level-clear button a window offers.</summary>
        public void PressLevelClear()
        {
            if (_levelNextLevelAction != null)
            {
                OnLevelClearNextLevel();
            }
            else
            {
                OnLevelClearContinue();
            }
        }

        /// <summary>Whether the event window offers this option as a button that works.</summary>
        public bool CanTakeEventOption(Events.RoomEventOption option)
        {
            return option != null && !string.IsNullOrEmpty(option.Label) && BlockOf(option) == Events.OptionBlock.None;
        }

        /// <summary>Takes an option of the open event; below 0 leaves it by its Back button.</summary>
        public void PressEventOption(int index)
        {
            if (index >= 0)
            {
                OnEventOptionChosen(index);
            }
            else
            {
                OnEventBack();
            }
        }

        /// <summary>Walks up to what stands on <paramref name="tile"/> and uses it, as a click on it does.</summary>
        public void WalkToTile(Vector2Int tile)
        {
            if (!TryWalkToWallEvent(tile))
            {
                SetWalkGoal(null, _currentRoom, tile);
            }
        }

        /// <summary>A turn the command menu did not take still has to clear the menu.</summary>
        public void HideCommands()
        {
            SetShown(_heroBar, false);
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
    }
}
