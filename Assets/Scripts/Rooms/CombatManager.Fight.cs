using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Combat.Triggers;
using Assets.Scripts.Audio;
using Assets.Scripts.Dungeon;
using Assets.Scripts.Enemies;
using Assets.Scripts.Enemies.Behaviors;
using Assets.Scripts.Heroes;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using Assets.Scripts.UnitStats;
using ImmoralityGaming.Fundamentals;
using UnityEngine;
using Assets.Scripts.Events;
using ImmoralityGaming.Localization;

namespace Assets.Scripts.Rooms
{
    /// <summary>The fight, by job: the fight itself: setup, the turn loop, deaths, rewards and teardown.</summary>
    public partial class CombatManager
    {
        public void StartCombat(Party party, Room room)
        {
            if (InCombat)
            {
                return;
            }

            StartCoroutine(RunCombat(party, room));
        }

        private IEnumerator RunCombat(Party party, Room room)
        {
            InCombat = true;
            // The player's combat speed (Options) runs only while the fight does.
            CombatSpeed.Apply(true);
            _currentCombatRoom = room;
            _currentParty = party;
            _combatLoot.Clear();
            _combatXp = 0;
            _xpAtCombatStart.Clear();
            foreach (var pair in party.XpEarnedThisLevel)
            {
                _xpAtCombatStart[pair.Key] = pair.Value;
            }
            _combatGold = 0;
            _fightGoldBonus = 0;
            _robbed.Clear();
            _freeCasts.Clear();
            // A cast cut off mid-presentation must not leave a plate frozen into the next fight.
            HealthDisplay.ReleaseAll();
            _currentCombatHadBoss = room.Enemies.Any(e => e != null && e.IsBoss);
            _squad.Clear();
            _allies.Clear();
            _standIns.Clear();
            _mounts.Clear();
            _parting.Clear();
            // Every gauge opens the fight where the run left it (UltraOps: kept for the run).
            _ultraGauge.Clear();
            _gaugeCredit.Clear();
            _threatWindow = null;
            _forms.Clear();
            var runGauges = DungeonManager.HasInstance ? DungeonManager.Instance.UltraGauges : null;
            foreach (var hero in party.Heroes.Where(h => h != null))
            {
                _ultraGauge[hero] = runGauges != null ? runGauges.Get(hero.HeroKey) : 0;
                _gaugeCredit[hero] = 0f;
            }
            _threat.Reset();
            _pendingDeaths.Clear();
            _fallen.Clear();
            _fielded.Clear();
            Events = new CombatEvents();
            // The game's own bookkeeping listens first (order 0 is the default), so authored reactions
            // always see a death after it has been queued for its rewards.
            Events.Subscribe<UnitDefeated>(_pendingDeaths.Add);
            // The Ultra gauge and threat are fed from the health stream, not from re-reading bars.
            Events.Subscribe<HealthChange>(TrackHealth);
            BuffTracker = new CombatBuffTracker { Events = Events };
            _turnManager.SetBuffTracker(BuffTracker);
            // A TurnDelay effect (Exatrix's claws) pushes units back on this fight's clock.
            _calculator.Clock = _turnManager;
            _calculator.Events = Events;
            // Disassemble's odds grow with the Bestiary's kills of that enemy (DisassembleOps).
            _calculator.KillsOf = BestiaryKills;
            // Authored reactions (items, enemies, summons) listen after the bookkeeping above, and the
            // log and floating text after them. The registry needs no reference kept: the stream holds it.
            new TriggerRegistry(Events, _calculator, BuffTracker, () => HeroSideUnits().Concat(GetAliveEnemies()));
            Events.Subscribe<ReactionResolved>(ShowReaction, TriggerRegistry.Order + 1);
            // The Warrior's Momentum: stacks of Strength for every turn in a row he lands a hit.
            _momentum = new MomentumTracker(Events, BuffTracker);
            _momentum.Changed += ShowMomentum;
            // The Mage's Focus: a point for every turn he casts, spent by MultiCast.
            _focus = new FocusTracker(Events);
            _focus.Changed += ShowFocus;
            // A Revive (the Cleric's) stands a hero who fell in this fight back up; the effect sets the
            // bar and announces it, and this puts them back on the stage and the clock.
            _calculator.FallenAllies = FallenAlliesOf;
            // A drain shared with the party (Soul Feast) heals the whole hero side.
            _calculator.LivingAllies = caster => caster == null || caster is Enemy
                ? new List<ICombatUnit>()
                : HeroSideUnits();
            // The Rogue's Steal and Fence pay out of this fight.
            _calculator.StealFrom = StealFrom;
            _calculator.AddGoldBonus = percent => _fightGoldBonus = Mathf.Max(_fightGoldBonus, percent);
            // The Arcane Circlet: the cast in progress keeps its charge (SpendCharge reads the mark).
            _calculator.FreeCast = unit => unit is Hero && !_freeCasts.Contains(unit) && AddFreeCast(unit);
            // Divine Intervention's counterstrike, fired off the same stream.
            new RetaliationTracker(Events, _calculator, BuffTracker);
            // The Warlock's Leeching Curse: the party's hits on a cursed enemy heal him.
            new LeechTracker(Events, BuffTracker);
            Events.Subscribe<UnitRevived>(HandleRevived);
            // A RestoreCharge effect (Life Tap) gives charges back to the run's ability slots.
            _calculator.Charges = DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null
                ? new EquippedChargeBank(DungeonManager.Instance.MagicState)
                : null;
            _tagTracker = new MagicTagTracker();
            // Prefer the shared combo catalog (single source of truth, also used by the hub
            // Forge); fall back to the scene-serialized list if no catalog is present.
            var combos = MagicComboCatalog.HasInstance
                ? new List<MagicComboSO>(MagicComboCatalog.Instance.AllCombos)
                : _cardCombos;
            _comboDetector = new ComboDetector(combos);

            // Charges are deliberately NOT refilled here. They are a run resource: filled on the
            // run's first floor, spent across the rest of it, and restored only by resting in a
            // refuge. Refilling per fight made magic infinite - a dozen casts and two free Heals in
            // every room, which is why a whole run could be cleared without the party's health ever
            // trending down. See EquippedMagicState.RefillsOnLevelStart.

            // Seed the fresh buff tracker with anything room events hung on the party for this
            // level. The tracker is rebuilt per fight and ticks per turn, so a level-scoped curse
            // has to be re-applied here or it would only ever affect the fight it was picked up in
            // - and there is no fight when it is picked up.
            if (DungeonManager.HasInstance && DungeonManager.Instance.Afflictions != null)
            {
                foreach (var hero in party.Heroes)
                {
                    if (hero != null && hero.IsAlive)
                    {
                        DungeonManager.Instance.Afflictions.SeedCombat(hero.HeroKey, hero, BuffTracker);
                    }
                }
            }


            // Swap the floor's bed for a fight (a boss gets its own climax track).
            LevelMusic.PlayCombat(_currentCombatHadBoss);

            // Raise the FF-style battle stage: heroes left, enemies right, over a background
            // that hides the dungeon. Must run before EnsureHealthBars so the bars anchor at
            // the battle positions. One frame lets the formation render before turn 1.
            CombatStage.Instance.Begin(party, room);
            yield return null;

            // Build the unit list
            var units = new List<ICombatUnit>();
            foreach (var hero in party.Heroes)
            {
                if (hero.IsAlive)
                {
                    units.Add(hero);
                }
            }
            foreach (var enemy in room.Enemies)
            {
                if (enemy != null && enemy.IsAlive)
                {
                    // Fresh per-combat runtime state (cadence, charge, limited actions) for behaviors.
                    enemy.ResetForCombat();
                    units.Add(enemy);
                    RecordEnemySeen(enemy);
                }
            }

            _fielded.AddRange(units.OfType<Hero>());
            _turnManager.Initialize(units);
            EnsureHealthBars(units);
            BroadcastTurnOrder();

            Events.Publish(new CombatStarted
            {
                Heroes = units.Where(u => u.IsHero).ToList(),
                Enemies = units.Where(u => !u.IsHero).ToList()
            });
            // A reaction at the start of the fight may already have felled something.
            ResolveDeaths();


            // Combat loop
            while (HasAliveHeroes(party) && HasAliveEnemies(room))
            {
                var unit = _turnManager.GetNextUnit();
                if (unit == null)
                {
                    break;
                }

                if (!unit.IsAlive)
                {
                    continue;
                }

                // A buff this unit lands on itself this turn is not ticked by this turn's upkeep.
                BuffTracker.BeginTurn(unit);
                _freeCasts.Clear();
                Events.Publish(new TurnStarted { Unit = unit });
                // A reaction at the turn's start may already have felled someone - the actor included.
                ResolveDeaths();

                // Point the on-field turn marker at whoever is acting.
                TurnIndicator.Instance.SetTarget(unit);

                // Harm at the start of the turn (TickTiming): bleed, poison and burn fire before the
                // unit acts - and before a freeze is checked, so freezing something does not shelter
                // it. A tick that kills ends the turn right here: the whole point of the split is
                // that a lethal damage-over-time denies the action.
                yield return ResolveOverTimeTicks(unit, TickTiming.StartOfTurn);
                if (!unit.IsAlive)
                {
                    yield return ResolveParting();
                    CloseTurn(unit);
                    OnTurnExecuted?.Invoke();
                    BroadcastTurnOrder();
                    continue;
                }

                if (SkipsTurn(unit))
                {
                    yield return EndOfTurnUpkeep(unit);
                    AfterSummonTurn(unit, dismissed: false);
                    AfterFormTurn(unit);
                    yield return ResolveParting();
                    CloseTurn(unit);
                    OnTurnExecuted?.Invoke();
                    BroadcastTurnOrder();
                    continue;
                }

                if (unit.IsHero)
                {
                    // Wait for player input
                    _pendingAction = HeroAction.None;
                    _pendingCastAction = null;
                    _pendingMultiCast = false;
                    _pendingAttackTarget = null;
                    _pendingUseItem = null;
                    _pendingUseItemTarget = null;
                    _pendingSummon = null;
                    _pendingSummonAbility = null;
                    _pendingSummonTargets = null;
                    _pendingUltra = null;
                    _pendingUltraTarget = null;

                    if (unit is SummonUnit automaton && automaton.Summon != null && automaton.Summon.ActsAlone
                        && automaton.AttackAbility != null)
                    {
                        // It needs no orders (the Auto Turret): its Attack, at a random enemy
                        // (ExecuteSummonAbility picks one when no target is given).
                        yield return new WaitForSeconds(_turnDelay);
                        _pendingSummonAbility = automaton.AttackAbility;
                        _pendingAction = HeroAction.SummonAbility;
                    }
                    else
                    {
                        OnHeroTurnStarted?.Invoke(unit);
                    }

                    while (_pendingAction == HeroAction.None)
                    {
                        yield return null;
                    }

                    // Threat is what the action changed: health taken off enemies, health put back
                    // on the hero side. Measured around the whole action rather than inside each
                    // executor, so attacks, abilities, items and summons all count the same way.
                    OpenThreatWindow();

                    if (_pendingAction == HeroAction.Attack)
                    {
                        yield return ExecuteHeroTurn(unit, room);
                    }
                    else if (_pendingAction == HeroAction.Cast && _pendingCastAction != null)
                    {
                        yield return _pendingMultiCast
                            ? ExecuteMultiCast(_pendingCastAction, _pendingCastSlot)
                            : ExecuteCastAction(_pendingCastAction, _pendingCastSlot);
                    }
                    else if (_pendingAction == HeroAction.UseItem && _pendingUseItem != null)
                    {
                        yield return ExecuteUseItemAction(unit, _pendingUseItem, _pendingUseItemTarget);
                    }
                    else if (_pendingAction == HeroAction.Summon && _pendingSummon != null)
                    {
                        yield return ExecuteSummonAction(unit, _pendingSummon, _pendingSummonTarget);
                    }
                    else if (_pendingAction == HeroAction.SummonAbility && _pendingSummonAbility != null
                             && unit is SummonUnit abilityCaster)
                    {
                        yield return ExecuteSummonAbility(abilityCaster, _pendingSummonAbility, _pendingSummonTargets);
                    }
                    else if (_pendingAction == HeroAction.Ultra && _pendingUltra != null && unit is Hero ultraHero)
                    {
                        yield return ExecuteUltra(ultraHero, _pendingUltra, _pendingUltraTarget);
                    }
                    else if (_pendingAction == HeroAction.Dismiss && unit is SummonUnit dismissing && CanDismiss(dismissing))
                    {
                        yield return new WaitForSeconds(_turnDelay * 0.5f);
                    }

                    if (_pendingAction == HeroAction.Summon && _pendingSummon?.Summon != null)
                    {
                        CreditThreat(unit, _pendingSummon.Summon.ThreatMultiplier, _pendingSummon.Summon.BonusThreat);
                    }
                    else
                    {
                        CreditThreat(unit, ThreatSourceFor(_pendingAction));
                    }
                }
                else
                {
                    yield return new WaitForSeconds(_turnDelay);
                    yield return ExecuteEnemyTurn(unit, party);
                }

                // Every action path resolves its own deaths where they belong in its presentation;
                // this catches anything a path did not (an item, a reaction) before the turn moves on.
                ResolveDeaths();

                yield return EndOfTurnUpkeep(unit);
                AfterSummonTurn(unit, dismissed: _pendingAction == HeroAction.Dismiss && unit is SummonUnit leaving && CanDismiss(leaving));
                AfterFormTurn(unit);
                yield return ResolveParting();
                CloseTurn(unit);
                OnTurnExecuted?.Invoke();
                BroadcastTurnOrder();
            }

            // A turn-end reaction can fell the last enemy as the loop's condition is checked: its death
            // still pays out.
            ResolveDeaths();

            // The last enemy fell while a summon was out: the party comes back for an ordinary
            // victory, and XP splits across them as usual (it was already awarded per kill).
            if (_squad.Count > 0)
            {
                EndSummon(SummonExit.Victory);
            }
            // Allies go home with the fight, won or lost.
            EndAllAllies(SummonExit.Victory);
            // ...and every form comes off, the health bar keeping its share.
            foreach (var transformed in _forms.Keys.ToList())
            {
                EndForm(transformed);
            }

            // Clear turn order display + the on-field turn marker
            OnTurnOrderChanged?.Invoke(new List<ICombatUnit>());
            TurnIndicator.Instance.Clear();

            // Determine outcome
            CombatOutcome outcome;
            if (!HasAliveHeroes(party))
            {
                outcome = CombatOutcome.PlayerDied;
                CombatAudio.Play(CombatSound.Defeat);
                // The bed goes with the party: the defeat stinger and the death screen play out over
                // silence, and the hub's own track comes up when the menu scene loads.
                MusicPlayer.Stop(fadeSeconds: 1.5f);
                // Somber tint that lingers under the death screen (approximates a desaturate).
                ScreenFade.Instance.FadeTo(new Color(0.06f, 0f, 0.02f), 0.55f, 0.7f);

                // Tear the stage down (unfreeze camera, drop background) before the death screen.
                CombatStage.Instance.End(restoreEnemyPositions: false);
            }
            else
            {
                outcome = CombatOutcome.Victory;
                CombatAudio.Play(CombatSound.Victory);
                // Cut the fight's bed so the win stinger lands in the clear; the floor's theme comes
                // back when the summary is dismissed (FinishVictory).
                MusicPlayer.Stop(fadeSeconds: 0.6f);
                // A quick warm flash to punctuate the win before the summary appears.
                ScreenFade.Instance.Flash(new Color(1f, 0.92f, 0.55f), 0.5f, 0.06f, 0.4f);

                // Keep the battle stage up so the victory summary shows over it; it is torn down
                // (and doors enabled / level completed) when the summary is dismissed — see
                // FinishVictory, called by RoomActionUI's Continue button.
                _lastVictoryRoom = room;
            }

            // Save dungeon state (not party — deferred until level completion)
            if (DungeonSaveManager.Instance != null)
            {
                DungeonSaveManager.Instance.Save(party.CurrentRoom);
            }

            BuffTracker.Clear();
            _tagTracker.Clear();
            _currentCombatRoom = null;
            InCombat = false;
            CombatSpeed.Apply(false);

            bool levelCleared = outcome == CombatOutcome.Victory && room.IsExit && !HasAliveEnemies(room);
            bool bossDefeated = outcome == CombatOutcome.Victory && _currentCombatHadBoss;
            var result = new CombatResult
            {
                Outcome = outcome,
                RemainingEnemies = room.Enemies.Count(e => e != null && e.IsAlive),
                Loot = new List<LootAward>(_combatLoot),
                XpGained = _combatXp,
                XpByHero = XpPaidThisCombat(party),
                GoldGained = _combatGold,
                LevelCleared = levelCleared,
                BossDefeated = bossDefeated,
                RunCompleted = levelCleared && DungeonManager.IsFinalRunLevel
            };

            Events.Publish(new CombatEnded { PartyWon = outcome == CombatOutcome.Victory });
            GameEvents.Publish(new CombatFinished
            {
                Won = outcome == CombatOutcome.Victory,
                HadBoss = _currentCombatHadBoss,
                HeroKeys = _fielded.Where(h => h != null).Select(h => h.HeroKey).ToList(),
                FoesStanding = room.Enemies.Where(e => e != null && e.IsAlive).Select(e => e.Definition.SaveKey).ToList()
            });
            Events = null;
            _calculator.Events = null;
            OnCombatEnded?.Invoke(result);
            // Stage teardown + OnDungeonCleared are deferred to FinishVictory (the summary's Continue).
        }

