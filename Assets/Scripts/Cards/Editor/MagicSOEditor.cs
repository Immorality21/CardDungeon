using Assets.Scripts.Cards;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MagicSO))]
public class MagicSOEditor : Editor
{
    private ReorderableList _effectsList;

    private void OnEnable()
    {
        var effectsProp = serializedObject.FindProperty("Effects");
        _effectsList = new ReorderableList(serializedObject, effectsProp, true, true, true, true);

        _effectsList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Effects");
        };

        _effectsList.elementHeightCallback = index =>
        {
            var effectType = (SpellEffectType)effectsProp.GetArrayElementAtIndex(index).FindPropertyRelative("EffectType").enumValueIndex;
            return LinesFor(effectType) * (EditorGUIUtility.singleLineHeight + 2) + 4;
        };

        _effectsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = effectsProp.GetArrayElementAtIndex(index);
            rect.y += 2;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2;

            var effectTypeProp = element.FindPropertyRelative("EffectType");
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                effectTypeProp);
            rect.y += lineHeight;

            var effectType = (SpellEffectType)effectTypeProp.enumValueIndex;

            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("Power"));
            rect.y += lineHeight;

            // How Power is read. Sits directly under Power because it changes what the number
            // means: 10 is 10 damage in BasePower/Flat and 10% of a health bar in PercentOfMaxHealth.
            // A buff reads it too: PercentOfTargetStat for a stat buff, PercentOfMaxHealth for a barrier.
            if (ShowsPowerMode(effectType))
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("PowerMode"),
                    new GUIContent("Power Mode", "BasePower adds the caster's Scaling Stat. Flat uses "
                        + "Power as authored. PercentOfMaxHealth reads Power as a percentage of the "
                        + "max health of the unit the effect lands on (the caster, for a health cost), "
                        + "rounded down with a floor of 1, and takes no upgrade bonus. A Barrier buff "
                        + "honours it too; a stat buff honours PercentOfTargetStat."));
                rect.y += lineHeight;
            }

            // Which caster stat is added to Power. Sits next to Power because the two are read
            // together: Power is the floor, the stat is what a good caster brings. A health cost has
            // no caster contribution at all, so the field would be a lie there; nor does a turn delay,
            // a drain, a charge restore, a revive, a cure or an execute (all percentages or counts).
            if (ShowsScalingStat(effectType))
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("ScalingStat"),
                    new GUIContent("Scaling Stat", "Caster stat added to Power. Damage/heals add it in "
                        + "full; buffs and debuffs add a quarter of it, since their Power is a flat stat "
                        + "delta and full scaling would dwarf the stat being buffed."));
                rect.y += lineHeight;
            }

            if (effectType == SpellEffectType.Damage)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("DamageType"));
                rect.y += lineHeight;

                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("BonusVsTrait"),
                    new GUIContent("Bonus Vs", "A kind of unit this hits harder (Purge on the Undead). None = no bonus."));
                rect.y += lineHeight;

                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("BonusPercent"),
                    new GUIContent("Bonus %", "Extra raw damage against Bonus Vs, in percent: 100 doubles it."));
                rect.y += lineHeight;
            }
            else if (effectType == SpellEffectType.Retaliate)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("DamageType"),
                    new GUIContent("Strike Element", "The element of the counterstrike."));
                rect.y += lineHeight;
            }
            else if (effectType == SpellEffectType.Execute)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("BonusVsTrait"),
                    new GUIContent("Only Destroys", "The kind of unit this destroys at or under Power % health. "
                        + "None = any unit. Bosses never are."));
                rect.y += lineHeight;
            }
            else if (effectType == SpellEffectType.Buff || effectType == SpellEffectType.Debuff)
            {
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("BuffType"));
                rect.y += lineHeight;

                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("Duration"));
                rect.y += lineHeight;

                if (effectType == SpellEffectType.Debuff)
                {
                    EditorGUI.PropertyField(
                        new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                        element.FindPropertyRelative("ApplyChance"),
                        new GUIContent("Apply Chance %", "Percent chance this debuff takes hold on each "
                            + "target (AfflictionRules). 100 always lands."));
                    rect.y += lineHeight;
                }
            }

            // Upgrade level at which this effect unlocks (0 = always active).
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                element.FindPropertyRelative("UnlockLevel"));
        };
    }

    /// <summary>Whether an effect of this type reads <c>PowerMode</c>.</summary>
    private static bool ShowsPowerMode(SpellEffectType type)
    {
        return type == SpellEffectType.Damage || type == SpellEffectType.Heal
               || type == SpellEffectType.HealthCost || type == SpellEffectType.Buff;
    }

    /// <summary>Whether an effect of this type adds a caster stat to its Power.</summary>
    private static bool ShowsScalingStat(SpellEffectType type)
    {
        switch (type)
        {
            case SpellEffectType.HealthCost:
            case SpellEffectType.TurnDelay:
            case SpellEffectType.Drain:
            case SpellEffectType.RestoreCharge:
            case SpellEffectType.Disassemble:
            case SpellEffectType.Revive:
            case SpellEffectType.Cure:
            case SpellEffectType.Execute:
            case SpellEffectType.Intercept:
                return false;
            default:
                return true;
        }
    }

    /// <summary>How many rows the drawer gives an effect - counted from the same rules it draws by,
    /// so the height can never disagree with what is drawn.</summary>
    private static int LinesFor(SpellEffectType type)
    {
        int lines = 3; // EffectType + Power + UnlockLevel
        if (ShowsPowerMode(type))
        {
            lines++;
        }
        if (ShowsScalingStat(type))
        {
            lines++;
        }
        if (type == SpellEffectType.Damage)
        {
            lines += 3; // DamageType + BonusVsTrait + BonusPercent
        }
        else if (type == SpellEffectType.Execute || type == SpellEffectType.Retaliate)
        {
            lines += 1; // BonusVsTrait / DamageType
        }
        else if (type == SpellEffectType.Buff)
        {
            lines += 2; // BuffType + Duration
        }
        else if (type == SpellEffectType.Debuff)
        {
            lines += 3; // BuffType + Duration + ApplyChance
        }
        return lines;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Auto-generate a stable Key from the asset's name, then show it read-only.
        // Save data + catalog lookups key off this, so it must never change once set — it is
        // generated only while empty, and renaming the asset later does not touch it.
        var keyProp = serializedObject.FindProperty("Key");
        if (string.IsNullOrEmpty(keyProp.stringValue))
        {
            keyProp.stringValue = $"{target.name}-{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(keyProp, new GUIContent("Key (auto)"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("DisplayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Icon"));
        var deliveryProp = serializedObject.FindProperty("Delivery");
        EditorGUILayout.PropertyField(deliveryProp);
        if (deliveryProp.enumValueIndex == (int)MagicDelivery.Projectile)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ProjectileArtAngle"));
        }
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Rarity"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("CanCrit"),
            new GUIContent("Can Crit", "Its damage can be a critical hit at the caster's crit chance. "
                + "A grid's MagicUpgrade node (CritChance) can grant this per hero instead."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("EnemyOnly"),
            new GUIContent("Enemy Only", "An enemy's own ability: never on a grid, never in the "
                + "MagicCatalog. EnemyAbilityTests enforces both."));

        EditorGUILayout.Space(8);
        _effectsList.DoLayoutList();

        EditorGUILayout.Space(8);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Tags"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TagDuration"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Threat", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ThreatMultiplier"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("BonusThreat"));

        serializedObject.ApplyModifiedProperties();
    }
}
