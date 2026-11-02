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
    /// <summary>The fight, by job: Ultras: the gauge, strikes, forms, the Sacrifice and mounts.</summary>
    public partial class CombatManager
    {
        // ------------------------------------------------------------------ Ultras

        /// <summary>
        /// Settles every hero's gauge with the weighted health credited since it was last settled
        /// (<see cref="TrackHealth"/>, <see cref="UltraOps.Credit"/>: damage taken, damage dealt,
        /// healing done). Run once per turn, just before the turn is reported, so the party window
        /// shows the gauge it is about to offer. A fallen hero's gauge keeps what it had and gains
        /// nothing.
        /// </summary>
        private void UpdateUltraGauges()
        {
            foreach (var hero in _gaugeCredit.Keys.ToList())
            {
                float credit = _gaugeCredit[hero];
                if (hero != null && credit > 0f && hero.IsAlive)
                {
                    _ultraGauge.TryGetValue(hero, out int gauge);
                    SetUltraGauge(hero, UltraOps.Add(gauge, UltraOps.GainFor(credit, hero.GetEffectiveMaxHealth())));
                }
                _gaugeCredit[hero] = 0f;
            }
        }

        /// <summary>Sets a hero's gauge for this fight and for the run, so a gauge built or spent here
        /// is what the next fight opens with.</summary>
        private void SetUltraGauge(Hero hero, int gauge)
        {
            _ultraGauge[hero] = gauge;
            if (DungeonManager.HasInstance && DungeonManager.Instance.UltraGauges != null)
            {
                DungeonManager.Instance.UltraGauges.Set(hero.HeroKey, gauge);
            }
        }

        /// <summary>
        /// Every change to a bar, credited to the Ultra gauges (<see cref="UltraOps.Credit"/>: the hero
        /// hit, the hero who hit or healed; settled once a turn) and summed per unit for threat (every
        /// unit standing when a hero's action began, settled when it ends). For threat, summing
        /// <c>After - Before</c>, floored at 0, telescopes to "the bar now less the bar then", so damage
        /// and healing in the same window net against each other exactly as re-reading the bars did -
        /// and <see cref="HealthOps"/> being the only writer of health is what makes the sum complete.
        /// A form starting or ending rescales the bar; that is not damage taken or healing done, so it
        /// is left out.
        /// </summary>
        private void TrackHealth(HealthChange change)
        {
            if (change?.Target == null || change.Cause == HealthCause.Form)
            {
                return;
            }
            UltraOps.Credit(change, (unit, amount) =>
            {
                if (unit is Hero credited && _gaugeCredit.ContainsKey(credited))
                {
                    _gaugeCredit[credited] += amount;
                }
            });
            int delta = Mathf.Max(0, change.After) - Mathf.Max(0, change.Before);
            if (delta == 0)
            {
                return;
            }
            if (_threatWindow != null && _threatWindow.ContainsKey(change.Target))
            {
                _threatWindow[change.Target] += delta;
            }
        }

        /// <summary>
        /// A hero uses an Ultra (COMBAT_DEPTH §13). The gauge empties, and a Transform Ultra puts the
        /// hero in its form: a bigger bar with the same share filled, the form's attack element and
        /// art, and its abilities on the Ability list. The form lasts the Ultra's turns, counted from
        /// the hero's next one.
        /// </summary>
        private IEnumerator ExecuteUltra(Hero hero, UltraSO ultra, ICombatUnit target)
        {
            if (IsTransformed(hero) || !CanUseUltra(hero, ultra) || !KnownUltras(hero).Contains(ultra)
                || UltraOps.IsMultiCast(ultra))
            {
                // A MultiCast never comes through here: it is cast from the Ability list (ExecuteMultiCast).
                yield break;
            }
            SetUltraGauge(hero, 0);

            switch (ultra.Kind)
            {
                case UltraKind.Strike:
                    yield return ExecuteUltraStrike(hero, ultra, target);
                    break;
                case UltraKind.Sacrifice:
                    yield return ExecuteSacrifice(hero, ultra, target as Hero);
                    break;
                case UltraKind.Mount:
                    yield return ExecuteMount(hero, ultra);
                    break;
                default:
                    yield return ExecuteTransform(hero, ultra);
                    break;
            }
            // An Ultra never pays into the gauge it was just spent from: what the Strike dealt or
            // healed is dropped before the turn settles.
            if (_gaugeCredit.ContainsKey(hero))
            {
                _gaugeCredit[hero] = 0f;
            }
        }

        /// <summary>A Transform Ultra: the hero takes the form for its turns - a bigger bar with the same
        /// share filled, the form's attack element and art, its abilities on the Ability list.</summary>
        private IEnumerator ExecuteTransform(Hero hero, UltraSO ultra)
        {
            int oldMax = hero.GetEffectiveMaxHealth();
            hero.FormMaxHealthPercent = ultra.MaxHealthPercent;
            hero.FormAttackDamageType = ultra.AttackDamageType;
            hero.InForm = true;
            HealthOps.Set(hero, UltraOps.KeepShare(hero.Stats.Health, oldMax, hero.GetEffectiveMaxHealth()),
                new HealthSource(hero, HealthCause.Form), Events);
            SetHeroFrames(hero, ultra.FormFrames, ultra.FormFps);
            _forms[hero] = new ActiveForm { Ultra = ultra, TurnsLeft = ultra.Turns, TakenThisTurn = true };

            CombatAudio.Play(CombatSound.BossSignature);
            CombatFeedback.Instance.Shake(0.25f, 0.4f);
            if (MainCamera.HasInstance)
            {
                MainCamera.Instance.ZoomPunch(0.35f, 0.35f);
            }
            ScreenFade.Instance.Flash(new Color(0.55f, 0.1f, 0.25f), 0.45f, 0.05f, 0.35f);
            ShowFloatingLabel(hero.Transform.position + new Vector3(0f, 0.5f, 0f), Loc.Format("combat.float.ultra", ("ultra", ultra.Label)), new Color(1f, 0.35f, 0.45f), 0.2f);
            yield return new WaitForSeconds(_turnDelay);
        }

        /// <summary>
        /// A Strike Ultra: the big blow lands and it is over. Resolved like a summon's special attack -
        /// the throwaway castable carries no tags and no Forge bonus - with the hero's own stats.
        /// </summary>
        private IEnumerator ExecuteUltraStrike(Hero hero, UltraSO ultra, ICombatUnit chosen = null)
        {
            var castable = UltraOps.CastableFor(ultra);

            CombatAudio.Play(CombatSound.BossSignature);
            CombatFeedback.Instance.Shake(0.25f, 0.4f);
            ScreenFade.Instance.Flash(new Color(0.55f, 0.1f, 0.25f), 0.45f, 0.05f, 0.35f);
            ShowFloatingLabel(hero.Transform.position + new Vector3(0f, 0.5f, 0f), Loc.Format("combat.float.ultra", ("ultra", ultra.Label)), new Color(1f, 0.35f, 0.45f), 0.2f);

            // Each hit picks its targets again from whoever is still standing (Flurry's slashes).
            for (int hit = 0; hit < UltraOps.HitsOf(ultra); hit++)
            {
                var targets = UltraOps.StrikeTargets(ultra, (ICombatUnit)hero, GetTargetableEnemies(), HeroSideUnits(),
                    count => UnityEngine.Random.Range(0, count), chosen);
                if (targets.Count == 0)
                {
                    break;
                }
                var action = new SpellcastAction { Magic = castable, Caster = hero, Targets = targets, CastSlot = -1 };
                HoldHealthDisplay();
                var result = _calculator.Execute(action, BuffTracker);
                RecordCastDamageObserved(action);
                yield return _presenter.Present(result, hero, castable);
                HealthDisplay.ReleaseAll();
                ResolveDeaths();
            }
        }

        /// <summary>
        /// The Cultist's Sacrifice (<see cref="UltraKind.Sacrifice"/>). <paramref name="victim"/> - any
        /// living hero, the Cultist included - falls, and stays down for the floor like any fallen
        /// hero. <see cref="UltraSO.Creature"/> rises where they stood: built off <b>their</b> stats
        /// (base + gear), at full health, its Attack picked by their highest stat, for the rest of the
        /// fight. It is the hero side's (enemies may hit it, area attacks reach it) but not part of the
        /// defeat check: the last hero cannot be given (<see cref="SacrificeTargets"/>), and if the
        /// rest fall the party is down whatever still stands in their places.
        /// </summary>
        private IEnumerator ExecuteSacrifice(Hero cultist, UltraSO ultra, Hero victim)
        {
            if (victim == null || ultra.Creature == null || !SacrificeTargets().Contains(victim))
            {
                yield break;
            }

            var attack = UltraOps.PickStatAbility(ultra, victim.GetEffectiveStat);
            var spot = victim.transform.position;
            var aimedAtVictim = AliveEnemyComponents().Where(e => ReferenceEquals(e.ChargeTarget, victim)).ToList();

            CombatAudio.Play(CombatSound.BossSignature);
            ScreenFade.Instance.Flash(new Color(0.15f, 0.55f, 0.25f), 0.5f, 0.05f, 0.4f);
            ShowFloatingLabel(victim.Transform.position + new Vector3(0f, 0.5f, 0f), Loc.Format("combat.float.ultra", ("ultra", ultra.Label)), new Color(0.5f, 1f, 0.55f), 0.2f);

            // The hero falls - for the floor, exactly as a killing blow leaves them, and through the
            // same death path (the rite is credited to whoever performed it).
            HealthOps.Set(victim, 0, new HealthSource(cultist, HealthCause.Sacrifice), Events);
            ResolveDeaths();

            // Built off the fallen hero (a living unit's stats, so the snapshot has their gear), and
            // never bound to them: it is what is left of them.
            var horror = SummonUnit.Create(ultra.Creature, null, victim, int.MaxValue / 2);
            horror.OverrideAttack(attack);
            _standIns.Add(horror);
            CombatStage.Instance.PlaceAt(horror, spot);
            yield return SummonPresenter.Arrive(horror);

            _turnManager.AddUnit(horror, actsNext: true);
            EnsureHealthBars(new List<ICombatUnit> { horror });
            // A blow wound up at the hero lands on what replaced them.
            foreach (var enemy in aimedAtVictim)
            {
                enemy.ChargeTarget = horror;
            }

        }

        /// <summary>
        /// A Mount Ultra (<see cref="UltraKind.Mount"/>, the Tinkerer's mechs): <see cref="UltraSO.Mech"/>
        /// is assembled where the hero stands and the hero climbs on top. The mech is a guest of the
        /// party - its own menu, the enemies may hit it, it goes when its rider falls - with three
        /// differences: it takes every blow aimed at its rider (<see cref="GuardTable"/>), it acts at once
        /// and then straight after each of the rider's turns (<see cref="TurnManager.Follow"/>), and it
        /// has no turn limit, staying until it breaks or the fight ends. A rider already on a mech
        /// rebuilds it.
        /// </summary>
        private IEnumerator ExecuteMount(Hero hero, UltraSO ultra)
        {
            if (ultra.Mech == null)
            {
                yield break;
            }
            if (_mounts.TryGetValue(hero, out var previous))
            {
                EndAlly(previous, SummonExit.Dismissed);
            }

            var spot = hero.transform.position;
            CombatAudio.Play(CombatSound.BossSignature);
            CombatFeedback.Instance.Shake(0.2f, 0.35f);
            ScreenFade.Instance.Flash(new Color(0.85f, 0.6f, 0.25f), 0.4f, 0.05f, 0.3f);
            ShowFloatingLabel(spot + new Vector3(0f, 0.5f, 0f), Loc.Format("combat.float.ultra", ("ultra", ultra.Label)), new Color(1f, 0.75f, 0.35f), 0.2f);

            // No turn limit: the mech's health is what ends it (int.MaxValue / 2, as a Sacrifice horror).
            var mech = SummonUnit.Create(ultra.Mech, null, hero, int.MaxValue / 2);
            _allies.Add(mech);
            _mounts[hero] = mech;
            CombatStage.Instance.PlaceAt(mech, spot);
            yield return SummonPresenter.Arrive(mech);

            EnsureHealthBars(new List<ICombatUnit> { mech });
            Events.Guards.Set(hero, mech);
            _turnManager.Follow(mech, hero, actsNow: true);
            // She climbs in: one figure on the stage, drawn as the mech with her at the controls
            // (SummonSO.MountedFrames), and one bar - the mech's, which is her shield.
            SetRiderShown(hero, false);
            SetUnitFrames(mech, ultra.Mech.MountedFrames, ultra.Mech.AnimationFps);
            // A blow wound up at the rider now lands on the mech.
            foreach (var enemy in AliveEnemyComponents())
            {
                if (ReferenceEquals(enemy.ChargeTarget, hero))
                {
                    enemy.ChargeTarget = mech;
                }
            }

        }

        /// <summary>The rider climbs down from <paramref name="mech"/>: no longer shielded, and walked
        /// back to where they stood. Safe to call for any ally; a no-op unless it is a mech.</summary>
        private void Dismount(SummonUnit mech)
        {
            var rider = _mounts.FirstOrDefault(p => ReferenceEquals(p.Value, mech)).Key;
            if (rider == null)
            {
                return;
            }
            _mounts.Remove(rider);
            Events?.Guards.ClearGuard(mech);
            // She climbs out where she stood; a rider who fell while aboard stays down.
            if (rider.IsAlive)
            {
                SetRiderShown(rider, true);
            }
        }

        /// <summary>Shows or hides a rider - sprite and health bar together. While she is aboard, the
        /// mech's mounted frames draw her and the mech's bar is the one that matters.</summary>
        private static void SetRiderShown(Hero hero, bool shown)
        {
            if (hero == null)
            {
                return;
            }
            var sr = hero.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = shown;
            }
            var bar = hero.GetComponent<UnitHealthBar>();
            if (bar != null)
            {
                bar.Hidden = !shown;
            }
        }

        /// <summary>How many of <paramref name="unit"/>'s kind the Bestiary has recorded killed -
        /// Disassemble's odds (<see cref="DisassembleOps"/>). Anything that is not an enemy reads 0.</summary>
        private static int BestiaryKills(ICombatUnit unit)
        {
            var enemy = unit as Enemy;
            if (enemy == null || enemy.Definition == null || !MetaProgressManager.HasInstance)
            {
                return 0;
            }
            var entry = MetaProgressManager.Instance.GetBestiaryEntry(enemy.Definition.SaveKey);
            return entry != null ? entry.Kills : 0;
        }

        /// <summary>One of a transformed hero's turns is over: the form counts it down and comes off
        /// after the last. The turn the Ultra was used on does not count.</summary>
        private void AfterFormTurn(ICombatUnit unit)
        {
            var hero = unit as Hero;
            if (hero == null || !_forms.TryGetValue(hero, out var form))
            {
                return;
            }
            if (form.TakenThisTurn)
            {
                form.TakenThisTurn = false;
                return;
            }
            form.TurnsLeft--;
            if (form.TurnsLeft <= 0)
            {
                EndForm(hero);
            }
        }

        /// <summary>The form comes off: the bonus health goes with the bar keeping its share, and the
        /// hero's own attack and art come back. Safe to call on a hero with no form.</summary>
        private void EndForm(Hero hero)
        {
            if (hero == null || !_forms.TryGetValue(hero, out var form))
            {
                return;
            }
            _forms.Remove(hero);
            int oldMax = hero.GetEffectiveMaxHealth();
            hero.FormMaxHealthPercent = 0;
            hero.FormAttackDamageType = DamageType.Normal;
            hero.InForm = false;
            if (hero.IsAlive)
            {
                HealthOps.Set(hero, UltraOps.KeepShare(hero.Stats.Health, oldMax, hero.GetEffectiveMaxHealth()),
                    new HealthSource(hero, HealthCause.Form), Events);
            }
            SetHeroFrames(hero, hero.HeroSO != null ? hero.HeroSO.AnimationFrames : null,
                hero.HeroSO != null ? hero.HeroSO.AnimationFps : 4f);
            if (hero.HeroSO != null && (hero.HeroSO.AnimationFrames == null || hero.HeroSO.AnimationFrames.Length == 0))
            {
                var sr = hero.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = hero.HeroSO.Sprite;
                }
            }
        }

        /// <summary>Puts <paramref name="frames"/> on the hero's sprite (animated when there are
        /// several). Nothing to show keeps what is there.</summary>
        private static void SetHeroFrames(Hero hero, Sprite[] frames, float fps)
        {
            var valid = frames != null ? frames.Where(f => f != null).ToArray() : new Sprite[0];
            if (hero == null || valid.Length == 0)
            {
                return;
            }
            var animator = hero.GetComponent<SpriteAnimator>();
            if (animator == null)
            {
                animator = hero.gameObject.AddComponent<SpriteAnimator>();
            }
            animator.Initialize(valid, Mathf.Max(1f, fps));
        }
    }
}