        /// <summary>
        /// Called when the player dismisses the victory summary: lowers the battle stage and re-opens
        /// the room's doors.
        ///
        /// <para>It no longer completes the level, even after clearing the exit room - that is the
        /// stairs' job. Doors are re-enabled unconditionally, which also un-seals a boss
        /// room (<c>DisableAllDoors</c>) so the player can go back for anything they left.</para>
        /// </summary>
        public void FinishVictory()
        {
            CombatStage.Instance.End(restoreEnemyPositions: false);
            LevelMusic.PlayExploration(); // back out of the fight, back onto the floor's theme
            _lastVictoryRoom?.EnableAllDoors();
        }

        /// <summary>
        /// Runs everything that went down since the last call through its side's death path: an enemy
        /// pays out and leaves, a hero falls for the floor, a summon leaves the field. <b>The one place
        /// deaths are handled</b>, called at the point each action's presentation wants them - after a
        /// swing lands, after a cast's numbers have floated - and once more after every action.
        ///
        /// <para>Deaths arrive as <see cref="UnitDefeated"/> events, raised by <see cref="HealthOps"/>
        /// the moment a bar crosses zero, which is what lets a kill be credited to whoever struck it.
        /// A sweep of both sides follows as a safety net for anything that went down without one - nothing
        /// should, since every health write goes through <see cref="HealthOps"/>
        /// (<c>CombatEventsTests.OnlyHealthOps_WritesHealth</c>), but a runaway reaction chain the stream
        /// cut short can lose an event, and a lost death must not leave a corpse on the clock.</para>
        /// </summary>
        private void ResolveDeaths()
        {
            var deaths = _pendingDeaths.ToList();
            _pendingDeaths.Clear();
            foreach (var death in deaths)
            {
                ResolveDeath(death.Victim, death.Killer, death.Cause);
            }

            if (_currentCombatRoom != null)
            {
                foreach (var enemy in _currentCombatRoom.Enemies.Where(e => e != null && !e.IsAlive).ToList())
                {
                    ResolveDeath(enemy, null, HealthCause.Attack);
                }
            }
            foreach (var hero in _fielded.Where(h => h != null && !h.IsAlive && !_fallen.Contains(h)).ToList())
            {
                ResolveDeath(hero, null, HealthCause.Attack);
            }
            foreach (var summon in BesideParty.Concat(_squad).Where(u => u != null && u.Stats.Health <= 0 && !u.Stay.HasLeft).ToList())
            {
                ResolveSummonDamaged(summon);
            }
        }

