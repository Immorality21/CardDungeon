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
    /// <summary>The fight, by job: numbers and labels floating over the fight.</summary>
    public partial class CombatManager
    {
        private void ShowDamageText(Vector3 position, int damage, Color color, float scale = 0.15f)
        {
            ShowFloatingLabel(position, damage.ToString(), color, scale);
        }

        /// <summary>Popup for a resistance outcome (Weak!/Resisted/Immune/Absorbed); no-op if Normal.</summary>
        private void ShowEffectiveness(ICombatUnit target, DamageType type, float resistanceBonus = 0f)
        {
            if (target == null || target.Transform == null)
            {
                return;
            }

            string word = null;
            Color color = Color.white;
            switch (DamageCalculator.Classify(type, target.Resistances, resistanceBonus))
            {
                case DamageEffectiveness.Weak:
                    word = Loc.Get("combat.float.weak"); color = new Color(1f, 0.85f, 0.2f); break;
                case DamageEffectiveness.Resisted:
                    word = Loc.Get("combat.float.resisted"); color = new Color(0.6f, 0.7f, 0.85f); break;
                case DamageEffectiveness.Immune:
                    word = Loc.Get("combat.float.immune"); color = new Color(0.78f, 0.78f, 0.82f); break;
                case DamageEffectiveness.Absorbed:
                    word = Loc.Get("combat.float.absorbed-effect"); color = new Color(0.4f, 0.95f, 0.5f); break;
            }

            if (word != null)
            {
                ShowFloatingLabel(target.Transform.position + new Vector3(0f, 0.45f, 0f), word, color, 0.13f);
            }
        }

        /// <summary>A Momentum stack built or lost: "Momentum 3" over the hero, or "Momentum lost".</summary>
        private void ShowMomentum(ICombatUnit unit, int stacks)
        {
            if (unit?.Transform == null)
            {
                return;
            }
            string text = stacks > 0 ? Loc.Format("combat.float.momentum", ("stacks", stacks)) : Loc.Get("combat.float.momentum-lost");
            var color = stacks > 0 ? new Color(1f, 0.6f, 0.2f) : new Color(0.7f, 0.7f, 0.7f);
            ShowFloatingLabel(unit.Transform.position + new Vector3(0f, 0.7f, 0f), text, color, 0.13f);
        }

        /// <summary>Focus built or spent: "Focus 2" over the Mage.</summary>
        private void ShowFocus(ICombatUnit unit, int focus)
        {
            if (unit?.Transform == null || focus <= 0)
            {
                return;
            }
            ShowFloatingLabel(unit.Transform.position + new Vector3(0f, 0.7f, 0f), Loc.Format("combat.float.focus", ("focus", focus)),
                new Color(0.55f, 0.75f, 1f), 0.13f);
        }

        private void ShowFloatingLabel(Vector3 position, string text, Color color, float scale = 0.15f)
        {
            if (FloatingTextHandler.HasInstance)
            {
                FloatingTextHandler.Instance.CreateFloatingText(
                    position,
                    text,
                    color,
                    1f,     // fadeSpeed — fade out over ~1 second
                    0.8f,   // fadeRange — gentle drift
                    scale,
                    TextFadeMode.FadeUp);
            }
        }
    }
}
