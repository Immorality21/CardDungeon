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
    /// <summary>The fight, by job: what a hero's command does: attack, cast, item, steal.</summary>
    public partial class CombatManager
    {
        /// <summary>
        /// The Mage's MultiCast: all his Focus is spent and the ability he picked is cast once plus once per
        /// point (<see cref="FocusOps.Casts"/>), each repeat aimed afresh at whoever still stands - a single
        /// target that fell passes to the weakest enemy. One charge pays for all of it, spent after the last,
        /// as an ordinary cast spends its one. Each repeat lays its tags and may set off a combo.
        /// </summary>
        private IEnumerator ExecuteMultiCast(SpellcastAction castAction, int slotIndex)
        {
            var caster = castAction.Caster;
            int focus = _focus != null ? _focus.Spend(caster) : 0;
            int casts = FocusOps.Casts(focus);
            if (casts < 2)
            {
                yield return ExecuteCastAction(castAction, slotIndex);
                yield break;
            }

            ShowFloatingLabel(caster.Transform.position + new Vector3(0f, 0.7f, 0f), Loc.Format("combat.float.multi-cast", ("cast", FocusOps.CastName(focus))),
                new Color(0.55f, 0.75f, 1f), 0.15f);
            for (int i = 0; i < casts; i++)
            {
                var repeat = new SpellcastAction
                {
                    Magic = castAction.Magic,
                    Caster = caster,
                    Targets = RepeatTargets(castAction),
                    ChargeSlot = castAction.ChargeSlot,
                    CastSlot = castAction.CastSlot
                };
                if (repeat.Targets.Count == 0)
                {
                    break;
                }
                yield return ExecuteCastAction(repeat, slotIndex, spendCharge: false);
            }
            // Free, and from an empty slot too (owner, 2026-10-31): Focus is the price, and every point of
            // it already cost a charge to build, so the Ultra was dry when it was finally ready.
            // Said over the Mage when there was a charge it could have cost (playtest 4 finding 17).
            if (SlotHasCharge(caster, slotIndex) && caster.Transform != null)
            {
                ShowFloatingLabel(caster.Transform.position + new Vector3(0f, 0.4f, 0f), Loc.Get("combat.float.charge-kept"),
                    new Color(0.55f, 0.75f, 1f), 0.14f);
            }
            SpendCharge(caster, slotIndex, free: true);
        }

        /// <summary>Whether <paramref name="caster"/>'s slot <paramref name="slotIndex"/> holds a charge a
        /// cast would spend (an unlimited form ability never does).</summary>
        private static bool SlotHasCharge(ICombatUnit caster, int slotIndex)
        {
            if (!(caster is Hero hero) || !DungeonManager.HasInstance || DungeonManager.Instance.MagicState == null)
            {
                return false;
            }
            var slots = DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey);
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Count || slots[slotIndex] == null)
            {
                return false;
            }
            return !slots[slotIndex].Unlimited && slots[slotIndex].Charges > 0;
        }

        private static bool AddFreeCast(ICombatUnit unit)
        {
            _freeCasts.Add(unit);
            return true;
        }

        /// <summary>Spends one charge from <paramref name="caster"/>'s slot <paramref name="slotIndex"/> -
        /// unless the cast is <paramref name="free"/> or a reaction made it so - and tells the game a hero
        /// used an ability.</summary>
        private static void SpendCharge(ICombatUnit caster, int slotIndex, bool free = false)
        {
            if (!(caster is Hero hero))
            {
                return;
            }
            var slots = DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null
                ? DungeonManager.Instance.MagicState.GetSlots(hero.HeroKey)
                : null;
            string abilityKey = slots != null && slotIndex >= 0 && slotIndex < slots.Count && slots[slotIndex]?.Magic != null
                ? slots[slotIndex].Magic.Key
                : null;
            if (!_freeCasts.Remove(caster) && !free && DungeonManager.HasInstance && DungeonManager.Instance.MagicState != null)
            {
                DungeonManager.Instance.MagicState.TryCast(hero.HeroKey, slotIndex);
            }
            GameEvents.Publish(new AbilityCast { HeroKey = hero.HeroKey, AbilityKey = abilityKey });
        }

        /// <summary>Who a MultiCast's next repeat lands on: the living of the first cast's targets, the
        /// enemies standing for an area cast, and the weakest enemy when a single target has fallen.</summary>
        private List<ICombatUnit> RepeatTargets(SpellcastAction first)
        {
            var magic = first.Magic;
            if (magic.TargetType == MagicTargetType.AllEnemies)
            {
                return GetTargetableEnemies();
            }
            var living = first.Targets != null ? first.Targets.Where(t => t != null && t.IsAlive).ToList() : new List<ICombatUnit>();
            if (living.Count == 0 && magic.TargetType == MagicTargetType.SingleEnemy)
            {
                var weakest = GetTargetableEnemies().OrderBy(e => e.Stats.Health).FirstOrDefault();
                if (weakest != null)
                {
                    living.Add(weakest);
                }
            }
            return living;
        }

        private IEnumerator ExecuteCastAction(SpellcastAction castAction, int slotIndex, bool spendCharge = true)
        {
            if (castAction.Magic != null && castAction.Magic.HasRandomHits)
            {
                // Rivet Storm: each shot picks its own enemy from whoever is still standing.
                for (int hit = 0; hit < castAction.Magic.RandomHits; hit++)
                {
                    var standing = GetTargetableEnemies();
                    if (standing.Count == 0)
                    {
                        break;
                    }
                    var shot = new SpellcastAction
                    {
                        Magic = castAction.Magic,
                        Caster = castAction.Caster,
                        Targets = new List<ICombatUnit> { standing[UnityEngine.Random.Range(0, standing.Count)] },
                        ChargeSlot = castAction.ChargeSlot,
                        CastSlot = castAction.CastSlot
                    };
                    yield return ResolveCast(shot);
                }
            }
            else
            {
                yield return ResolveCast(castAction);
            }

            if (spendCharge)
            {
                SpendCharge(castAction.Caster, slotIndex);
            }
        }

        /// <summary>One cast of a hero's ability on its targets: effects, tags and combos, presentation, deaths.
        /// The charge is the caller's (<see cref="ExecuteCastAction"/>), so a many-shot ability pays once.</summary>
        private IEnumerator ResolveCast(SpellcastAction castAction)
        {
            // Use Instance (auto-creates + loads Meta.json) rather than HasInstance: the
            // manager may not exist yet mid-combat, and we must still apply combo upgrades and
            // record combo discovery. Abilities themselves have no upgrade level since the Forge
            // stopped selling ability upgrades (2026-10-06, docs/plans/HUB.md §3c): an ability
            // grows on the sphere grid, so it always resolves at power bonus 0, level 0.
            var meta = MetaProgressManager.Instance;
            Func<string, int> comboLevelLookup = meta.GetComboUpgradeLevel;

            // The plates keep showing the health from before the cast until each hit lands.
            HoldHealthDisplay();
            var result = _calculator.Execute(
                castAction, BuffTracker, _tagTracker, _comboDetector, 0, 0, comboLevelLookup);
            // A weapon ability swings; a spell casts (playtest 2026-10-30: a physical ability sounded like magic).
            CombatAudio.Play(castAction.Magic != null && castAction.Magic.IsPhysicalAttack() ? CombatSound.MeleeSwing : CombatSound.MagicCast);

            // Record any triggered combos as discovered (permanent, survives death).
            foreach (var comboKey in result.TriggeredComboKeys)
            {
                meta.MarkComboDiscovered(comboKey);
            }

            // ...and what the cast taught the player about the enemies it hit. Read off the magic's
            // live Damage effects rather than the result entries, because an entry carries the
            // number and the popup word but not the element that produced them.
            RecordCastDamageObserved(castAction);

            yield return _presenter.Present(
                result,
                castAction.Caster,
                castAction.Magic
            );
            HealthDisplay.ReleaseAll();

            // Combo flourish: a camera punch + brief hit-stop so a triggered combo lands with weight
            // (the combo name already floats up in orange from the resolver).
            if (!string.IsNullOrEmpty(result.ComboName))
            {
                CombatFeedback.Instance.Shake(0.2f, 0.28f);
                yield return new WaitForSecondsRealtime(0.12f);
            }

            ResolveDeaths();
        }

        private IEnumerator ExecuteUseItemAction(ICombatUnit heroUnit, ItemSO item, ICombatUnit target)
        {
            if (item == null || item.Category != ItemCategory.Consumable)
            {
                yield break;
            }

            // Default a missing/fallen target to the user themselves.
            if (target == null || !target.IsAlive)
            {
                target = heroUnit;
            }

            // Spend the item first; if the party isn't actually carrying it, the turn is a no-op.
            if (!InventoryManager.Instance.TryConsume(item.Key))
            {
                yield break;
            }

            switch (item.ConsumableEffect)
            {
                case ConsumableEffectType.RestoreHealth:
                    DrinkHeal(heroUnit, item, target);
                    break;
                case ConsumableEffectType.RestoreCharge:
                    CombatAudio.Play(CombatSound.ItemUse);
                    DrinkCharges(item, target);
                    break;
                case ConsumableEffectType.HealAndCharge:
                    DrinkHeal(heroUnit, item, target);
                    DrinkCharges(item, target);
                    break;
                case ConsumableEffectType.CureStatus:
                    var cured = BuffTracker.CureStatusEffects(target);
                    CombatAudio.Play(CombatSound.ItemUse);
                    if (cured.Count > 0)
                    {
                        // The item is spent either way — it was already consumed above, and a cure
                        // that refunded itself on a clean target would be a free "is anyone
                        // poisoned?" probe.
                        ShowFloatingLabel(target.Transform.position, Loc.Get("combat.float.cured"), Color.green);
                    }
                    break;
            }

            Events?.Publish(new ItemUsed { User = heroUnit, Target = target, Item = item });
            yield return new WaitForSeconds(_turnDelay);
        }

        /// <summary>A drunk potion's heal. Scales off the TARGET's bar - a potion is worth what it is
        /// worth to whoever drinks it; ItemSO owns the arithmetic so the balance model agrees.</summary>
        private void DrinkHeal(ICombatUnit user, ItemSO item, ICombatUnit target)
        {
            int healed = HealthOps.Heal(target, PotionUse.HealAmount(item, target),
                new HealthSource(user, HealthCause.Item), Events).Healed;
            CombatAudio.Play(CombatSound.ItemUse);
            CombatAudio.Play(CombatSound.Heal);
            ShowDamageText(target.Transform.position, healed, Color.green);
        }

        /// <summary>
        /// A drunk potion's charges, back to the target's most-spent ability through the same bank
        /// Life Tap uses.
        /// </summary>
        private void DrinkCharges(ItemSO item, ICombatUnit target)
        {
            int charges = PotionUse.ChargesOf(item);
            var bank = _calculator != null ? _calculator.Charges : null;
            if (bank == null || !bank.Carries(target))
            {
                return;
            }
            var restored = bank.Restore(target, SpellcastAction.AnyChargeSlot, charges, -1);
            if (restored.Added <= 0 || restored.Magic == null)
            {
                return;
            }
            ShowFloatingLabel(target.Transform.position,
                Loc.Format("combat.float.item-charges", ("count", restored.Added), ("ability", restored.Magic.DisplayName)), new Color(0.55f, 0.75f, 1f));
        }

        private IEnumerator ExecuteHeroTurn(ICombatUnit hero, Room room)
        {
            // Use the player's chosen target; fall back to a random enemy if it's
            // missing or already dead (e.g. single-enemy auto-target edge cases).
            ICombatUnit target = _pendingAttackTarget;
            if (target == null || !target.IsAlive)
            {
                target = GetRandomAliveEnemy(room);
            }

            if (target == null)
            {
                yield break;
            }

            yield return ExecuteAttack(hero, target, Vector3.right, Color.white);
            ResolveDeaths();
        }

        private IEnumerator ExecuteAttack(ICombatUnit attacker, ICombatUnit target, Vector3 lungeDirection, Color damageColor, float damageMultiplier = 1f, string verbKey = "combat.verb.attacks")
        {
            // A guarded unit's blows land on its guard - the mech under its rider (GuardTable) - and
            // the guard's own Endurance, Luck and resistances answer them.
            if (Events != null)
            {
                var aimed = target;
                target = Events.Guards.Redirect(target);
                // An enemy's cover says so: the player picked one target and another took the blow.
                if (!ReferenceEquals(aimed, target) && Events.Guards.IsCovered(aimed) && target.Transform != null)
                {
                    ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Get("combat.float.cover"),
                        new Color(0.75f, 0.85f, 1f), 0.14f);
                }
            }
            // A ranged hero (the Mage, the Ranger) shoots the blow across the stage instead of stepping in.
            var ranged = attacker is Hero shooter && shooter.HeroSO != null && shooter.HeroSO.RangedAttack ? shooter.HeroSO : null;
            if (ranged != null && attacker.Transform != null && target.Transform != null)
            {
                CombatAudio.Play(CombatSound.MagicCast, 0.7f);
                yield return ShootAnimation(attacker.Transform.position, target.Transform.position, ranged);
            }
            else
            {
                CombatAudio.Play(CombatSound.MeleeSwing);
                yield return LungeAnimation(attacker.Transform, lungeDirection);
            }

            // A basic attack is always physical: a blinded attacker may miss it outright, and then
            // the target's Luck may dodge it (DefenseRules).
            if (DefenseRules.RollMiss(BlindedBuffHandler.IsBlinded(BuffTracker, attacker)))
            {
                Events?.Publish(new HitAvoided { Attacker = attacker, Target = target, Dodged = false });
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Get("combat.float.miss"), new Color(0.8f, 0.8f, 0.85f), 0.16f);
                RecordAttackTypeObserved(attacker);
                yield return new WaitForSecondsRealtime(0.045f);
                yield break;
            }
            // Shadowstep / Smoke and Mirrors: a held evasion is spent before Luck gets its roll.
            if (EvasiveBuffHandler.TryEvade(BuffTracker, target))
            {
                Events?.Publish(new HitAvoided { Attacker = attacker, Target = target, Dodged = true });
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Get("combat.float.evade"), new Color(0.8f, 0.8f, 0.85f), 0.16f);
                RecordAttackTypeObserved(attacker);
                yield return new WaitForSecondsRealtime(0.045f);
                yield break;
            }
            if (DefenseRules.RollDodge(target))
            {
                Events?.Publish(new HitAvoided { Attacker = attacker, Target = target, Dodged = true });
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Get("combat.float.dodge"), new Color(0.8f, 0.8f, 0.85f), 0.16f);
                RecordAttackTypeObserved(attacker);
                yield return new WaitForSecondsRealtime(0.045f);
                yield break;
            }

            // Buff the stat this attacker actually swings with, not Strength unconditionally.
            int attackBonus = BuffTracker.GetBuffAmount(attacker, attacker.AttackStat);
            int defenseBonus = BuffTracker.GetBuffAmount(target, StatType.Endurance);
            // A summon's SummonDamage nodes (OutgoingDamage.AllPercent) grow its Attack too.
            float outgoing = attacker is IOutgoingDamage dealer && dealer.OutgoingDamage != null
                ? dealer.OutgoingDamage.AttackFactor
                : 1f;
            int rawAttack = Mathf.RoundToInt((attacker.GetEffectiveAttackPower() + attackBonus) * damageMultiplier * outgoing);
            int defense = target.GetEffectiveStat(StatType.Endurance) + defenseBonus;

            // Physical attacks carry the attacker's element, so elemental resistance applies to them too.
            // Normal is the default on both sides and bypasses the elemental layer entirely.
            var damageType = attacker.AttackDamageType;
            float resistanceBonus = BuffTracker.GetResistanceBonus(target, damageType);
            int dmg = DamageCalculator.Calculate(
                rawAttack, defense, damageType, target.Resistances, resistanceBonus);

            // Critical hit: a chance for a harder blow, called out with a gold popup + bigger number.
            // Death Mark: every hit on the marked unit crits, harder than usual.
            bool deathMarked = DeathMarkedBuffHandler.IsMarked(BuffTracker, target);
            bool crit = dmg > 0 && (deathMarked || CritRules.RollCrit(attacker));
            if (crit)
            {
                dmg = CritRules.Apply(dmg, deathMarked);
            }

            // Absorbed (resistance above 100%) heals instead, clamped to the target's maximum - without
            // the clamp an absorbing unit heals past full and the popup reads "-7".
            var hit = HealthOps.Damage(target, dmg, new HealthSource(attacker, HealthCause.Attack, damageType, crit), Events);
            // Divine protection turned the whole blow away.
            if (hit.Negated > 0)
            {
                CombatAudio.Play(CombatSound.Impact, 0.6f);
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Get("combat.float.protected"), new Color(1f, 0.85f, 0.35f), 0.16f);
                RecordAttackTypeObserved(attacker);
                yield return new WaitForSecondsRealtime(0.045f);
                yield break;
            }
            if (dmg < 0)
            {
                dmg = -hit.Healed;
            }
            else if (hit.Absorbed > 0)
            {
                // A barrier soaked part of the blow: the number shows what reached the bar.
                dmg -= hit.Absorbed;
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.3f, 0f), Loc.Format("combat.float.absorbed", ("amount", hit.Absorbed)), new Color(0.6f, 0.85f, 1f), 0.13f);
            }

            // Impact juice: flash + damage-scaled shake (extra punch on heavy/crit blows) + hit-stop.
            float punch = (damageMultiplier > 1f ? 1.6f : 1f) * (crit ? 1.4f : 1f);
            CombatFeedback.Instance.PlayImpact(target, Mathf.Abs(dmg), punch);
            CombatAudio.Play(CombatSound.Impact, damageMultiplier > 1f ? 1f : 0.9f);
            ShowDamageText(
                target.Transform.position,
                Mathf.Abs(dmg),
                dmg < 0 ? Color.green : damageColor,
                crit ? 0.24f : 0.15f);
            if (crit)
            {
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.45f, 0f), Loc.Get("combat.float.crit"), new Color(1f, 0.82f, 0.2f), 0.16f);
            }
            ShowEffectiveness(target, damageType, resistanceBonus);

            // The player just watched this land, so the bestiary learns it: which element was tried
            // on the enemy, and - when the enemy is the one swinging - what it attacks with.
            RecordDamageObserved(target, damageType);
            RecordAttackTypeObserved(attacker);

            yield return new WaitForSecondsRealtime(crit ? 0.075f : 0.045f);

        }

        /// <summary>
        /// A ranged hero's basic Attack: the projectile flies from the hero to the target on a shallow
        /// arc, turned head first, and the hit lands as it arrives. Shorter and flatter than an
        /// ability's bolt, so a basic attack stays as quick as the lunge it replaces.
        /// </summary>
        private IEnumerator ShootAnimation(Vector3 from, Vector3 to, HeroSO hero)
        {
            var go = new GameObject("AttackProjectile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = hero.AttackProjectile != null ? hero.AttackProjectile : CombatIcons.Get("burst");
            sr.color = hero.AttackProjectileColor;
            sr.sortingOrder = 850;
            go.transform.localScale = Vector3.one * 0.35f;
            go.transform.position = from;

            Vector3 control = (from + to) * 0.5f + Vector3.up * 0.4f;
            const float duration = 0.28f;
            float elapsed = 0f;
            Vector3 previous = from;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                Vector3 position = Vector3.Lerp(Vector3.Lerp(from, control, p), Vector3.Lerp(control, to, p), p);
                Vector2 direction = position - previous;
                if (direction.sqrMagnitude > 0.000001f)
                {
                    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    go.transform.rotation = Quaternion.Euler(0f, 0f, angle - hero.AttackProjectileArtAngle);
                }
                go.transform.position = position;
                previous = position;
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator LungeAnimation(Transform unit, Vector3 direction)
        {
            var startPos = unit.position;
            var lungePos = startPos + direction * _lungeDistance;

            // Lunge forward
            float elapsed = 0f;
            while (elapsed < _lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _lungeDuration;
                unit.position = Vector3.Lerp(startPos, lungePos, t);
                yield return null;
            }

            // Snap back
            elapsed = 0f;
            while (elapsed < _lungeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _lungeDuration;
                unit.position = Vector3.Lerp(lungePos, startPos, t);
                yield return null;
            }

            unit.position = startPos;
        }

        /// <summary>
        /// The Rogue's Steal, once the odds are won (<see cref="StealEffectExecutor"/>): one random line of
        /// the enemy's drop table goes into the bag at its smallest quantity, or, when it has none, half its
        /// kill gold - Fence's bonus included either way. Once per enemy per fight; a called-in body has
        /// nothing on it. A revisit's withheld materials are skipped as they are on a kill.
        /// </summary>
        private string StealFrom(ICombatUnit thief, ICombatUnit mark)
        {
            var enemy = mark as Enemy;
            if (enemy == null || enemy.IsSummoned)
            {
                return Loc.Get("combat.float.nothing-to-steal");
            }
            if (_robbed.Contains(mark))
            {
                return Loc.Get("combat.float.already-robbed");
            }
            _robbed.Add(mark);

            var lines = enemy.LootTable
                .Where(d => d != null && d.Item != null && !RunFear.Current.Withholds(d.Item)
                            && !InventoryManager.Instance.AlreadyKnown(d.Item))
                .ToList();
            if (lines.Count > 0)
            {
                var line = lines[UnityEngine.Random.Range(0, lines.Count)];
                var award = new LootAward(line.Item, Mathf.Max(1, line.MinQuantity));
                InventoryManager.Instance.AddItem(award, EconomySource.Kill);
                _combatLoot.Add(award);
                RecordLootObserved(enemy, award.Item);
                return Loc.Format("combat.float.stole-item", ("item", award.Item.DisplayName));
            }

            int gold = StealOps.WithBonus(StealOps.FallbackGold(enemy.GoldReward), _fightGoldBonus);
            _combatGold += gold;
            MetaProgressManager.Instance.AddPendingGold(gold, EconomySource.Kill);
            return Loc.Format("combat.float.stole-gold", ("gold", gold));
        }
    }
}