        private void ResolveDeath(ICombatUnit victim, ICombatUnit killer, HealthCause cause)
        {
            // Back on its feet since (an absorbed hit after the killing blow): not a death any more.
            if (victim == null || victim.IsAlive)
            {
                return;
            }

            switch (victim)
            {
                case SummonUnit summon:
                    ResolveSummonDamaged(summon);
                    break;

                case Enemy enemy:
                    // Already paid out and removed: the room no longer holds it.
                    if (_currentCombatRoom == null || !_currentCombatRoom.Enemies.Contains(enemy))
                    {
                        return;
                    }
                    HandleEnemyDeath(enemy, killer, cause);
                    break;

                case Hero hero:
                    if (!_fallen.Add(hero))
                    {
                        return;
                    }
                    // A fallen hero's threat goes with them: back on their feet, they start clean.
                    _threat.Clear(hero);
                    HandleHeroDeath(hero);
                    _turnManager.RemoveUnit(hero);
                    break;
            }
        }

        /// <summary>
        /// Closes a unit's turn on the rules side: the gauges read the turn's damage, <see cref="TurnEnded"/>
        /// goes out (a turn-end reaction may fire), anything that fell is resolved, and the turn's
        /// reaction lines join its log. Every turn path calls it before the log is kept.
        /// </summary>
        private void CloseTurn(ICombatUnit unit)
        {
            UpdateUltraGauges();
            Events.Publish(new TurnEnded { Unit = unit });
            ResolveDeaths();
        }

