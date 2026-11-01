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
    /// <summary>The room's screen, by job: the victory summary and the level-clear window.</summary>
    public partial class RoomActionUI
    {
        // ============================================================
        //  VICTORY SUMMARY (loot / XP / gold, over the battle stage)
        // ============================================================

        private void ShowVictory(CombatResult result)
        {
            SetShown(_combatBar, false);
            SetShown(_detailWindow, false);

            // Escalate the copy toward the climax: run complete > boss slain > victory. Clearing the
            // exit room is not "Level Cleared!" - the level ends on the stairs, and the level-clear
            // window says so then (playtest 2 finding 6).
            if (result.RunCompleted)
            {
                _victoryTitle.text = Loc.Get("room.victory.title.run");
            }
            else if (result.BossDefeated)
            {
                _victoryTitle.text = Loc.Get("room.victory.title.boss");
            }
            else
            {
                _victoryTitle.text = Loc.Get("room.victory.title");
            }
            _victoryRewards.Clear();

            // Gold, then XP per hero, then the loot (menu review, 2026-09-28): the two numbers every
            // fight pays first, and the drops - the part worth reading - last and at full size,
            // instead of a "Loot" header above everything with the currencies trailing under it.
            // No "(banked)" on the exit fight: its gold joins the floor's pending pool like any
            // other, banked on the stairs, which is what the HUD says (playtest 2 finding 6).
            _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.gold"), Loc.Format("room.reward.plus", ("amount", result.GoldGained))));

            // The per-hero split only when there is something to split: "shared" over a party of
            // one reads as a mistake.
            if (result.XpByHero != null && result.XpByHero.Count > 1)
            {
                _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.xp"), Loc.Format("room.victory.xp-shared", ("amount", result.XpGained))));
                foreach (var share in result.XpByHero)
                {
                    var row = MakeVictoryRow(share.Key, Loc.Format("room.reward.plus", ("amount", share.Value)));
                    row.AddToClassList("cd-victory-row--sub");
                    _victoryRewards.Add(row);
                }
            }
            else
            {
                _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.xp"), Loc.Format("room.reward.plus", ("amount", result.XpGained))));
            }

            bool anyLoot = false;
            if (result.Loot != null)
            {
                foreach (var award in result.Loot)
                {
                    if (award.IsEmpty)
                    {
                        continue;
                    }
                    if (!anyLoot)
                    {
                        _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.loot"), string.Empty));
                        anyLoot = true;
                    }
                    var loot = new VisualElement();
                    loot.AddToClassList("cd-victory-loot");
                    loot.AddToClassList("cd-victory-loot--large");
                    var icon = new VisualElement();
                    icon.AddToClassList("cd-victory-loot__icon");
                    if (award.Item.Icon != null)
                    {
                        icon.style.backgroundImage = new StyleBackground(award.Item.Icon);
                    }
                    var name = new Label(award.Quantity > 1
                        ? Loc.Format("room.reward.counted", ("name", award.Item.DisplayName), ("count", award.Quantity))
                        : award.Item.DisplayName);
                    name.AddToClassList("cd-victory-loot__name");
                    name.AddToClassList(ItemPresenter.RarityClass(award.Item.Rarity));
                    loot.Add(icon);
                    loot.Add(name);
                    _victoryRewards.Add(loot);
                }
            }
            if (!anyLoot)
            {
                _victoryRewards.Add(MakeVictoryRow(Loc.Get("room.victory.loot"), Loc.Get("room.victory.loot-none")));
            }

            if (result.LevelCleared)
            {
                // After the run's last boss there is no further floor - the stairs lead out.
                _victoryRewards.Add(MakeVictoryRow(result.RunCompleted ? Loc.Get("room.victory.way-out") : Loc.Get("room.victory.way-down"), Loc.Get("room.victory.way-open")));
            }

            if (_victoryRewards is ScrollView rewardsScroll)
            {
                rewardsScroll.scrollOffset = Vector2.zero;
            }
            SetShown(_victoryWindow, true);
        }

        // ============================================================
        //  LEVEL CLEARED
        // ============================================================

        /// <summary>
        /// The moment a level ends: what the floor banked, shown before the hub loads. Everything in
        /// it is already committed by <c>DungeonManager.OnDungeonCleared</c>, so it is a statement -
        /// one button, and <paramref name="onContinue"/> is the scene change.
        ///
        /// <para>Built to make every number the hub is about to show traceable (playtest finding 6):
        /// gold is split into what the floor paid and the clear bonus, because the bonus was the
        /// +30 nobody could account for, and Essence says where it is spent.</para>
        /// </summary>
        /// <param name="onContinue">Back to Town. Null hides the button: mid-run the town is not a way
        /// on (owner, 2026-10-30) - only the run's end goes home.</param>
        /// <param name="onNextLevel">Continue: straight down to the next level. Null hides the button,
        /// leaving the town as the only way on (the run's last level).</param>
        public void ShowLevelCleared(LevelClearSummary summary, Action onContinue, Action onNextLevel = null)
        {
            if (!EnsureRefs() || _levelWindow == null || summary == null)
            {
                (onNextLevel ?? onContinue)?.Invoke();
                return;
            }

            // Nothing else is live now: the level is over, and a door clicked behind the window
            // would walk a party that no longer has a floor.
            UnsubscribeDoors();
            SetShown(_combatBar, false);
            SetShown(_detailWindow, false);
            SetShown(_eventWindow, false);
            SetShown(_victoryWindow, false);
            SetShown(_bossBanner, false);

            _levelContinueAction = onContinue;
            _levelNextLevelAction = onNextLevel;
            SetShown(_levelNextLevel, onNextLevel != null);
            SetShown(_levelContinue, onContinue != null);
            // Continue is the default when it is offered: a player clearing a run level by level
            // should not have to reach for the arrows every time.
            SetLevelChoice(onNextLevel != null);
            _levelTitle.text = summary.RunCompleted ? Loc.Get("room.level-clear.title.run") : Loc.Get("room.level-clear.title");
            SetText(_levelSubtitle, LevelClearSubtitle(summary));

            FillLevelClearBody(summary);

            SetText(_levelNext, summary.RunCompleted
                ? Loc.Get("room.level-clear.next.done")
                : string.IsNullOrEmpty(summary.NextLevelName)
                    ? Loc.Get("room.level-clear.next.story-map")
                    : onNextLevel != null
                        ? Loc.Format("room.level-clear.next.continue", ("level", summary.NextLevelName))
                        : Loc.Format("room.level-clear.next.from-map", ("level", summary.NextLevelName)));

            _levelBody.scrollOffset = Vector2.zero;
            SetShown(_levelWindow, true);
            FocusRoot();
        }

        private static string LevelClearSubtitle(LevelClearSummary summary)
        {
            if (summary.LevelCount <= 0)
            {
                return summary.LevelName ?? string.Empty;
            }
            string level = string.IsNullOrEmpty(summary.LevelName)
                ? Loc.Format("room.level-clear.subtitle.level", ("number", summary.LevelNumber), ("count", summary.LevelCount))
                : Loc.Format("room.level-clear.subtitle.named", ("name", summary.LevelName), ("number", summary.LevelNumber), ("count", summary.LevelCount));
            return string.IsNullOrEmpty(summary.RunName) ? level : Loc.Format("room.level-clear.subtitle.run", ("run", summary.RunName), ("level", level));
        }

        /// <summary>
        /// The window's body, most telling first (2026-10-22 redesign): a newcomer, then the totals as
        /// tiles, then each hero's XP with a portrait, then the finds as an icon grid. The single list
        /// it replaced pushed XP and newcomers below the scroll fold on any floor that dropped much.
        /// </summary>
        private void FillLevelClearBody(LevelClearSummary summary)
        {
            _levelJoined?.Clear();
            foreach (var hero in summary.Joined)
            {
                var row = new VisualElement();
                row.AddToClassList("cd-lc-joined__row");
                row.Add(Portrait(hero.Icon, "cd-lc-joined__portrait"));
                var text = new Label(Loc.Format("room.level-clear.joined", ("hero", hero.Label)));
                text.AddToClassList("cd-lc-joined__text");
                row.Add(text);
                _levelJoined?.Add(row);
            }

            int xpTotal = 0;
            foreach (var line in summary.Xp)
            {
                xpTotal += line.Amount;
            }
            _levelTallies?.Clear();
            AddTally("cd-lc-tally--gold", Loc.Format("room.reward.plus", ("amount", summary.GoldTotal)), Loc.Get("room.level-clear.gold"),
                Loc.Format("room.level-clear.gold-note", ("found", summary.GoldFound), ("bonus", summary.GoldBonus)));
            AddTally("cd-lc-tally--xp", Loc.Format("room.reward.plus", ("amount", xpTotal)), Loc.Get("room.level-clear.xp"), Loc.Get("room.level-clear.xp-note"));
            // Only a revisit pays Essence; a "+0" tile on every first clear would only confuse.
            if (summary.Essence > 0)
            {
                AddTally("cd-lc-tally--essence", Loc.Format("room.reward.plus", ("amount", summary.Essence)), Loc.Get("room.level-clear.essence"), Loc.Get("room.level-clear.essence-note"));
            }

            _levelXp?.Clear();
            SetShown(_levelXpHeading, summary.Xp.Count > 0);
            foreach (var line in summary.Xp)
            {
                var card = new VisualElement();
                card.AddToClassList("cd-lc-hero");
                card.Add(Portrait(line.Icon, "cd-lc-hero__portrait"));
                var text = new VisualElement();
                text.AddToClassList("cd-lc-hero__text");
                var name = new Label(line.Label);
                name.AddToClassList("cd-lc-hero__name");
                text.Add(name);
                var xp = new Label(Loc.Format("room.level-clear.hero-xp", ("amount", line.Amount)));
                xp.AddToClassList("cd-lc-hero__xp");
                text.Add(xp);
                card.Add(text);
                _levelXp?.Add(card);
            }

            // Materials first: they gate buildings, and they are what a floor is for finding.
            _levelLoot?.Clear();
            AddLootCells(summary.Materials);
            AddLootCells(summary.Items);
            if (summary.Materials.Count == 0 && summary.Items.Count == 0)
            {
                var none = new Label(Loc.Get("room.level-clear.no-loot"));
                none.AddToClassList("cd-lc-empty");
                _levelLoot?.Add(none);
            }
        }

        private void AddTally(string kindClass, string value, string label, string note)
        {
            var tile = new VisualElement();
            tile.AddToClassList("cd-lc-tally");
            tile.AddToClassList(kindClass);
            var v = new Label(value);
            v.AddToClassList("cd-lc-tally__value");
            tile.Add(v);
            var l = new Label(label);
            l.AddToClassList("cd-lc-tally__label");
            tile.Add(l);
            var n = new Label(note);
            n.AddToClassList("cd-lc-tally__note");
            tile.Add(n);
            _levelTallies?.Add(tile);
        }

        private void AddLootCells(List<LevelClearSummary.Line> lines)
        {
            foreach (var line in lines)
            {
                var cell = new VisualElement();
                cell.AddToClassList("cd-lc-item");
                cell.Add(Portrait(line.Icon, "cd-lc-item__icon"));
                var name = new Label(line.Label);
                name.AddToClassList("cd-lc-item__name");
                if (line.Item != null)
                {
                    name.AddToClassList(ItemPresenter.RarityClass(line.Item.Rarity));
                }
                cell.Add(name);
                var count = new Label(Loc.Format("room.level-clear.loot-count", ("count", line.Amount)));
                count.AddToClassList("cd-lc-item__count");
                cell.Add(count);
                _levelLoot?.Add(cell);
            }
        }

        private static VisualElement Portrait(Sprite sprite, string className)
        {
            var image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.AddToClassList(className);
            if (sprite != null)
            {
                image.style.backgroundImage = new StyleBackground(sprite);
            }
            return image;
        }

        private void OnLevelClearContinue()
        {
            var action = _levelContinueAction;
            _levelContinueAction = null;
            _levelNextLevelAction = null;
            SetShown(_levelWindow, false);
            action?.Invoke();
        }

        private void OnLevelClearNextLevel()
        {
            var action = _levelNextLevelAction;
            if (action == null)
            {
                return;
            }
            _levelContinueAction = null;
            _levelNextLevelAction = null;
            SetShown(_levelWindow, false);
            action.Invoke();
        }

        /// <summary>Moves the keyboard highlight between Back to Town and Continue.</summary>
        private void SetLevelChoice(bool next)
        {
            _levelChoiceIsNext = next;
            _levelNextLevel?.EnableInClassList("cd-nav--selected", next);
            _levelContinue?.EnableInClassList("cd-nav--selected", !next);
        }

        private VisualElement MakeVictoryRow(string label, string value)
        {
            var row = new VisualElement();
            row.AddToClassList("cd-victory-row");
            var l = new Label(label);
            l.AddToClassList("cd-victory-row__label");
            var v = new Label(value);
            v.AddToClassList("cd-victory-row__value");
            row.Add(l);
            row.Add(v);
            return row;
        }

        private void OnVictoryContinue()
        {
            SetShown(_victoryWindow, false);
            CombatManager.Instance.FinishVictory();
            ShowPartyStatusOutOfCombat();
            // Always back to the room, even after clearing the exit: the level completes when the
            // player takes the stairs.
            SettleRoom();
            SubscribeDoors();
        }
    }
}
