using System.Collections.Generic;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Every key and mouse action in the game, grouped by where it applies: the one list the
    /// Controls screen shows in the dungeon's pause menu, on the title screen's Options and in town
    /// (F1 or the town's Controls button). Playtest 4 finding 58 and the owner's call of 2026-11-10:
    /// one screen that lists everything, instead of one-off hints.
    ///
    /// <para>Holds text-table keys only, never wording (<c>controls.*</c> in TextTable.asset). Each
    /// line was read off the code that handles the input (RoomActionUI's partials, PartyWalk,
    /// PotionWindow, MagicSelectionUI, KeyboardNavigator, HubManager, SphereGridUI, InventoryHubUI,
    /// CampaignMapUI). <b>A new binding is a new line here plus its two table entries</b>;
    /// <c>ControlsListTests</c> fails on a key the table does not have.</para>
    /// </summary>
    public static class ControlsList
    {
        /// <summary>One row: the keys or mouse action, and what it does.</summary>
        public readonly struct Line
        {
            public readonly string KeysKey;
            public readonly string ActionKey;

            public Line(string keysKey, string actionKey)
            {
                KeysKey = keysKey;
                ActionKey = actionKey;
            }
        }

        /// <summary>A heading and its rows.</summary>
        public sealed class Group
        {
            public readonly string TitleKey;
            public readonly IReadOnlyList<Line> Lines;

            public Group(string titleKey, params Line[] lines)
            {
                TitleKey = titleKey;
                Lines = lines;
            }
        }

        private static readonly Group[] AllGroups =
        {
            new Group("controls.group.dungeon",
                new Line("controls.dungeon.walk.keys", "controls.dungeon.walk.action"),
                new Line("controls.dungeon.click-walk.keys", "controls.dungeon.click-walk.action"),
                new Line("controls.dungeon.click-door.keys", "controls.dungeon.click-door.action"),
                new Line("controls.dungeon.use.keys", "controls.dungeon.use.action"),
                new Line("controls.dungeon.flee.keys", "controls.dungeon.flee.action"),
                new Line("controls.dungeon.map.keys", "controls.dungeon.map.action"),
                new Line("controls.dungeon.potions.keys", "controls.dungeon.potions.action"),
                new Line("controls.dungeon.pause.keys", "controls.dungeon.pause.action")),
            new Group("controls.group.combat",
                new Line("controls.combat.fight.keys", "controls.combat.fight.action"),
                new Line("controls.combat.command.keys", "controls.combat.command.action"),
                new Line("controls.combat.list.keys", "controls.combat.list.action"),
                new Line("controls.combat.confirm.keys", "controls.combat.confirm.action"),
                new Line("controls.combat.back.keys", "controls.combat.back.action"),
                new Line("controls.combat.click.keys", "controls.combat.click.action"),
                new Line("controls.combat.inspect.keys", "controls.combat.inspect.action"),
                new Line("controls.combat.pause.keys", "controls.combat.pause.action"),
                new Line("controls.combat.victory.keys", "controls.combat.victory.action")),
            new Group("controls.group.town",
                new Line("controls.town.cursor.keys", "controls.town.cursor.action"),
                new Line("controls.town.tab.keys", "controls.town.tab.action"),
                new Line("controls.town.press.keys", "controls.town.press.action"),
                new Line("controls.town.back.keys", "controls.town.back.action"),
                new Line("controls.town.click.keys", "controls.town.click.action"),
                new Line("controls.town.page.keys", "controls.town.page.action"),
                new Line("controls.town.heroes.keys", "controls.town.heroes.action"),
                new Line("controls.town.tabs.keys", "controls.town.tabs.action"),
                new Line("controls.town.controls.keys", "controls.town.controls.action")),
            new Group("controls.group.maps",
                new Line("controls.maps.move.keys", "controls.maps.move.action"),
                new Line("controls.maps.drag.keys", "controls.maps.drag.action"),
                new Line("controls.maps.wheel.keys", "controls.maps.wheel.action"),
                new Line("controls.maps.pan.keys", "controls.maps.pan.action"),
                new Line("controls.maps.zoom.keys", "controls.maps.zoom.action"),
                new Line("controls.maps.begin.keys", "controls.maps.begin.action"),
                new Line("controls.maps.revisit.keys", "controls.maps.revisit.action")),
        };

        /// <summary>Every group, in the order the screen shows them.</summary>
        public static IReadOnlyList<Group> Groups => AllGroups;
    }
}