        /// <summary>
        /// A triggered reaction resolved: its name over the bearer, its numbers over whoever it landed
        /// on. Presentation only - the effects already happened - so it runs
        /// alongside whatever the turn is animating rather than holding it up.
        /// </summary>
        private void ShowReaction(ReactionResolved reaction)
        {
            if (reaction?.Bearer == null)
            {
                return;
            }
            if (reaction.Bearer.Transform != null)
            {
                ShowFloatingLabel(reaction.Bearer.Transform.position + new Vector3(0f, 0.55f, 0f),
                    reaction.Name, new Color(1f, 0.8f, 0.35f), 0.14f);
            }
            // A phase change is the fight turning, so it gets the ability banner as well as the float.
            if (reaction.Trigger != null && reaction.Trigger.When == TriggerKind.OnHealthBelow
                && !reaction.Bearer.IsHero && !string.IsNullOrEmpty(reaction.Name))
            {
                OnEnemyAbilityUsed?.Invoke(reaction.Bearer, reaction.Name);
                CombatAudio.Play(CombatSound.BossSignature);
            }
            if (reaction.Result != null && reaction.Result.Entries.Count > 0)
            {
                StartCoroutine(_presenter.Present(reaction.Result));
            }
        }

        /// <summary>
        /// End-of-turn upkeep for the unit that just acted: end-of-turn over-time effects
        /// (regeneration) fire, then every duration ticks down. Damage-over-time fired at the start
        /// of the turn already (see the turn loop and <see cref="TickTiming"/>).
        ///
        /// <para><b>The order is the point.</b> A buff with one turn left has to deal its last tick
        /// before it expires, so every tick runs before <c>TickBuffs</c>. And ticks run on the
        /// <i>victim's</i> turn rather than on a global clock, because the turn is the unit of
        /// time in a CTB system — which is what makes Haste and Slow change how often something
        /// burns, for free.</para>
        /// </summary>
        private IEnumerator EndOfTurnUpkeep(ICombatUnit unit)
        {
            yield return ResolveOverTimeTicks(unit, TickTiming.EndOfTurn);

            BuffTracker.TickBuffs(unit);
            _tagTracker.TickTags(unit);
        }

