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
    /// <summary>The room's screen, by job: an object in the room, and the room events behind them.</summary>
    public partial class RoomActionUI
    {
        // ============================================================
        //  EXAMINE / ACTION FLOWS
        // ============================================================

        /// <summary>
        /// Opens the room's event, from walking up to its object, once the room is clear. There is no
        /// button for it: the object in the room is the way in.
        /// </summary>
        private bool TryUseEvent()
        {
            if (_currentRoom.Enemies.Any(e => e != null && e.IsAlive))
            {
                return false;
            }

            var roomEvent = PendingEvent();
            if (roomEvent == null)
            {
                // What the event opened: its object is the way in now.
                var spec = DungeonManager.Instance.ResolvedOutcomeOf(_currentRoom)?.SideRoom;
                if (spec == null || !spec.IsSet || spec.Mandatory)
                {
                    return false;
                }
                EnterSideRoom(_currentRoom, spec);
                return true;
            }

            ShowRoomEvent(roomEvent);
            return true;
        }

        /// <summary>Takes the party to <paramref name="host"/>'s side room, remembering where it stood.</summary>
        private void EnterSideRoom(Room host, Events.SideRoomSpec spec, List<ItemSO> pendingLoot = null)
        {
            var party = GameManager.Instance.Party;
            var side = DungeonManager.Instance.SideRoomFor(host, spec, pendingLoot);
            _sideReturnTile = party.Tile;
            party.PlaceOnTile(side, side.SideEntryTile);
            side.Reveal();
            GameManager.Instance.SnapCamera();
            Show(side);
        }

        private void LeaveSideRoom()
        {
            var host = _currentRoom.HostRoom;
            var party = GameManager.Instance.Party;
            party.PlaceOnTile(host, _sideReturnTile);
            GameManager.Instance.SnapCamera();
            Show(host);
        }

        /// <summary>
        /// Pays the item an event held back until its fight was won, as one statement once the room is
        /// clear. Winning a mandatory side room is what resolves its event, and takes the party back.
        /// </summary>
        private bool TryGrantEventLoot()
        {
            if (_currentRoom == null || _currentRoom.PendingEventLoot == null
                || _currentRoom.Enemies.Any(e => e != null && e.IsAlive))
            {
                return false;
            }

            var item = Events.RoomEventRunner.GrantOneOf(_currentRoom.PendingEventLoot);
            _currentRoom.PendingEventLoot = null;
            var owner = _currentRoom.IsSideRoom ? _currentRoom.HostRoom : _currentRoom;
            owner.EventLootClaimed = true;
            bool arena = _currentRoom.IsSideRoom && _currentRoom.SideSpec.Mandatory;
            if (arena && _arenaOption >= 0)
            {
                owner.MarkEventResolved(_arenaOption, _arenaOutcome, _arenaSucceeded);
                _arenaOption = -1;
            }
            if (DungeonSaveManager.HasInstance)
            {
                DungeonSaveManager.Instance.Save(_currentRoom);
            }

            ShowDetail(owner.RoomEvent != null ? owner.RoomEvent.Title : string.Empty,
                item != null ? Loc.Format("event-text.report.found", ("item", item.DisplayName)) : string.Empty);
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                if (arena)
                {
                    LeaveSideRoom();
                }
                else
                {
                    SettleRoom();
                }
            };
            return true;
        }


        /// <summary>
        /// Who is in the room, as the Fight / Flee bar's heading (<see cref="FoeLine"/>): met enemies
        /// by name, grouped by kind, and the unmet folded into one count - no stats, so the decision
        /// is informed without the bar becoming the Inspect page.
        /// </summary>
        private static string DescribeFoes(Room room)
        {
            var foes = new List<FoeEntry>();
            // The boss leads the line and is always named: its banner already says who it is, so
            // "Floating Eye + 1 unknown" under "Abyssal Warden" hid nothing and read as a mistake
            // (playtest 2 finding 3).
            foreach (var enemy in room.Enemies.Where(e => e != null).OrderBy(e => e.IsBoss ? 0 : 1))
            {
                if (!enemy.IsAlive)
                {
                    continue;
                }
                bool seen = enemy.IsBoss || (enemy.Definition != null && MetaProgressManager.HasInstance
                    && MetaProgressManager.Instance.IsEnemySeen(enemy.Definition.SaveKey));
                string kind = enemy.Definition != null ? enemy.Definition.SaveKey : enemy.DisplayName;
                foes.Add(new FoeEntry(kind, enemy.DisplayName, seen));
            }
            return FoeLine.Describe(foes);
        }

        // ============================================================
        //  ROOM EVENTS
        // ============================================================

        /// <summary>The room's unresolved event, or null when there is nothing left to do here.</summary>
        private Events.RoomEventSO PendingEvent()
        {
            return _currentRoom != null && _currentRoom.HasPendingEvent ? _currentRoom.RoomEvent : null;
        }

        /// <summary>
        /// Opens the event: the fiction, how well the party can read the risk, and the choices. The
        /// check resolves against the party's <i>best</i> hero at the governing stat, and that hero
        /// is named - the point of a specialist is being able to see them earn their slot.
        /// </summary>
        private void ShowRoomEvent(Events.RoomEventSO roomEvent)
        {
            var party = GameManager.Instance.Party;
            var units = new List<ICombatUnit>();
            if (party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null)
                    {
                        units.Add(hero);
                    }
                }
            }

            _currentEvent = roomEvent;
            _eventActingHero = Events.RoomEventResolver.BestFor(units, roomEvent.GoverningStat) as Heroes.Hero;

            int statValue = _eventActingHero != null
                ? _eventActingHero.GetEffectiveStat(roomEvent.GoverningStat)
                : 0;
            _eventChance = Events.RoomEventResolver.SuccessChance(statValue, roomEvent.Difficulty);

            var band = Events.RoomEventResolver.BandFor(_eventChance);
            var clarity = Events.RoomEventResolver.ClarityFor(statValue, roomEvent.Difficulty);

            _eventTitle.text = roomEvent.Title;
            _eventPrompt.text = roomEvent.Prompt;
            // Only events that actually gamble get an odds line, and it says "anything you chance"
            // rather than "this": an event can mix a sure thing with a gamble (take the loose coin,
            // or open the chest), and a bare "this looks dangerous" over the whole window would be
            // claiming the safe option is risky.
            bool hasCheck = false;
            foreach (var option in roomEvent.Options)
            {
                if (option != null && option.Kind == Events.RoomEventOptionKind.StatCheck)
                {
                    hasCheck = true;
                    break;
                }
            }
            SetShown(_eventOdds, hasCheck);
            _eventOdds.text = hasCheck ? BuildOddsLine(roomEvent, band, clarity) : string.Empty;

            _eventOptions.Clear();
            _eventDecline = null;
            for (int i = 0; i < roomEvent.Options.Count; i++)
            {
                var option = roomEvent.Options[i];
                if (option == null || string.IsNullOrEmpty(option.Label))
                {
                    continue;
                }

                int captured = i;
                var btn = new Button(() => OnEventOptionChosen(captured));
                btn.AddToClassList("cd-list-button");
                btn.AddToClassList("cd-event-option");
                btn.focusable = false;
                // The label and its tag share a row, so a long choice wraps instead of running
                // under the tag (playtest 2 finding 5).
                var text = new Label(option.Label) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("cd-event-option__text");
                btn.Add(text);
                // Which choices are gambles, on the row itself rather than in prose above the list.
                var block = BlockOf(option);
                string tagText = OptionTag(option, roomEvent, band, clarity, out string tone);
                if (block != Events.OptionBlock.None)
                {
                    btn.SetEnabled(false);
                    tagText = BlockedTag(option, block);
                    tone = "bad";
                }
                var tag = new Label(tagText) { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("cd-event-tag");
                tag.AddToClassList("cd-event-tag--" + tone);
                btn.Add(tag);
                _eventOptions.Add(btn);
                if (option.Kind == Events.RoomEventOptionKind.Decline && _eventDecline == null)
                {
                    _eventDecline = btn;
                }
            }

            // An event's own Decline already walks away, in the event's words - a Back beside it is
            // the same exit twice. Back stays only for an event authored without one, so no event
            // window can trap the player.
            SetShown(_eventBack, _eventDecline == null);

            _eventNav?.Reset();
            SetShown(_eventWindow, true);
        }

        private static Events.OptionBlock BlockOf(Events.RoomEventOption option)
        {
            var inventory = InventoryManager.HasInstance ? InventoryManager.Instance : null;
            return Events.RoomEventResolver.BlockOf(option,
                inventory != null && inventory.Holds(option.RequiredItem),
                inventory != null && inventory.HasAnyConsumable(),
                MetaProgressManager.HasInstance ? MetaProgressManager.Instance.PendingRunGold : 0);
        }

        /// <summary>What a greyed-out choice is waiting for, in its right-hand tag.</summary>
        private static string BlockedTag(Events.RoomEventOption option, Events.OptionBlock block)
        {
            switch (block)
            {
                case Events.OptionBlock.NeedsItem:
                    return Loc.Format("room.event.tag.needs-item", ("item", option.RequiredItem.DisplayName));
                case Events.OptionBlock.NeedsConsumable:
                    return Loc.Get("room.event.tag.needs-consumable");
                default:
                    return Loc.Format("room.event.tag.needs-gold", ("amount", option.GoldCost));
            }
        }

        /// <summary>
        /// A choice's right-hand tag: what a sure thing really holds ("safe", "price", "danger") or
        /// walking away, otherwise the stat it
        /// turns on and the odds as far as the party can read them ("LCK · even", "INT · risky",
        /// "WIS · ?"). <paramref name="tone"/> is the colour class suffix.
        /// </summary>
        private static string OptionTag(Events.RoomEventOption option, Events.RoomEventSO roomEvent,
            Events.OddsBand band, Events.OddsClarity clarity, out string tone)
        {
            if (option.Kind != Events.RoomEventOptionKind.StatCheck)
            {
                if (option.Kind == Events.RoomEventOptionKind.Decline)
                {
                    tone = "safe";
                    return Loc.Get("room.event.tag.leave");
                }
                switch (Events.RoomEventResolver.RiskOf(option))
                {
                    case Events.OptionRisk.Danger:
                        tone = "bad";
                        return Loc.Get("room.event.tag.danger");
                    case Events.OptionRisk.Price:
                        tone = "even";
                        return Loc.Get("room.event.tag.price");
                    default:
                        tone = "safe";
                        return Loc.Get("room.event.tag.safe");
                }
            }
            string stat = StatCatalog.ShortName(roomEvent.GoverningStat);
            if (clarity == Events.OddsClarity.Unknown)
            {
                tone = "unknown";
                return Loc.Format("room.event.tag.unknown", ("stat", stat));
            }
            bool good = band >= Events.OddsBand.VeryLikely;
            bool even = band == Events.OddsBand.EvenOdds;
            tone = good ? "good" : even ? "even" : "bad";
            if (clarity == Events.OddsClarity.Vague)
            {
                return Loc.Format(good ? "room.event.tag.vague.good" : even ? "room.event.tag.vague.even" : "room.event.tag.vague.bad", ("stat", stat));
            }
            switch (band)
            {
                case Events.OddsBand.AlmostCertain:
                    return Loc.Format("room.event.tag.certain", ("stat", stat));
                case Events.OddsBand.VeryLikely:
                    return Loc.Format("room.event.tag.likely", ("stat", stat));
                case Events.OddsBand.EvenOdds:
                    return Loc.Format("room.event.tag.even", ("stat", stat));
                case Events.OddsBand.SlightChance:
                    return Loc.Format("room.event.tag.slim", ("stat", stat));
                default:
                    return Loc.Format("room.event.tag.hopeless", ("stat", stat));
            }
        }

        private string BuildOddsLine(
            Events.RoomEventSO roomEvent,
            Events.OddsBand band,
            Events.OddsClarity clarity)
        {
            string reading = Events.RoomEventResolver.DescribeOdds(band, clarity);
            string statName = StatCatalog.DisplayName(roomEvent.GoverningStat);

            if (_eventActingHero == null)
            {
                return Loc.Format("room.event.odds", ("stat", statName), ("reading", reading));
            }

            return Loc.Format("room.event.odds-hero", ("stat", statName), ("hero", _eventActingHero.DisplayName), ("reading", reading));
        }

        /// <summary>
        /// Resolves the chosen option and applies whatever came of it. The resolution is written to
        /// the room <b>and</b> saved at once: without that the player re-rolls a bad outcome by
        /// walking out and back in, or by quitting to the menu and resuming.
        /// </summary>
        private void OnEventOptionChosen(int optionIndex)
        {
            if (_currentEvent == null || optionIndex < 0 || optionIndex >= _currentEvent.Options.Count)
            {
                return;
            }

            var option = _currentEvent.Options[optionIndex];
            if (BlockOf(option) != Events.OptionBlock.None)
            {
                return;
            }

            SetShown(_eventWindow, false);
            _eventDecline = null;

            if (option.Kind == Events.RoomEventOptionKind.Decline)
            {
                // Walking away leaves the event unconsumed - the choice is deferred, not spent.
                string declineText = string.IsNullOrEmpty(option.DeclineText)
                    ? Loc.Get("room.event.decline.default")
                    : option.DeclineText;
                // Say so, or the object still standing there reads as a bug rather than an offer
                // still open (revisit playtest finding 11).
                ShowDetail(_currentEvent.Title, Loc.Format("room.event.decline.message", ("text", declineText)));
                _detailOkAction = CloseEventResult;
                return;
            }

            bool succeeded = option.Kind == Events.RoomEventOptionKind.Guaranteed
                || Events.RoomEventResolver.Passes(_eventChance, UnityEngine.Random.Range(0f, 1f));

            var pool = succeeded ? option.Success : option.Failure;

            // The acting hero bends the pool - it is their hand in the chest. Party-best would mean
            // the Scout's charm helping while the Warrior forces a door.
            int outcomeIndex = Events.RoomEventResolver.PickOutcomeIndex(
                pool, UnityEngine.Random.Range(0f, 1f), _eventActingHero);
            var outcome = outcomeIndex >= 0 ? pool[outcomeIndex] : null;

            var paid = _eventRunner.PayFor(option);
            var report = _eventRunner.Apply(
                outcome,
                succeeded,
                _currentRoom,
                GameManager.Instance.Party,
                _eventActingHero,
                DungeonManager.HasInstance ? DungeonManager.Instance.Afflictions : null);
            report.Lines.InsertRange(0, paid);

            // The party window reports what the outcome did at once, not after the dialog is dismissed.
            RefreshPartyStatus();

            bool arena = outcome != null && outcome.SideRoom != null && outcome.SideRoom.IsSet && outcome.SideRoom.Mandatory;
            if (arena)
            {
                _arenaOption = optionIndex;
                _arenaOutcome = outcomeIndex;
                _arenaSucceeded = succeeded;
            }
            else
            {
                _currentRoom.MarkEventResolved(optionIndex, outcomeIndex, succeeded);
                if (DungeonSaveManager.HasInstance)
                {
                    DungeonSaveManager.Instance.Save(_currentRoom);
                }
            }

            // A room the outcome opened is walked into at once; afterwards the object is the way back in.
            bool opensRoom = outcome != null && outcome.SideRoom != null && outcome.SideRoom.IsSet;
            ShowEventResult(report, option.Kind == Events.RoomEventOptionKind.StatCheck ? (bool?)succeeded : null,
                opensRoom ? outcome : null);
        }

        private void ShowEventResult(Events.RoomEventOutcomeReport report, bool? checkPassed = null, Events.RoomEventOutcome arena = null)
        {
            string message = string.IsNullOrEmpty(report.Text) ? Loc.Get("room.event.result.nothing") : report.Text;
            if (report.Lines.Count > 0)
            {
                message += "\n\n" + string.Join("\n", report.Lines);
            }

            ShowDetail(_currentEvent.Title, message);
            // Whether the gamble came off, before the flavour text: a result read as fiction alone
            // left the player to infer it from the consequences.
            if (checkPassed.HasValue)
            {
                SetDetailChip(checkPassed.Value ? Loc.Get("room.event.result.success") : Loc.Get("room.event.result.failed"), checkPassed.Value);
            }

            // A woken room is a different room: re-show it so the Fight/Flee bar replaces the
            // Examine/Action one, rather than leaving the player among live enemies with
            // non-combat buttons.
            bool woken = report.SpawnedEnemies;
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                _currentEvent = null;
                _eventActingHero = null;

                if (arena != null)
                {
                    EnterSideRoom(_currentRoom, arena.SideRoom, arena.SideRoom.Mandatory ? arena.LootOneOf : null);
                }
                else if (woken)
                {
                    Show(_currentRoom, _entryDoor);
                }
                else
                {
                    SettleRoom();
                }
            };
        }

        /// <summary>
        /// A question: Ok runs <paramref name="onConfirm"/>, Cancel puts the player back in the room
        /// having changed nothing. Anything irreversible - freeing a captive, leaving a level behind -
        /// should ask this way rather than through <see cref="ShowDetail"/>, where the only button is
        /// consent.
        /// </summary>
        private void ShowConfirm(string title, string message, string confirmLabel, Action onConfirm)
        {
            ShowDetail(title, message);
            _detailOk.text = confirmLabel;
            _detailOkAction = () =>
            {
                _detailOk.text = Loc.Get("room.detail.ok");
                SetShown(_detailWindow, false);
                onConfirm();
            };
            _detailCancelAction = () =>
            {
                _detailOk.text = Loc.Get("room.detail.ok");
                SetShown(_detailWindow, false);
                SettleRoom();
            };
            SetShown(_detailCancel, true);
        }

        private void CloseEventResult()
        {
            SetShown(_detailWindow, false);
            _currentEvent = null;
            _eventActingHero = null;
            SettleRoom();
        }

        private void OnEventBack()
        {
            _eventNav?.Reset();
            SetShown(_eventWindow, false);
            _eventOptions.Clear();
            _eventDecline = null;
            _currentEvent = null;
            _eventActingHero = null;
            SettleRoom();
        }

        /// <summary>
        /// A statement: one button, and dismissing it returns to the room. Callers that need a second
        /// beat overwrite <c>_detailOkAction</c> after calling this.
        /// </summary>
        private void ShowDetail(string title, string message)
        {
            _detailNav?.Reset();
            SetShown(_detailCancel, false);
            _detailCancelAction = null;
            _detailTitle.text = title;
            _detailMessage.text = message;
            SetShown(_detailChip, false);
            _detailRows?.Clear();
            SetShown(_detailRows, false);
            // Default: dismissing returns to the room. Callers that need a second beat (the rescue
            // flow, an event outcome) overwrite _detailOkAction after calling this.
            _detailOkAction = () =>
            {
                SetShown(_detailWindow, false);
                SettleRoom();
            };
            SetShown(_detailWindow, true);
        }

        private void SetDetailChip(string text, bool good)
        {
            if (_detailChip == null)
            {
                return;
            }
            _detailChip.text = text;
            _detailChip.EnableInClassList("cd-detail-chip--good", good);
            _detailChip.EnableInClassList("cd-detail-chip--bad", !good);
            SetShown(_detailChip, true);
        }

        /// <summary>One reward row in a dialog, in the victory window's style: icon, rarity-coloured
        /// name, count on the right.</summary>
        private void AddDetailRow(Sprite icon, string name, int quantity, ItemSO rarityOf)
        {
            if (_detailRows == null)
            {
                return;
            }
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("cd-victory-loot");
            var tile = new VisualElement { pickingMode = PickingMode.Ignore };
            tile.AddToClassList("cd-victory-loot__icon");
            if (icon != null)
            {
                tile.style.backgroundImage = new StyleBackground(icon);
            }
            row.Add(tile);
            var label = new Label(quantity > 1 ? Loc.Format("room.reward.counted", ("name", name), ("count", quantity)) : name) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("cd-victory-loot__name");
            if (rarityOf != null)
            {
                label.AddToClassList(ItemPresenter.RarityClass(rarityOf.Rarity));
            }
            row.Add(label);
            _detailRows.Add(row);
            SetShown(_detailRows, true);
        }

        /// <summary>
        /// Takes the stairs: this is the one place a level is completed. Confirmed first, because it
        /// ends the level and abandons anything still unspent on it.
        /// </summary>
        private void OnDescend()
        {
            if (_currentRoom == null || !_currentRoom.IsExit)
            {
                return;
            }

            // Warn only about what is really left, and say what - "anything still unfound stays here"
            // on a floor with nothing left read as a trap.
            var dungeon = DungeonManager.HasInstance ? DungeonManager.Instance : null;
            var model = DungeonMapOps.Build(BuildMapInputs(dungeon != null ? dungeon.CurrentRooms : null),
                _currentRoom.RoomIndex);
            string left = model.FrontierCount > 0
                ? (model.FrontierCount == 1
                    ? Loc.Format("room.descend.unexplored.one", ("count", model.FrontierCount))
                    : Loc.Format("room.descend.unexplored.many", ("count", model.FrontierCount)))
                : string.Empty;
            // The last floor has nothing below it: there the stairs lead home (revisit playtest
            // finding 11).
            bool lastFloor = dungeon != null && DungeonManager.ActiveRun != null
                && DungeonManager.RunLevelIndex >= DungeonManager.ActiveRun.Levels.Count - 1;
            ShowConfirm(lastFloor ? Loc.Get("room.descend.title.last") : Loc.Get("room.descend.title"),
                (lastFloor
                    ? Loc.Get("room.descend.message.last")
                    : Loc.Get("room.descend.message")) + left,
                lastFloor ? Loc.Get("room.descend.confirm.last") : Loc.Get("room.descend.confirm"),
                () => CombatManager.Instance.NotifyDungeonCleared());
        }

        /// <summary>
        /// Frees the captive in a cleared room - automatically, because leaving them bound was never
        /// a real choice (playtest 2026-09-28): the only decision the old Rescue button posed was
        /// whether to press it. One statement says who joined, and the party window is rebuilt at
        /// once so the newcomer's row is there before the player walks on. Returns false when there
        /// was nobody to free, so the caller can move on.
        /// </summary>
        private bool TryAutoRescue()
        {
            var captive = _currentRoom != null ? _currentRoom.CaptiveHero : null;
            if (captive == null || _currentRoom.Enemies.Any(e => e != null && e.IsAlive)
                || !DungeonManager.HasInstance || !DungeonManager.Instance.TryRescueCaptive(_currentRoom))
            {
                return false;
            }

            // Rebuilt, not refreshed: RefreshPartyStatus only updates the rows that already exist,
            // which is why the newcomer used to be missing until the next room.
            ShowPartyStatusOutOfCombat();

            if (FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    GameManager.Instance.Party.transform.position,
                    Loc.Format("room.captive.joined-float", ("hero", captive.DisplayName)),
                    Color.cyan);
            }

            string blurb = string.IsNullOrEmpty(captive.Blurb) ? "" : captive.Blurb + "\n\n";
            ShowDetail(Loc.Format("room.captive.title", ("hero", captive.DisplayName)),
                Loc.Format("room.captive.message", ("hero", captive.DisplayName), ("blurb", blurb)));
            return true;
        }

        /// <summary>
        /// Empties a cache: gold, plus at most one depth-rolled item. Not confirmed, because nothing
        /// is spent and nothing is risked - the decision a cache poses is only whether to walk to it.
        /// </summary>
        private void OnSearch()
        {
            if (_currentRoom == null || !_currentRoom.HasPendingPayload
                || _currentRoom.Kind != RoomKind.Treasure)
            {
                return;
            }


            int gold = RoomKindRewards.TreasureGold(DungeonManager.RunLevelIndex);
            if (gold > 0)
            {
                // Pending, not banked: cache gold is forfeited on death exactly like a kill's, so
                // carrying it out of the level is the reward.
                MetaProgressManager.Instance.AddPendingGold(gold, EconomySource.Cache);
            }

            var item = RollTreasureItem();
            if (item != null && InventoryManager.HasInstance)
            {
                InventoryManager.Instance.AddItem(item, 1, EconomySource.Cache);
            }

            // A side room's chest can hold better than a cache's depth roll.
            var sideSpec = _currentRoom.SideSpec;
            if (sideSpec != null && UnityEngine.Random.Range(0f, 100f) < sideSpec.ChestLootChancePercent)
            {
                var rare = Events.RoomEventRunner.GrantOneOf(sideSpec.ChestLoot);
                item = rare != null ? rare : item;
            }

            // The floor's own raw stuff, rolled separately from the gear slot above - see
            // RoomKindRewards.TreasureMaterials.
            var materials = RollTreasureMaterials();
            foreach (var award in materials)
            {
                InventoryManager.Instance.AddItem(award, EconomySource.Cache);
            }

            TakePayload();

            if (FloatingTextHandler.HasInstance && GameManager.Instance.Party != null)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    GameManager.Instance.Party.transform.position, Loc.Format("room.cache.gold-float", ("amount", gold)), new Color(1f, 0.85f, 0.25f));
            }

            ShowDetail(Loc.Get("room.cache.title"), gold > 0
                ? Loc.Format("room.cache.message-gold", ("amount", gold))
                : Loc.Get("room.cache.message"));
            // What was found, as reward rows (the victory window's style) rather than sentences.
            if (item != null)
            {
                AddDetailRow(item.Icon, item.DisplayName, 1, item);
            }
            foreach (var award in materials)
            {
                AddDetailRow(award.Item.Icon, award.Item.DisplayName, award.Quantity, award.Item);
            }
            if (item == null && materials.Count == 0)
            {
                _detailMessage.text += Loc.Get("room.cache.nothing-else");
            }
            _detailOkAction = ClosePayloadResult;
        }

        /// <summary>
        /// One item from the whole catalog, rolled through the same rarity and depth rules a kill drop
        /// follows. Shuffled first, so *which* item varies; capped at one, so a cache is a find rather
        /// than a shop.
        /// </summary>
        private ItemSO RollTreasureItem()
        {
            if (!InventoryManager.HasInstance)
            {
                return null;
            }

            var candidates = InventoryManager.Instance.AllItems().Where(i => i != null).ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var swap = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = swap;
            }

            return RoomKindRewards.PickTreasureItem(
                candidates, DungeonManager.RunLevelIndex, () => UnityEngine.Random.Range(0f, 1f));
        }

        /// <summary>
        /// The raw materials this floor's caches yield, off <c>LevelDefinitionSO.MaterialTable</c>.
        /// Empty when the level authors no table, which is the same as it authoring no materials.
        /// </summary>
        private List<LootAward> RollTreasureMaterials()
        {
            var level = DungeonManager.HasInstance ? DungeonManager.Instance.CurrentLevel : null;
            if (level == null || !InventoryManager.HasInstance)
            {
                return new List<LootAward>();
            }

            var materials = RoomKindRewards.TreasureMaterials(
                level.MaterialTable, DungeonManager.RunLevelIndex, () => UnityEngine.Random.Range(0f, 1f));
            // A revisit below its best fear does not pay the scarce materials (docs/plans/REVISITS.md).
            // A recipe already known teaches nothing, so a cache does not hand it over again.
            materials.RemoveAll(a => a.Item != null
                && (RunFear.Current.Withholds(a.Item) || InventoryManager.Instance.AlreadyKnown(a.Item)));
            return materials;
        }

        /// <summary>
        /// Takes the refuge: heals every hero a share of their bar, once. Confirmed, because resting
        /// at full health throws the whole thing away - that timing is the decision the room poses,
        /// and the party's current state is in the prompt so the player can make it.
        /// </summary>
        private void OnRest()
        {
            if (_currentRoom == null || !_currentRoom.HasPendingPayload
                || _currentRoom.Kind != RoomKind.Rest)
            {
                return;
            }

            var party = GameManager.Instance.Party;
            int missing = 0;
            if (party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null && hero.Stats != null)
                    {
                        missing += Mathf.Max(0, hero.GetEffectiveMaxHealth() - hero.Stats.Health);
                    }
                }
            }


            string state = missing > 0
                ? Loc.Format("room.refuge.state.hurt", ("missing", missing))
                : Loc.Get("room.refuge.state.whole");

            ShowConfirm(Loc.Get("room.refuge.title"),
                Loc.Format("room.refuge.prompt", ("state", state),
                    ("percent", Mathf.RoundToInt(RoomKindRewards.RestHealFraction * 100f))),
                Loc.Get("room.refuge.confirm"),
                ApplyRest);
        }

        private void ApplyRest()
        {
            var party = GameManager.Instance.Party;
            var lines = new List<string>();

            if (party != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero == null || hero.Stats == null || !hero.IsAlive)
                    {
                        continue;
                    }

                    int max = hero.GetEffectiveMaxHealth();
                    int healed = HealthOps.Heal(hero,
                        RunFear.Current.ScaleHeroHealing(RoomKindRewards.RestHealAmount(max)),
                        new HealthSource(null, HealthCause.Rest), null).Healed;
                    lines.Add(healed > 0
                        ? Loc.Format("room.refuge.healed", ("hero", hero.DisplayName), ("amount", healed))
                        : Loc.Format("room.refuge.already-whole", ("hero", hero.DisplayName)));
                }
            }

            // A refuge is the only in-run refill of a spell charge. Draw used to be that refill -
            // spend a charge, take the magic again - and removing it (2026-09-04) left magic as a
            // strictly finite run allowance with no way back. Hanging the top-up on the refuge keeps
            // it a place the player has to find and spend rather than a rule about levels, and puts
            // it in direct competition with the heal: the same one-shot room pays both, so resting
            // early for charges is resting early for health too.
            if (DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                DungeonManager.Instance.MagicState.RefillCharges();
                lines.Add(Loc.Get("room.refuge.ability-charges"));
            }
            if (DungeonManager.HasInstance && DungeonManager.Instance.Summons != null
                && DungeonManager.Instance.Summons.GetSaveData().Count > 0)
            {
                DungeonManager.Instance.Summons.RefillCharges();
                lines.Add(Loc.Get("room.refuge.summon-charges"));
            }

            TakePayload();

            if (FloatingTextHandler.HasInstance && party != null)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    party.transform.position, Loc.Get("room.refuge.rested-float"), Color.green);
            }

            ShowDetail(Loc.Get("room.refuge.title"), Loc.Format("room.refuge.result", ("lines", string.Join("\n", lines))));
            _detailOkAction = ClosePayloadResult;
            RefreshPartyStatus();
        }

        /// <summary>
        /// Marks the room's payload spent and saves at once - the same rule an event follows, and for
        /// the same reason: without it the player re-loots the cache by walking out and back in, or by
        /// quitting to the menu and resuming.
        /// </summary>
        private void TakePayload()
        {
            _currentRoom.MarkPayloadTaken();
            if (DungeonSaveManager.HasInstance)
            {
                DungeonSaveManager.Instance.Save(_currentRoom);
            }
        }

        private void ClosePayloadResult()
        {
            SetShown(_detailWindow, false);
            SettleRoom();
        }
    }
}
