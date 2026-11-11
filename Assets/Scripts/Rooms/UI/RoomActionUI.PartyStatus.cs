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
    /// <summary>The room's screen, by job: the party window.</summary>
    public partial class RoomActionUI
    {
        // ============================================================
        //  PARTY STATUS WINDOW (FF-style, bottom-left)
        // ============================================================

        /// <summary>
        /// One row per hero: portrait, name, HP as a number and a slim bar, and any level affliction
        /// as a chip ("STR -2") - an event's lasting penalty used to vanish with its dialog.
        /// </summary>
        private void BuildPartyStatus(Heroes.Party party)
        {
            _partyStatusRows?.Clear();
            _partyRows.Clear();
            _partyHpLabels.Clear();
            _partyHpFills.Clear();
            _partyAfflictions.Clear();
            _partyGaugeFills.Clear();
            if (party == null || _partyStatusRows == null)
            {
                return;
            }

            foreach (var hero in party.Heroes)
            {
                if (hero == null)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("cd-party-row");

                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("cd-party-row__portrait");
                if (hero.Icon != null)
                {
                    portrait.style.backgroundImage = new StyleBackground(hero.Icon);
                }
                row.Add(portrait);

                var main = new VisualElement { pickingMode = PickingMode.Ignore };
                main.AddToClassList("cd-party-row__main");
                var top = new VisualElement { pickingMode = PickingMode.Ignore };
                top.AddToClassList("cd-party-row__top");
                var name = new Label(hero.DisplayName);
                name.AddToClassList("cd-party-row__name");
                var hp = new Label();
                hp.AddToClassList("cd-party-row__hp");
                top.Add(name);
                top.Add(hp);
                main.Add(top);

                // Bars in a group of their own, so the fight's one-line row can stand them beside the name.
                var bars = new VisualElement { pickingMode = PickingMode.Ignore };
                bars.AddToClassList("cd-party-row__bars");
                main.Add(bars);

                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("cd-party-row__bar");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("cd-party-row__fill");
                bar.Add(fill);
                bars.Add(bar);

                // The Ultra gauge, under the health bar, only for a hero who knows an Ultra.
                if (hero.KnownUltras.Count > 0)
                {
                    var gauge = new VisualElement { pickingMode = PickingMode.Ignore };
                    gauge.AddToClassList("cd-party-row__gauge");
                    var gaugeFill = new VisualElement { pickingMode = PickingMode.Ignore };
                    gaugeFill.AddToClassList("cd-party-row__gauge-fill");
                    gauge.Add(gaugeFill);
                    bars.Add(gauge);
                    _partyGaugeFills[hero] = gaugeFill;
                }

                var afflictions = new VisualElement { pickingMode = PickingMode.Ignore };
                afflictions.AddToClassList("cd-party-row__afflictions");
                main.Add(afflictions);
                row.Add(main);

                _partyStatusRows.Add(row);
                _partyRows[hero] = row;
                _partyHpLabels[hero] = hp;
                _partyHpFills[hero] = fill;
                _partyAfflictions[hero] = afflictions;
            }

            RefreshPartyStatus();
        }

        private void RefreshPartyStatus()
        {
            var tracker = DungeonManager.HasInstance ? DungeonManager.Instance.Afflictions : null;
            foreach (var pair in _partyHpLabels)
            {
                var hero = pair.Key;
                var hp = pair.Value;
                if (hero == null || hp == null)
                {
                    continue;
                }
                int max = Mathf.Max(1, hero.GetEffectiveMaxHealth());
                // Overkill leaves Health below zero; the row reads 0, as the plate above the hero does.
                hp.text = Loc.Format("room.party.hp", ("current", Mathf.Max(0, hero.Stats.Health)), ("max", max));
                if (_partyRows.TryGetValue(hero, out var row) && row != null)
                {
                    row.EnableInClassList("cd-party-row--dead", !hero.IsAlive);
                }
                if (_partyHpFills.TryGetValue(hero, out var fill) && fill != null)
                {
                    float ratio = Mathf.Clamp01((float)hero.Stats.Health / max);
                    fill.style.width = Length.Percent(ratio * 100f);
                    fill.EnableInClassList("cd-party-row__fill--mid", ratio <= 0.5f && ratio > 0.25f);
                    fill.EnableInClassList("cd-party-row__fill--low", ratio <= 0.25f);
                }
                if (_partyGaugeFills.TryGetValue(hero, out var gaugeFill) && gaugeFill != null)
                {
                    // Outside a fight there is no gauge (it starts every fight empty), so it reads empty.
                    var combat = CombatManager.HasInstance ? CombatManager.Instance : null;
                    int gauge = combat != null && combat.InCombat ? combat.UltraGauge(hero) : 0;
                    // A hero who builds Focus (the Mage) spends that on his Ultra, so the bar shows it instead.
                    var focusRule = hero.HeroSO != null ? hero.HeroSO.Focus : null;
                    if (focusRule != null && focusRule.IsActive)
                    {
                        int focus = combat != null && combat.InCombat ? combat.FocusOf(hero) : 0;
                        gauge = Mathf.RoundToInt(focus * (float)Cards.UltraOps.Max / focusRule.Max);
                    }
                    gaugeFill.style.width = Length.Percent(gauge * 100f / Cards.UltraOps.Max);
                    gaugeFill.EnableInClassList("cd-party-row__gauge-fill--full", Cards.UltraOps.IsFull(gauge));
                    gaugeFill.EnableInClassList("cd-party-row__gauge-fill--active",
                        combat != null && combat.InCombat && combat.IsTransformed(hero));
                }
                if (_partyAfflictions.TryGetValue(hero, out var chips) && chips != null)
                {
                    chips.Clear();
                    if (tracker != null)
                    {
                        foreach (var affliction in tracker.For(hero.HeroKey))
                        {
                            var handler = Cards.Buffs.BuffHandlerRegistry.Get(affliction.Buff);
                            string text = handler != null
                                ? handler.GetDisplayText(affliction.Amount).TrimEnd('!')
                                : affliction.Buff.ToString();
                            var chip = new Label(text) { pickingMode = PickingMode.Ignore, tooltip = Loc.Format("room.party.affliction-tooltip", ("effect", text)) };
                            chip.AddToClassList("cd-party-row__affliction");
                            chips.Add(chip);
                        }
                    }
                }
            }
        }

        private void HighlightActiveHero(ICombatUnit active)
        {
            foreach (var pair in _partyRows)
            {
                if (pair.Value == null)
                {
                    continue;
                }
                pair.Value.EnableInClassList("cd-party-row--active", ReferenceEquals(pair.Key, active));
            }
        }

        private void OnTurnExecuted()
        {
            // The turn is over: the row stops claiming it, or the hero stayed lit through the
            // enemy's turn that follows. The next hero turn lights its own row.
            HighlightActiveHero(null);
            RefreshPartyStatus();
        }

        private void OnCombatEnded(CombatResult result)
        {
            CombatManager.Instance.OnCombatEnded -= OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted -= OnHeroTurnStarted;
            CombatManager.Instance.OnSummonStarted -= OnSummonStarted;
            CombatManager.Instance.OnEnemyAbilityUsed -= OnEnemyAbilityUsed;
            SetShown(_abilityBanner, false);
            CombatManager.Instance.OnSummonEnded -= OnSummonEnded;
            SetShown(_summonBanner, false);
            CombatManager.Instance.OnTurnExecuted -= OnTurnExecuted;
            CombatManager.Instance.OnTurnOrderChanged -= OnTurnOrderChanged;
            SetShown(_heroBar, false);
            SetShown(_turnOrder, false);
            // The party window survives the fight - it is the exploring HUD now, not a combat-only
            // panel - so it keeps its rows and only drops the whose-turn-is-it highlight.
            HighlightActiveHero(null);
            RefreshPartyStatus();
            _turnOrderList?.Clear();
            _commandList?.Clear();

            switch (result.Outcome)
            {
                case CombatOutcome.Victory:
                    ShowVictory(result);
                    break;
                case CombatOutcome.PlayerDied:
                    ShowDeathScreen();
                    break;
            }
        }

        private void ShowDeathScreen()
        {
            SetShown(_combatBar, false);
            // What dying costs and what it keeps, as rows (2026-09-30) - it used to be the generic
            // detail dialog with the raw combat log dumped in it.
            // A painted scene rather than the dungeon behind a dimmed dialog: the fire is going out
            // and the gear lies where it fell. The dialog docks left so it stays in view; nothing
            // follows this screen but the hub, so neither change is ever undone.
            SetShown(_deathBackdrop, true);
            Assets.Scripts.Hub.UI.BackdropAmbienceView.AttachTo(_deathBackdrop)?.Show("cd-bg--death");
            _detailWindow?.AddToClassList("cd-window--death");
            ShowDetail(Loc.Get("room.death.title"), DefeatSummary() + FirstDeathLesson() + ReplayHint());
            _detailOk.text = Loc.Get("room.death.return");
            AddDefeatRows();
            _detailOkAction = () =>
            {
                if (DungeonManager.HasInstance)
                {
                    DungeonManager.Instance.HandlePartyDeath();
                }
                // A wipe returns to the hub like any other way out of a run - death costs the
                // floor's gains, not your place in the game.
                SceneManager.LoadScene("HubScene");
            };
            SetShown(_detailWindow, true);
            // Reclaim the keyboard from the selection picker, as the victory window does: without it
            // Enter did not close this screen (playtest 4 finding 9).
            FocusRoot();
        }

        /// <summary>
        /// The first wipe of a save says that dying is expected (a playtest finding: a new player read
        /// it as failure). Read before <c>HandlePartyDeath</c> counts this one, so 0 means first.
        /// </summary>
        private static string FirstDeathLesson()
        {
            if (!MetaProgressManager.HasInstance || MetaProgressManager.Instance.PartyDeaths > 0)
            {
                return string.Empty;
            }
            var tutorial = UnityEngine.Resources.Load<Tutorial.TutorialSO>(Tutorial.TutorialSO.ResourcePath);
            string text = tutorial != null ? tutorial.TextFor(Tutorial.TutorialCue.FirstDeath) : string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            MetaProgressManager.Instance.MarkHintSeen(Tutorial.TutorialCue.FirstDeath);
            return "\n\n" + text;
        }

        /// <summary>
        /// The first wipe in a run built as a wall to come back to (<c>RunDefinitionSO.ReplayHintOnWipe</c>, the
        /// Catacombs) says how to get past it: replay earlier runs for XP and gear. Once per save.
        /// </summary>
        private static string ReplayHint()
        {
            var run = DungeonManager.ActiveRun;
            // Only once the tutorial is over: until then there is no earlier run to replay.
            if (run == null || !run.ReplayHintOnWipe || !MetaProgressManager.HasInstance
                || !MetaProgressManager.Instance.TutorialFinished
                || MetaProgressManager.Instance.ReplayHintShown)
            {
                return string.Empty;
            }
            var tutorial = UnityEngine.Resources.Load<Tutorial.TutorialSO>(Tutorial.TutorialSO.ResourcePath);
            string text = tutorial != null ? tutorial.TextFor(Tutorial.TutorialCue.ReplayToGetStronger) : string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            MetaProgressManager.Instance.MarkReplayHintShown();
            return "\n\n" + text;
        }

        private static string DefeatSummary()
        {
            var entry = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentLevelEntry : null;
            string where = entry != null && !string.IsNullOrEmpty(entry.LevelName) ? entry.LevelName : Loc.Get("room.death.where-default");
            return DungeonManager.ActiveRun != null
                ? Loc.Format("room.death.summary-run", ("where", where))
                : Loc.Format("room.death.summary", ("where", where));
        }

        /// <summary>Kept and lost, in the same rows the victory window uses.</summary>
        private void AddDefeatRows()
        {
            if (_detailRows == null)
            {
                return;
            }
            _detailRows.Clear();
            AddRunStoryRows();
            int cleared = DungeonManager.RunLevelIndex;
            if (DungeonManager.ActiveRun != null && cleared > 0)
            {
                _detailRows.Add(MakeVictoryRow(Loc.Format("room.death.row.level-gold", ("level", cleared)),
                    Loc.Format("room.reward.plus", ("amount", MetaProgressManager.RunProgressGoldOnDeath(cleared)))));
            }
            _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.earlier-floors"), Loc.Get("room.death.kept")));
            int pending = MetaProgressManager.HasInstance ? MetaProgressManager.Instance.PendingRunGold : 0;
            if (pending > 0)
            {
                _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.floor-gold"), Loc.Get("room.death.lost")));
            }
            _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.floor-gains"), Loc.Get("room.death.lost")));

            // Point at the way to get stronger. Nothing after a wipe used to link losing to the
            // sphere grid (revisit playtest finding 5). The XP is read off disk: saves are deferred,
            // so disk holds what survives this wipe. Worded "XP to spend", never "banked" (the owner
            // wants that word out of the game, playtest 3 finding 17) nor "gained" (it read as XP for
            // the lost fight, 2026-11-04).
            int unspent = UnspentPartyXp();
            _detailRows.Add(MakeVictoryRow(unspent > 0 ? Loc.Format("room.death.row.xp", ("xp", unspent)) : Loc.Get("room.death.row.grow"),
                Loc.Get("room.death.row.xp-where")));
            SetShown(_detailRows, true);
        }

        /// <summary>
        /// Where the run ended (docs/plans/POLISH_CONTENT.md §15). The "Fell to" foes and the run's
        /// kill/fight tally were cut (playtest 3 finding 17): the owner does not want them, and the
        /// extra rows pushed the Return button off the bottom of the screen.
        /// </summary>
        private void AddRunStoryRows()
        {
            if (DungeonManager.ActiveRun == null)
            {
                return;
            }
            _detailRows.Add(MakeVictoryRow(Loc.Get("room.death.row.fell-on"),
                Loc.Format("room.death.row.floor", ("floor", DungeonManager.RunLevelIndex + 1), ("total", DungeonManager.ActiveRun.Levels.Count))));
        }

        /// <summary>The fielded heroes' unspent XP as it stands on disk - what a wipe leaves them.</summary>
        private static int UnspentPartyXp()
        {
            var party = GameManager.HasInstance ? GameManager.Instance.Party : null;
            if (party == null)
            {
                return 0;
            }
            int total = 0;
            foreach (var hero in party.Heroes)
            {
                if (hero != null && hero.HeroSO != null)
                {
                    total += Heroes.HeroRoster.GetHeroSave(hero.HeroSO).CurrentXp;
                }
            }
            return total;
        }

        private void OnFlee()
        {
            var party = GameManager.Instance.Party;

            if (!CombatManager.Instance.CanFlee(party))
            {
                SetShown(_combatBar, false);
                ShowCombatResult(Loc.Get("room.flee.title"), Loc.Get("room.flee.nowhere"), showNormalAfter: false, returnToCombat: true);
                return;
            }

            SetShown(_combatBar, false);
            UnsubscribeDoors();

            // The key that fled - the arrow pointing back out - is still down when the party lands,
            // and read as a walk it carried them a step or more past the doorway: Flee seemed to go
            // twice as far (playtest 4 finding 33). It walks again once it has been let go.
            _walk?.HoldUntilReleased();

            CombatManager.Instance.Flee(party, _entryDoor, _currentRoom);
        }

        private void OnEntryDoorFlee(Door door)
        {
            OnFlee();
        }

        private void ShowCombatResult(string title, string message, bool showNormalAfter, bool returnToCombat = false)
        {
            SetShown(_combatBar, false);
            _detailNav?.Reset();
            _detailTitle.text = title;
            _detailMessage.text = message;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                if (showNormalAfter)
                {
                    _currentRoom.EnableAllDoors();
                    SettleRoom();
                    SubscribeDoors();
                }
                else if (returnToCombat)
                {
                    SetShown(_combatBar, true);
                }
            };
            SetShown(_detailWindow, true);
        }
    }
}