        /// <summary>
        /// Fires <paramref name="unit"/>'s over-time effects for one <see cref="TickTiming"/>, shows
        /// them, and runs the death path if one of them killed it. Shared by the start and the end of
        /// the turn so a tick kills the same way whenever it lands.
        /// </summary>
        private IEnumerator ResolveOverTimeTicks(ICombatUnit unit, TickTiming timing)
        {
            var ticks = BuffTracker.ResolveOverTime(unit, timing);

            if (ticks.Count > 0)
            {
                foreach (var tick in ticks)
                {
                    ShowOverTimeTick(unit, tick);
                }

                // A unit killed by a tick runs the same death path a killing blow does, credited to
                // whoever applied the effect (CombatBuff.Source).
                ResolveDeaths();

                yield return new WaitForSeconds(OverTimeTickPause);
            }
        }

        /// <summary>Floating number for one over-time tick, tinted by what fired it.</summary>
        private void ShowOverTimeTick(ICombatUnit unit, OverTimeTick tick)
        {
            if (unit?.Transform == null || tick == null)
            {
                return;
            }

            // Healing is green whatever caused it. Colouring purely by BuffType showed an *absorbed*
            // burn — a Fire-absorbing hero being healed by it — as an orange "+3", which reads as
            // damage. Direction first, flavour second.
            Color color;
            if (tick.Heals)
            {
                color = new Color(0.4f, 0.95f, 0.5f);
            }
            else
            {
                switch (tick.BuffType)
                {
                    case BuffType.Burning:
                        color = new Color(1f, 0.55f, 0.15f);
                        break;
                    case BuffType.Poisoned:
                        color = new Color(0.65f, 0.9f, 0.3f);
                        break;
                    default:
                        color = new Color(0.9f, 0.25f, 0.3f);
                        break;
                }
            }

            string text = tick.Heals ? $"+{tick.Amount}" : tick.Amount.ToString();
            ShowFloatingLabel(
                unit.Transform.position + new Vector3(0f, 0.3f, 0f), text, color, 0.15f);

            if (!tick.Heals)
            {
                // Same flash a hit gets, at a lower weight — a tick should read as damage without
                // shaking the camera as hard as a sword.
                CombatFeedback.Instance.PlayImpact(unit, tick.Amount, OverTimeTickPunch);
            }
        }

