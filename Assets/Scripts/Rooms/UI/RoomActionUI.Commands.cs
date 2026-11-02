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
    /// <summary>The room's screen, by job: the fight's bars, the command menu, the turn order and the hotkeys.</summary>
    public partial class RoomActionUI
    {
        // ============================================================
        //  COMBAT
        // ============================================================

        private void OnFight()
        {
            var party = GameManager.Instance.Party;

            HideAll();

            CombatManager.Instance.OnCombatEnded += OnCombatEnded;
            CombatManager.Instance.OnHeroTurnStarted += OnHeroTurnStarted;
            CombatManager.Instance.OnSummonStarted += OnSummonStarted;
            CombatManager.Instance.OnEnemyAbilityUsed += OnEnemyAbilityUsed;
            CombatManager.Instance.OnSummonEnded += OnSummonEnded;
            CombatManager.Instance.OnTurnExecuted += OnTurnExecuted;
            CombatManager.Instance.OnTurnOrderChanged += OnTurnOrderChanged;

            BuildPartyStatus(party);
            SetShown(_partyStatus, true);
            SetShown(_turnOrder, true);

            CombatManager.Instance.StartCombat(party, _currentRoom);
        }

        private void OnHeroTurnStarted(ICombatUnit hero)
        {
            _currentHeroTurn = hero;
            _heroTitle.text = Loc.Format("room.combat.turn-title", ("hero", hero.DisplayName));
            HighlightActiveHero(hero);
            RefreshPartyStatus();

            BuildCommandMenu(hero);

            SetShown(_heroBar, true);
            FocusRoot();
        }

        private void OnHeroAttack()
        {
            SetShown(_heroBar, false);

            var enemies = CombatManager.Instance.GetTargetableEnemies();
            if (enemies.Count <= 1)
            {
                CombatManager.Instance.SubmitAttackAction(enemies.Count == 1 ? enemies[0] : null);
                return;
            }

            CombatManager.Instance.RequestAttackTargets(_currentHeroTurn, enemies);
        }

        private void OnHeroMagic()
        {
            SetShown(_heroBar, false);

            var heroComponent = _currentHeroTurn as Heroes.Hero;
            if (heroComponent == null || !DungeonManager.HasInstance || DungeonManager.Instance.MagicState == null)
            {
                SetShown(_heroBar, true);
                return;
            }

            var slots = new List<Cards.MagicSlot>(DungeonManager.Instance.MagicState.GetSlots(heroComponent.HeroKey));
            // An Ultra's form adds its abilities after the real slots - appended, so a real slot's
            // index (what a cast spends a charge from) never moves. Casting one spends nothing:
            // EquippedMagicState.TryCast refuses an index past the hero's slots.
            foreach (var ability in CombatManager.Instance.FormAbilities(_currentHeroTurn))
            {
                slots.Add(new Cards.MagicSlot { Magic = ability, Charges = 1, MaxCharges = 1, Unlimited = true });
            }
            CombatManager.Instance.RequestMagicSlots(_currentHeroTurn, slots);
        }

        /// <summary>The hero's Ultras, always as a list - even one - as FFX's Overdrive menu does, so
        /// the player reads what it does before spending a full gauge on it.</summary>
        private void OnHeroUltra()
        {
            var ultras = CombatManager.Instance.KnownUltras(_currentHeroTurn);
            if (ultras.Count == 0 || !CombatManager.Instance.CanUseUltra(_currentHeroTurn))
            {
                return;
            }
            SetShown(_heroBar, false);
            CombatManager.Instance.RequestUltraList(_currentHeroTurn, ultras);
        }

        /// <summary>
        /// Summons have no target picker - they land on a whole side, or replace the party. The
        /// command always opens the summon list, even for one (the owner's call, like FFX): it shows
        /// every summon the hero knows, spent ones greyed, and the footer says what it does before a
        /// run's charge is spent.
        /// </summary>
        private void OnHeroSummon()
        {
            var heroComponent = _currentHeroTurn as Heroes.Hero;
            var summons = DungeonManager.HasInstance ? DungeonManager.Instance.Summons : null;
            var known = heroComponent != null && summons != null
                ? summons.GetSummons(heroComponent.HeroKey)
                : new List<Cards.SummonSlot>();
            var usable = known.FindAll(s => s.CanUse);
            if (usable.Count == 0)
            {
                return;
            }
            SetShown(_heroBar, false);
            CombatManager.Instance.RequestSummonList(_currentHeroTurn, known);
        }

        /// <summary>
        /// One of a party-replacing summon's abilities. A single-enemy ability with more than one
        /// enemy standing goes through the target picker; everything else knows its targets.
        /// </summary>
        private void OnSummonAbility(Cards.MagicSO ability)
        {
            var summon = _currentHeroTurn as SummonUnit;
            if (summon == null || ability == null)
            {
                return;
            }
            SetShown(_heroBar, false);
            var targets = CombatManager.Instance.SummonAbilityTargets(ability, summon);
            if (ability.TargetType == Cards.MagicTargetType.SingleEnemy && targets.Count > 1)
            {
                CombatManager.Instance.RequestSummonAbilityTarget(summon, ability, targets);
                return;
            }
            // A revive asks whom, even of one - the list says who is down.
            if (ability.TargetType == Cards.MagicTargetType.FallenAlly && targets.Count > 0)
            {
                CombatManager.Instance.RequestSummonAbilityTarget(summon, ability, targets);
                return;
            }
            CombatManager.Instance.SubmitSummonAbility(ability, targets);
        }

        private void OnSummonDismiss()
        {
            SetShown(_heroBar, false);
            CombatManager.Instance.SubmitDismiss();
        }

        private void OnSummonStarted(ICombatUnit caster, Cards.SummonSO summon)
        {
            if (_summonBannerName != null)
            {
                _summonBannerName.text = summon != null ? summon.Label : "";
            }
            SetShown(_summonBanner, true);
        }

        private void OnSummonEnded()
        {
            SetShown(_summonBanner, false);
        }

        // Long enough to read a two-word name while the projectile flies; gone before the next turn.
        private const long AbilityBannerMs = 1600;

        /// <summary>
        /// Names what an enemy uses, FFX-style (playtest 3 finding 12): the name alone, no flavour -
        /// combat text stays minimal.
        /// </summary>
        private void OnEnemyAbilityUsed(ICombatUnit enemy, string name)
        {
            if (_abilityBanner == null || string.IsNullOrEmpty(name))
            {
                return;
            }
            if (_abilityBannerName != null)
            {
                _abilityBannerName.text = name;
            }
            SetShown(_abilityBanner, true);
            int shown = ++_abilityBannerShown;
            _abilityBanner.schedule.Execute(() =>
            {
                if (shown == _abilityBannerShown)
                {
                    SetShown(_abilityBanner, false);
                }
            }).StartingIn(AbilityBannerMs);
        }

        private void OnHeroItem()
        {
            SetShown(_heroBar, false);

            if (!InventoryManager.HasInstance)
            {
                SetShown(_heroBar, true);
                return;
            }

            var consumables = InventoryManager.Instance.GetConsumables();
            if (consumables.Count == 0)
            {
                SetShown(_heroBar, true);
                return;
            }

            CombatManager.Instance.RequestItemList(_currentHeroTurn, consumables);
        }

        /// <summary>
        /// Opens the enemy knowledge page. The only hero command that costs nothing: it submits no
        /// action, so <c>MagicSelectionUI</c> hands the turn back through
        /// <see cref="ReturnToHeroActions"/> when the page closes. Reading what the party already
        /// learned is not a move.
        /// </summary>
        private void OnHeroInspect()
        {
            SetShown(_heroBar, false);

            var enemies = CombatManager.Instance.GetAliveEnemies();
            if (enemies.Count == 0)
            {
                SetShown(_heroBar, true);
                return;
            }

            CombatManager.Instance.RequestInspectTargets(_currentHeroTurn, enemies);
        }

        /// <summary>Called by the selection UI to return to the Attack/Magic/Item/Inspect/Skip window.</summary>
        public void ReturnToHeroActions()
        {
            if (EnsureRefs())
            {
                SetShown(_heroBar, true);
                FocusRoot(); // reclaim keyboard focus from the (now closed) selection picker
            }
        }

        private void OnHeroSkip()
        {
            SetShown(_heroBar, false);
            CombatManager.Instance.SubmitHeroAction(HeroAction.Skip);
        }

        // ============================================================
        //  COMMAND MENU (FFX-style cursor selection list)
        // ============================================================

        private void BuildCommandMenu(ICombatUnit hero)
        {
            _commands.Clear();
            _commandRows.Clear();
            _commandCursors.Clear();
            _commandList?.Clear();
            if (_commandList == null)
            {
                return;
            }

            if (hero is SummonUnit summonUnit)
            {
                AddSummonCommands(summonUnit);
            }
            else
            {
                AddHeroCommands(hero);
            }

            for (int i = 0; i < _commands.Count; i++)
            {
                var entry = _commands[i];
                var row = new VisualElement();
                row.AddToClassList("cd-cmd-row");
                if (!entry.Enabled)
                {
                    row.AddToClassList("cd-cmd-row--disabled");
                }

                var cursor = new Label(string.Empty);
                cursor.AddToClassList("cd-cmd-row__cursor");
                var label = new Label(entry.Label);
                label.AddToClassList("cd-cmd-row__label");
                row.Add(cursor);
                row.Add(label);
                if (!entry.Enabled && !string.IsNullOrEmpty(entry.Reason))
                {
                    var reason = new Label(entry.Reason);
                    reason.AddToClassList("cd-cmd-row__reason");
                    row.Add(reason);
                }

                int idx = i;
                row.RegisterCallback<ClickEvent>(_ => OnCommandClicked(idx));
                row.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (_commands[idx].Enabled)
                    {
                        SetSelectedCommand(idx);
                    }
                });

                _commandList.Add(row);
                _commandRows.Add(row);
                _commandCursors.Add(cursor);
            }

            _selectedCommand = FirstEnabledCommand();
            RenderCommandCursor();
        }

        /// <summary>
        /// A party-replacing summon's own menu (§4b): Attack, its actions, its Signature once per
        /// summoning, Dismiss, and the free Inspect. No Item - the belt is the party's - and no Skip,
        /// because Dismiss covers what Skip was for (a Sacrifice horror gets Skip instead of Dismiss).
        /// Silence closes its abilities and nothing else, the same rule a hero lives under.
        /// </summary>
        private void AddSummonCommands(SummonUnit summon)
        {
            bool silenced = CombatManager.Instance.BuffTracker != null
                && CombatManager.Instance.BuffTracker.HasStatusEffect(summon, BuffType.Silenced);

            // A summon with its own Attack (Exatrix's Rend) still offers it as "Attack": it is the
            // basic attack, so Silence leaves it open like a hero's.
            if (summon.AttackAbility != null)
            {
                _commands.Add(new CommandEntry
                {
                    // A rotating summon's row is this turn's rite, by name - "Attack" would be a lie.
                    Command = HeroCommand.SummonAbility,
                    Label = summon.Summon != null && summon.Summon.RotateActions ? summon.AttackAbility.DisplayName : Loc.Get("room.command.attack"),
                    Enabled = true,
                    Ability = summon.AttackAbility
                });
            }
            else
            {
                _commands.Add(new CommandEntry { Command = HeroCommand.Attack, Label = Loc.Get("room.command.attack"), Enabled = true });
            }
            if (summon.Summon != null && summon.Summon.Actions != null)
            {
                foreach (var action in summon.Summon.Actions)
                {
                    if (action == null)
                    {
                        continue;
                    }
                    _commands.Add(new CommandEntry
                    {
                        Command = HeroCommand.SummonAbility, Label = action.DisplayName,
                        Enabled = !silenced, Ability = action
                    });
                }
            }
            if (summon.Summon != null && summon.Summon.Signature != null)
            {
                // The star says "once": after it is spent the row stays, greyed, so the player can
                // see what they have already used rather than wondering where it went.
                _commands.Add(new CommandEntry
                {
                    Command = HeroCommand.SummonAbility, Label = Loc.Format("room.command.signature", ("ability", summon.Summon.Signature.DisplayName)),
                    Enabled = !silenced && summon.Stay.CanUseSignature, Ability = summon.Summon.Signature
                });
            }
            _commands.Add(new CommandEntry { Command = HeroCommand.Inspect, Label = Loc.Get("room.command.inspect"), Enabled = true });
            // A stand-in (a Sacrifice horror) is what is left of a hero, not a guest: there is nothing to
            // send it home to, so it gets Skip where a summon gets Dismiss.
            if (!CombatManager.Instance.CanDismiss(summon))
            {
                _commands.Add(new CommandEntry { Command = HeroCommand.Skip, Label = Loc.Get("room.command.skip"), Enabled = true });
            }
            else
            {
                _commands.Add(new CommandEntry { Command = HeroCommand.Dismiss, Label = Loc.Get("room.command.dismiss"), Enabled = true });
            }
        }

        private void AddHeroCommands(ICombatUnit hero)
        {
            bool hasMagic = false;
            bool carriesAny = false;
            var heroComponent = hero as Heroes.Hero;
            if (heroComponent != null && DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                hasMagic = DungeonManager.Instance.MagicState.HasAnyCastable(heroComponent.HeroKey);
                foreach (var slot in DungeonManager.Instance.MagicState.GetSlots(heroComponent.HeroKey))
                {
                    if (slot != null && slot.Magic != null)
                    {
                        carriesAny = true;
                    }
                }
            }

            // Silence gates casting and nothing else - Attack, Item and Inspect stay open, so a
            // silenced hero still has a turn worth taking rather than three turns of Skip.
            bool silenced = CombatManager.Instance.BuffTracker != null
                && CombatManager.Instance.BuffTracker.HasStatusEffect(hero, BuffType.Silenced);
            if (silenced)
            {
                hasMagic = false;
            }

            // An Ultra's form brings abilities of its own, so a hero with no charges left still has some.
            if (!silenced && CombatManager.Instance.FormAbilities(hero).Count > 0)
            {
                hasMagic = true;
            }

            bool hasItem = InventoryManager.HasInstance && InventoryManager.Instance.HasAnyConsumable();

            // Summon is the one command the menu grew by (§4b, 2026-09-28): shown only to a hero
            // who knows a summon, greyed when every charge is spent - and, like casting, by Silence.
            var summons = DungeonManager.HasInstance ? DungeonManager.Instance.Summons : null;
            bool knowsSummon = heroComponent != null && summons != null && summons.Knows(heroComponent.HeroKey);
            bool canSummon = knowsSummon && !silenced && summons.HasAnyUsable(heroComponent.HeroKey);

            _commands.Add(new CommandEntry { Command = HeroCommand.Attack, Label = Loc.Get("room.command.attack"), Enabled = true });
            // A greyed command says why on its own row - "Ability" alone, dimmed, read as a bug.
            _commands.Add(new CommandEntry
            {
                Command = HeroCommand.Magic, Label = Loc.Get("room.command.ability"), Enabled = hasMagic,
                Reason = silenced ? Loc.Get("room.command.reason.silenced") : carriesAny ? Loc.Get("room.command.reason.no-charges") : Loc.Get("room.command.reason.none-carried")
            });
            if (knowsSummon)
            {
                _commands.Add(new CommandEntry
                {
                    Command = HeroCommand.Summon, Label = Loc.Get("room.command.summon"), Enabled = canSummon,
                    Reason = silenced ? Loc.Get("room.command.reason.silenced") : Loc.Get("room.command.reason.spent")
                });
            }
            // Ultra (COMBAT_DEPTH §13): shown to a hero who knows one, open once the gauge is full.
            if (CombatManager.Instance.KnownUltras(hero).Count > 0)
            {
                bool transformed = CombatManager.Instance.IsTransformed(hero);
                _commands.Add(new CommandEntry
                {
                    Command = HeroCommand.Ultra, Label = Loc.Get("room.command.ultra"), Enabled = CombatManager.Instance.CanUseUltra(hero),
                    Reason = transformed ? Loc.Get("room.command.reason.active") : UltraWaitReason(hero)
                });
            }
            _commands.Add(new CommandEntry { Command = HeroCommand.Item, Label = Loc.Get("room.command.item"), Enabled = hasItem, Reason = Loc.Get("room.command.reason.none") });
            // Inspect is free - it opens a page and hands the turn straight back - so it sits above
            // Skip rather than among the actions that spend the turn.
            _commands.Add(new CommandEntry { Command = HeroCommand.Inspect, Label = Loc.Get("room.command.inspect"), Enabled = true });
            _commands.Add(new CommandEntry { Command = HeroCommand.Skip, Label = Loc.Get("room.command.skip"), Enabled = true });
        }

        /// <summary>What a closed Ultra command waits for: the gauge's fill, or Focus for a hero whose only
        /// Ultras are MultiCasts (the Mage).</summary>
        private static string UltraWaitReason(ICombatUnit hero)
        {
            var known = CombatManager.Instance.KnownUltras(hero);
            if (known.Count > 0 && known.TrueForAll(Cards.UltraOps.IsMultiCast))
            {
                int focus = CombatManager.Instance.FocusOf(hero);
                return focus > 0 ? Loc.Format("room.command.reason.focus", ("focus", focus)) : Loc.Get("room.command.reason.no-focus");
            }
            return Loc.Format("room.command.reason.gauge", ("percent", CombatManager.Instance.UltraGauge(hero)));
        }

        private int FirstEnabledCommand()
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                if (_commands[i].Enabled)
                {
                    return i;
                }
            }
            return 0;
        }

        private void RenderCommandCursor()
        {
            for (int i = 0; i < _commandRows.Count; i++)
            {
                bool selected = i == _selectedCommand;
                _commandRows[i].EnableInClassList("cd-cmd-row--selected", selected);
                _commandCursors[i].text = selected ? "▸" : string.Empty;
            }
        }

        private void SetSelectedCommand(int index)
        {
            if (index < 0 || index >= _commands.Count)
            {
                return;
            }
            _selectedCommand = index;
            RenderCommandCursor();
        }

        private void MoveCommandCursor(int delta)
        {
            if (_commands.Count == 0)
            {
                return;
            }
            int i = _selectedCommand;
            for (int step = 0; step < _commands.Count; step++)
            {
                i = (i + delta + _commands.Count) % _commands.Count;
                if (_commands[i].Enabled)
                {
                    SetSelectedCommand(i);
                    CombatAudio.Play(CombatSound.CursorMove);
                    return;
                }
            }
        }

        /// <summary>Rows per column of the command menu, as <c>.cd-command-list</c>'s height lays it out.</summary>
        private const int CommandRowsPerColumn = 4;

        /// <summary>
        /// The other column: the same row, or the last row a shorter column has. A greyed row there
        /// leaves the cursor where it is, as the up/down cursor never lands on one either.
        /// </summary>
        private void MoveCommandColumn(int direction)
        {
            int target = _selectedCommand + direction * CommandRowsPerColumn;
            if (direction > 0)
            {
                target = Mathf.Min(target, _commands.Count - 1);
            }
            bool sameColumn = target / CommandRowsPerColumn == _selectedCommand / CommandRowsPerColumn;
            if (target < 0 || sameColumn || !_commands[target].Enabled)
            {
                return;
            }
            SetSelectedCommand(target);
            CombatAudio.Play(CombatSound.CursorMove);
        }

        private void ConfirmCommand()
        {
            if (_selectedCommand < 0 || _selectedCommand >= _commands.Count || !_commands[_selectedCommand].Enabled)
            {
                return;
            }
            CombatAudio.Play(CombatSound.Confirm);
            InvokeCommand(_commands[_selectedCommand]);
        }

        private void OnCommandClicked(int index)
        {
            if (index < 0 || index >= _commands.Count || !_commands[index].Enabled)
            {
                return;
            }
            SetSelectedCommand(index);
            ConfirmCommand();
        }

        /// <summary>Runs one row of the command menu. Callers check it is enabled first.</summary>
        private void InvokeCommand(CommandEntry entry)
        {
            switch (entry.Command)
            {
                case HeroCommand.SummonAbility:
                    OnSummonAbility(entry.Ability);
                    break;
                case HeroCommand.Dismiss:
                    OnSummonDismiss();
                    break;
                case HeroCommand.Attack:
                    OnHeroAttack();
                    break;
                case HeroCommand.Magic:
                    OnHeroMagic();
                    break;
                case HeroCommand.Summon:
                    OnHeroSummon();
                    break;
                case HeroCommand.Ultra:
                    OnHeroUltra();
                    break;
                case HeroCommand.Item:
                    OnHeroItem();
                    break;
                case HeroCommand.Inspect:
                    OnHeroInspect();
                    break;
                case HeroCommand.Skip:
                    OnHeroSkip();
                    break;
            }
        }

        // ============================================================
        //  TURN-ORDER LIST (FFX-style, right side)
        // ============================================================

        /// <summary>How many upcoming turns the portrait column shows.</summary>
        private const int TurnOrderShown = 10;

        private void OnTurnOrderChanged(List<ICombatUnit> order)
        {
            if (_turnOrderList == null)
            {
                return;
            }
            _turnOrderList.Clear();
            if (order == null)
            {
                return;
            }

            // Portraits only, FFX-style (2026-09-30): the names were cut off ("Abyssal Warde") and made
            // the panel wide enough to sit on a boss. Side is the frame colour, the actor is the big one.
            int shown = 0;
            for (int i = 0; i < order.Count && shown < TurnOrderShown; i++)
            {
                var unit = order[i];
                if (unit == null)
                {
                    continue;
                }

                var row = new VisualElement();
                row.AddToClassList("cd-turn-row");
                if (shown == 0)
                {
                    row.AddToClassList("cd-turn-row--current");
                    // "Now" on the acting tile, so the column reads as a turn order at a glance (playtest
                    // 2026-10-30: the tester never noticed it).
                    var now = new Label(Loc.Get("room.turn-order.now")) { pickingMode = PickingMode.Ignore };
                    now.AddToClassList("cd-turn-row__now");
                    row.Add(now);
                }
                row.EnableInClassList("cd-turn-row--enemy", unit is Enemies.Enemy);
                row.tooltip = unit.DisplayName;

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("cd-turn-row__icon");
                if (unit.Icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(unit.Icon);
                }

                row.Add(icon);
                _turnOrderList.Add(row);
                shown++;
            }
        }

        /// <summary>
        /// The whole keyboard for this panel, dispatched by which bar is up: Fight/Flee on the combat
        /// bar, the hero command cursor and its letter shortcuts on the command bar, and otherwise the
        /// dungeon keys - arrows to walk, Enter to use. A key only ever acts when its bar is
        /// visible, and only invokes actions whose buttons are currently shown (Magic and Item
        /// appear conditionally), so a hotkey can never do something the on-screen menu cannot.
        /// </summary>
        private void OnCombatHotkey(KeyDownEvent evt)
        {
            // The map is checked before pause because it can be opened *from* pause, and the two are
            // never up together - opening the map closes the overlay and Back puts it back.
            if (IsShown(_mapWindow))
            {
                HandleMapKey(evt);
                evt.StopPropagation();
                return;
            }

            if (PotionsOpen)
            {
                _potions.HandleKey(evt);
                evt.StopPropagation();
                return;
            }

            // Pause is above everything else, including a dialog it can never be opened over: while
            // it is up it is the only thing the keyboard reaches.
            if (IsShown(_pauseWindow))
            {
                if (evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.Backspace || evt.keyCode == KeyCode.X || evt.keyCode == KeyCode.C)
                {
                    ClosePause();
                }
                else
                {
                    _pauseNav?.HandleKey(evt);
                }
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.M && CanOpenMap())
            {
                OpenMap(fromPause: false);
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.P && CanOpenPotions())
            {
                OpenPotions(fromPause: false);
                evt.StopPropagation();
                return;
            }

            // C is the RPG Maker menu key (playtest 2026-10-30), beside Escape.
            if ((evt.keyCode == KeyCode.Escape || evt.keyCode == KeyCode.C) && CanOpenPause())
            {
                OpenPause();
                evt.StopPropagation();
                return;
            }

            // Dialogs first: whatever is stacked over the room owns the keyboard while it is up.
            if (HandleDialogKey(evt))
            {
                evt.StopPropagation();
                return;
            }
            if (IsDialogUp())
            {
                // A dialog is up but did not want this key - it must not fall through to the room.
                return;
            }

            if (IsShown(_combatBar))
            {
                // Stepping back through the door the party came in by is Flee; nothing walks further in.
                if (IsShown(_fleeBtn) && _entryDoor != null
                    && PartyWalk.DirectionOf(evt.keyCode) == _entryDoor.DirectionFrom(_currentRoom))
                {
                    evt.StopPropagation();
                    OnFlee();
                    return;
                }

                // Arrows/Tab move along the bar, Enter presses. Flee is hidden in a boss room, so the
                // cursor cannot reach a way out the fight does not offer.
                if (_combatNav != null && _combatNav.HandleKey(evt))
                {
                    evt.StopPropagation();
                }
                return;
            }

            if (IsShown(_heroBar))
            {
                switch (evt.keyCode)
                {
                    // Cursor navigation (controller-friendly).
                    case KeyCode.UpArrow:
                        MoveCommandCursor(-1);
                        evt.StopPropagation();
                        break;
                    case KeyCode.DownArrow:
                        MoveCommandCursor(1);
                        evt.StopPropagation();
                        break;
                    case KeyCode.LeftArrow:
                        MoveCommandColumn(-1);
                        evt.StopPropagation();
                        break;
                    case KeyCode.RightArrow:
                        MoveCommandColumn(1);
                        evt.StopPropagation();
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                    case KeyCode.Z:
                    case KeyCode.Space:
                        ConfirmCommand();
                        evt.StopPropagation();
                        break;
                }
                return;
            }

            if (DoorNavActive() && HandleDungeonKey(evt))
            {
                evt.StopPropagation();
            }
        }
    }
}
