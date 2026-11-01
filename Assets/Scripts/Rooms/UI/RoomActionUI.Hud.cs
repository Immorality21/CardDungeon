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
    /// <summary>The room's screen, by job: the dungeon HUD, top-left while walking.</summary>
    public partial class RoomActionUI
    {
        // ============================================================
        //  DUNGEON HUD (top-left, while walking the floor)
        // ============================================================

        /// <summary>
        /// Where the party is and what the floor has paid so far: the level's name, the run and
        /// "level N of M", the gold found on this floor, and the map's explored/frontier line.
        ///
        /// <para>Added 2026-09-28 (playtest finding 7): the room floated in a black screen with no
        /// level name and no counter, so gold from an event had nowhere to show up. The gold shown is
        /// the floor's <b>pending</b> pool and says so - it is only banked on the stairs and a wipe
        /// forfeits it, which is the thing the player needs to know about it. The exploration line is
        /// <see cref="DungeonMapOps.StatusLine"/>, the floor map's own, so the HUD can never reveal
        /// more than the map (no total room count).</para>
        ///
        /// <para>Names and the exploration line change only when the party changes room, so they are
        /// rebuilt from <see cref="Show"/>; gold can change on any click (a cache, an event), so
        /// <see cref="TickHud"/> polls it and rewrites the label only when it moved.</para>
        /// </summary>
        private void RefreshHud()
        {
            if (_hud == null)
            {
                return;
            }

            var dungeon = DungeonManager.HasInstance ? DungeonManager.Instance : null;
            var entry = dungeon != null ? dungeon.CurrentLevelEntry : null;
            var run = DungeonManager.ActiveRun;

            string levelName = entry != null && !string.IsNullOrEmpty(entry.LevelName)
                ? entry.LevelName
                : dungeon != null && dungeon.CurrentLevel != null ? SpacedName(dungeon.CurrentLevel.name) : Loc.Get("room.hud.level-default");
            SetText(_hudLevel, levelName);

            string runLine = string.Empty;
            if (run != null && run.Levels.Count > 0)
            {
                string runName = !string.IsNullOrEmpty(run.DisplayName) ? run.DisplayName : run.name;
                runLine = Loc.Format("room.hud.run-line", ("run", runName), ("level", DungeonManager.RunLevelIndex + 1), ("total", run.Levels.Count));
            }
            SetText(_hudRun, runLine);
            SetShown(_hudRun, !string.IsNullOrEmpty(runLine));

            var rooms = dungeon != null ? dungeon.CurrentRooms : null;
            var model = DungeonMapOps.Build(BuildMapInputs(rooms), _currentRoom != null ? _currentRoom.RoomIndex : -1);
            string explore = DungeonMapOps.StatusLine(model);
            SetText(_hudExplore, string.IsNullOrEmpty(explore) ? Loc.Get("room.hud.map-key") : Loc.Format("room.hud.explore", ("status", explore)));

            _hudGoldShown = -1; // force the gold label to redraw
            TickHud();
        }

        /// <summary>
        /// "CollapsedCaverns" -> "Collapsed Caverns": a level with no run entry (a sandbox, a test
        /// floor) has only its asset name to show.
        /// </summary>
        private static string SpacedName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return string.Empty;
            }
            var sb = new System.Text.StringBuilder(assetName.Length + 4);
            for (int i = 0; i < assetName.Length; i++)
            {
                char c = assetName[i];
                if (c == '_')
                {
                    sb.Append(' ');
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && char.IsLower(assetName[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Every panel the camera should keep the party's room out from under, most important first,
        /// in viewport space: the Fight/Flee bar, the HUD and the party window - only
        /// the ones that are up (playtest 2 finding 8: the bar covered a door and an enemy).
        /// </summary>
        public void GetCameraSafePanels(List<Rect> panels)
        {
            panels.Clear();
            foreach (var element in new[] { _combatBar, _hud, _partyStatus })
            {
                if (TryGetViewportRect(element, out Rect rect))
                {
                    panels.Add(rect);
                }
            }
        }

        /// <summary>
        /// An element's screen rectangle in viewport space. UI Toolkit's origin is top-left, the
        /// viewport's bottom-left. False while it is hidden or not laid out yet.
        /// </summary>
        private static bool TryGetViewportRect(VisualElement element, out Rect rect)
        {
            rect = default;
            if (element == null || element.panel == null || !IsShown(element))
            {
                return false;
            }

            Rect screen = element.panel.visualTree.worldBound;
            Rect box = element.worldBound;
            if (screen.width <= 0f || screen.height <= 0f || box.width <= 0f || float.IsNaN(box.width))
            {
                return false;
            }

            rect = Rect.MinMaxRect(
                box.xMin / screen.width,
                1f - box.yMax / screen.height,
                box.xMax / screen.width,
                1f - box.yMin / screen.height);
            return true;
        }

        /// <summary>Per frame: the HUD is up only while walking, and its gold follows the pending pool.</summary>
        private void TickHud()
        {
            if (_hud == null)
            {
                return;
            }

            bool inCombat = CombatManager.HasInstance && CombatManager.Instance.InCombat;
            bool visible = _currentRoom != null && !inCombat
                && !IsShown(_victoryWindow) && !IsShown(_levelWindow);
            if (IsShown(_hud) != visible)
            {
                SetShown(_hud, visible);
            }
            if (!visible)
            {
                return;
            }

            int gold = MetaProgressManager.HasInstance ? MetaProgressManager.Instance.PendingRunGold : 0;
            if (gold != _hudGoldShown)
            {
                _hudGoldShown = gold;
                // The "banked when you take the stairs" lesson only while the tutorial runs: on every
                // frame of every floor it was teaching text that never went away.
                bool teaching = MetaProgressManager.HasInstance && !MetaProgressManager.Instance.TutorialFinished;
                SetText(_hudGold, teaching
                    ? Loc.Format("room.hud.gold-teaching", ("amount", gold))
                    : Loc.Format("room.hud.gold", ("amount", gold)));
            }
        }
    }
}