        private bool SkipsTurn(ICombatUnit unit)
        {
            foreach (var statusEffect in BuffTracker.GetActiveStatusEffects(unit))
            {
                var handler = BuffHandlerRegistry.Get(statusEffect);
                if (handler != null && handler.SkipsTurn)
                {
                    return true;
                }
            }

            return false;
        }

        private void BroadcastTurnOrder()
        {
            var order = _turnManager.GetTurnOrder(10);
            OnTurnOrderChanged?.Invoke(order);
        }

        /// <summary>Ensures every combat unit has an HP bar (idempotent — bars self-manage visibility).</summary>
        private void EnsureHealthBars(List<ICombatUnit> units)
        {
            foreach (var unit in units)
            {
                if (unit?.Transform == null)
                {
                    continue;
                }
                var go = unit.Transform.gameObject;
                if (go.GetComponent<UnitHealthBar>() == null)
                {
                    go.AddComponent<UnitHealthBar>();
                }
            }
        }

        /// <summary>Each hero's share of this fight's XP: the level's running tally now, less what it was when the fight began.</summary>
        private List<KeyValuePair<string, int>> XpPaidThisCombat(Party party)
        {
            var paid = new List<KeyValuePair<string, int>>();
            if (party == null)
            {
                return paid;
            }
            foreach (var hero in party.Heroes)
            {
                if (hero == null || !party.XpEarnedThisLevel.TryGetValue(hero, out int now))
                {
                    continue;
                }
                _xpAtCombatStart.TryGetValue(hero, out int before);
                if (now - before > 0)
                {
                    paid.Add(new KeyValuePair<string, int>(hero.DisplayName, now - before));
                }
            }
            return paid;
        }

        /// <summary>An enemy's death, paid out: XP, gold, loot, the bestiary, then off the clock and out
        /// of the room. Only <see cref="ResolveDeath"/> calls it. <paramref name="killer"/> is whoever
        /// struck the blow, or null.</summary>
        private void HandleEnemyDeath(Enemy enemy, ICombatUnit killer, HealthCause cause = HealthCause.Attack)
        {
            if (enemy == null)
            {
                return;
            }

            // Kill rewards: XP (awarded now), gold (accumulated, banked on clear).
            // Through the enemy, not the definition: a level scales its rewards alongside its stats.
            int xp = enemy.XpReward;
            int gold = StealOps.WithBonus(enemy.GoldReward, _fightGoldBonus);
            if (xp > 0)
            {
                _combatXp += xp;
                _currentParty?.DistributeXp(xp, EconomySource.Kill);
            }
            if (gold > 0)
            {
                _combatGold += gold;
                MetaProgressManager.Instance.AddPendingGold(gold, EconomySource.Kill);
            }

            // Loot: every line of the drop table rolls on its own, so one kill can yield a piece of
            // gear and the raw material the thing was made of. Only what actually landed in the bag
            // reaches the victory summary and the bestiary.
            // Taken apart (the Tinkerer's Disassemble): its salvage rolls on top of its loot.
            var table = cause == HealthCause.Disassemble && enemy.Definition != null && enemy.Definition.SalvageTable != null
                ? enemy.LootTable.Concat(enemy.Definition.SalvageTable).ToList()
                : enemy.LootTable;
            // A body another enemy called in pays nothing (XpReward and GoldReward are 0 for it too), so a
            // summoner cannot be farmed.
            if (enemy.IsSummoned)
            {
                table = new List<LootDrop>();
            }
            foreach (var award in LootRoller.Roll(
                         table, DungeonManager.RunLevelIndex, () => UnityEngine.Random.Range(0f, 1f)))
            {
                // A revisit below its best fear does not pay the scarce materials (docs/plans/REVISITS.md).
                // A recipe already known teaches nothing, so it is not dropped again.
                if (RunFear.Current.Withholds(award.Item) || InventoryManager.Instance.AlreadyKnown(award.Item))
                {
                    continue;
                }
                InventoryManager.Instance.AddItem(award, EconomySource.Kill);
                Debug.Log($"Item dropped: {award.Item.DisplayName} ({award.Item.Key}) x{award.Quantity}");
                _combatLoot.Add(award);
                RecordLootObserved(enemy, award.Item);
            }

            RecordEnemyKilled(enemy);
            GameEvents.Publish(new EnemyDefeated
            {
                Enemy = enemy.Definition,
                EnemyKey = enemy.Definition != null ? enemy.Definition.SaveKey : null,
                IsBoss = enemy.IsBoss,
                // A poison's kill is its poisoner's (CombatBuff.Source); a summon's is nobody's hero.
                KillerHeroKey = (killer as Hero)?.HeroKey
            });
            _turnManager.RemoveUnit(enemy);
            _currentCombatRoom?.Enemies.Remove(enemy);
            // Removed from combat immediately; the object lingers only for its pop/fade.
            CombatAudio.Play(CombatSound.EnemyDeath);
            CombatFeedback.Instance.KillWithEffect(enemy.gameObject);
        }

        /// <summary>A fallen hero was revived: back on the stage, back on the clock (acting in turn, not at once).</summary>
        private void HandleRevived(UnitRevived revived)
        {
            if (!(revived?.Unit is Hero hero) || !_fallen.Remove(hero))
            {
                return;
            }
            var sr = hero.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = true;
            }
            // Up from where they lay: the idle loop runs again.
            var animator = hero.GetComponent<SpriteAnimator>();
            if (animator != null)
            {
                animator.Resume();
            }
            _turnManager.AddUnit(hero, false);
            // Revived while a summon has taken the party's place: they wait off the stage with the rest
            // of the party, and come back with it (CombatStage.RestoreParty, TurnManager.Resume).
            if (ReplacementOut)
            {
                CombatStage.Instance.HideUnits(new Component[] { hero });
                _turnManager.Suspend(new ICombatUnit[] { hero });
            }
            // Through the reaction log: the action writes its own line after this, which would drop it.
            BroadcastTurnOrder();
        }

        private void HandleHeroDeath(Hero hero)
        {
            if (hero == null)
            {
                return;
            }

            EndAlliesOf(hero);
            EndForm(hero);

            // A hero with a drawn fall plays it and lies there for the rest of the fight (playtest 3
            // finding 7); one without simply vanishes. Either way the body is gone from the next fight:
            // only living heroes are placed on the stage, and RestoreAfterCombat hides every hero.
            var sr = hero.GetComponent<SpriteRenderer>();
            var animator = hero.GetComponent<SpriteAnimator>();
            var fall = hero.HeroSO != null && !hero.InForm ? hero.HeroSO.DeathFrames : null;
            if (fall != null && fall.Length > 0 && animator != null && sr != null)
            {
                animator.PlayOnceAndHold(fall, DeathAnimationSeconds);
                return;
            }
            if (sr != null)
            {
                sr.enabled = false;
            }
        }

        // ------------------------------------------------------------------ threat

        /// <summary>
        /// Starts counting what the coming action does to every unit standing on both sides
        /// (<see cref="TrackHealth"/>). A unit that arrives during the action is not in the window.
        /// </summary>
        private void OpenThreatWindow()
        {
            _threatWindow = new Dictionary<ICombatUnit, int>();
            foreach (var unit in GetAliveEnemies().Concat(HeroSideUnits()))
            {
                _threatWindow[unit] = 0;
            }
        }

        /// <summary>
        /// Credits <paramref name="actor"/> with the damage its action dealt to enemies and the
        /// healing it landed on its own side, then closes the window. Only what landed counts: a kill
        /// is worth the health the enemy had left, an overheal nothing past full.
        /// </summary>
        private void CreditThreat(ICombatUnit actor, MagicSO source)
        {
            CreditThreat(actor,
                source != null ? source.ThreatMultiplier : 1f,
                source != null ? source.BonusThreat : 0);
        }

        /// <summary>The same credit with the threat settings given directly - a summoning's come
        /// from its <c>SummonSO</c>, not from an ability.</summary>
        private void CreditThreat(ICombatUnit actor, float multiplier, int bonus)
        {
            int damage = 0;
            int healing = 0;
            if (_threatWindow != null)
            {
                foreach (var entry in _threatWindow)
                {
                    if (entry.Key.IsHero)
                    {
                        healing += Mathf.Max(0, entry.Value);
                    }
                    else
                    {
                        damage += Mathf.Max(0, -entry.Value);
                    }
                }
            }
            _threatWindow = null;

            _threat.Credit(actor, damage, healing, multiplier, bonus);
        }

        /// <summary>The ability behind this turn's action, whose threat settings apply - or null.</summary>
        private MagicSO ThreatSourceFor(HeroAction action)
        {
            switch (action)
            {
                case HeroAction.Cast:
                    return _pendingCastAction != null ? _pendingCastAction.Magic : null;
                case HeroAction.SummonAbility:
                    return _pendingSummonAbility;
                default:
                    return null;
            }
        }

        /// <summary>Whether the hero side still stands. Always true while a summon is out: a wipe is
        /// impossible then, because nothing can reach the party.</summary>
        private bool HasAliveHeroes(Party party)
        {
            return ReplacementOut || party.Heroes.Any(h => h != null && h.IsAlive);
        }

        private bool HasAliveEnemies(Room room)
        {
            return room.Enemies.Any(e => e != null && e.IsAlive);
        }

        /// <summary>
        /// Completes the level. Raised by the player taking the stairs in a cleared exit room, so
        /// finishing a level is always a decision.
        /// </summary>
        public void NotifyDungeonCleared()
        {
            OnDungeonCleared?.Invoke();
        }

        /// <summary>Fleeing is a party action, and while a summon has taken the party's place there is
        /// no party to flee: Dismiss it, then flee on a hero's turn (§4b). Allies need no such step:
        /// they go home with the fight.</summary>
        public bool CanFlee(Party party)
        {
            return !ReplacementOut && party.PreviousRoom != null;
        }

        public void Flee(Party party, Door entryDoor, Room currentRoom)
        {
            currentRoom.EnableAllDoors();
            party.PlaceInRoom(party.PreviousRoom, entryDoor);
            GameManager.Instance.EnterRoom(party.CurrentRoom, entryDoor);
        }
    }
}
