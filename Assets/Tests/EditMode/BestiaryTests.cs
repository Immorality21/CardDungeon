using System.Collections.Generic;
using Assets.Scripts.Combat;
using Assets.Scripts.Enemies;
using Assets.Scripts.Items;
using Assets.Scripts.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// The enemy knowledge record and how it reads back.
    ///
    /// <para>The rule the whole feature rests on is <b>nothing is shown that was not observed</b>:
    /// a resistance stays "???" until a hit of that element has actually landed. These tests pin
    /// that in both directions - unobserved stays hidden, and observing exactly one element does not
    /// leak the rest.</para>
    /// </summary>
    public class BestiaryTests
    {
        private List<BestiaryEntry> _entries;

        [SetUp]
        public void SetUp()
        {
            _entries = new List<BestiaryEntry>();
        }

        // ============================================================
        //  BestiaryOps
        // ============================================================

        [Test]
        public void MarkSeen_FirstTime_CreatesTheEntry()
        {
            Assert.IsTrue(BestiaryOps.MarkSeen(_entries, "Dragon"));
            Assert.IsNotNull(BestiaryOps.Find(_entries, "Dragon"));
        }

        [Test]
        public void MarkSeen_Again_ReportsNoChange()
        {
            BestiaryOps.MarkSeen(_entries, "Dragon");

            // The manager persists only on a change, so "already known" must not report one -
            // otherwise every hit in a fight writes Meta.json.
            Assert.IsFalse(BestiaryOps.MarkSeen(_entries, "Dragon"));
            Assert.AreEqual(1, _entries.Count);
        }

        [Test]
        public void MarkSeen_WithNoKey_DoesNothing()
        {
            Assert.IsFalse(BestiaryOps.MarkSeen(_entries, null));
            Assert.IsFalse(BestiaryOps.MarkSeen(_entries, string.Empty));
            CollectionAssert.IsEmpty(_entries);
        }

        [Test]
        public void MarkDamageTypeObserved_RecordsOnlyThatType()
        {
            BestiaryOps.MarkDamageTypeObserved(_entries, "Dragon", DamageType.Ice);
            var entry = BestiaryOps.Find(_entries, "Dragon");

            Assert.IsTrue(BestiaryOps.KnowsDamageType(entry, DamageType.Ice));
            Assert.IsFalse(BestiaryOps.KnowsDamageType(entry, DamageType.Fire));
        }

        [Test]
        public void MarkDamageTypeObserved_Twice_ReportsNoChange()
        {
            Assert.IsTrue(BestiaryOps.MarkDamageTypeObserved(_entries, "Dragon", DamageType.Ice));
            Assert.IsFalse(BestiaryOps.MarkDamageTypeObserved(_entries, "Dragon", DamageType.Ice));
        }

        [Test]
        public void MarkDamageTypeObserved_OnAnUnmetEnemy_AlsoMarksItSeen()
        {
            // A hit is a meeting. Without this the first thing a player learns about an enemy could
            // be filed against an entry that says it has never been encountered.
            BestiaryOps.MarkDamageTypeObserved(_entries, "Dragon", DamageType.Fire);
            Assert.IsNotNull(BestiaryOps.Find(_entries, "Dragon"));
        }

        [Test]
        public void MarkAttackTypeObserved_IsIdempotent()
        {
            Assert.IsTrue(BestiaryOps.MarkAttackTypeObserved(_entries, "Dragon"));
            Assert.IsFalse(BestiaryOps.MarkAttackTypeObserved(_entries, "Dragon"));
            Assert.IsTrue(BestiaryOps.Find(_entries, "Dragon").AttackTypeKnown);
        }

        [Test]
        public void MarkKilled_Accumulates()
        {
            BestiaryOps.MarkKilled(_entries, "Dragon");
            BestiaryOps.MarkKilled(_entries, "Dragon");
            BestiaryOps.MarkKilled(_entries, "Dragon");

            Assert.AreEqual(3, BestiaryOps.Find(_entries, "Dragon").Kills);
        }

        [Test]
        public void MarkLootObserved_RecordsOnlyThatItem()
        {
            BestiaryOps.MarkLootObserved(_entries, "Dragon", "IronSword");
            var entry = BestiaryOps.Find(_entries, "Dragon");

            Assert.IsTrue(BestiaryOps.KnowsLoot(entry, "IronSword"));
            Assert.IsFalse(BestiaryOps.KnowsLoot(entry, "SteelSword"));
        }

        [Test]
        public void KnowsDamageType_OnAnUnmetEnemy_IsFalse()
        {
            Assert.IsFalse(BestiaryOps.KnowsDamageType(null, DamageType.Fire));
            Assert.IsFalse(BestiaryOps.KnowsLoot(null, "IronSword"));
        }

        // ============================================================
        //  BestiaryPresenter
        // ============================================================

        private static EnemySO MakeEnemy(params Resistance[] resistances)
        {
            var enemy = ScriptableObject.CreateInstance<EnemySO>();
            enemy.Key = "TestEnemy";
            enemy.DisplayName = "Test Enemy";
            enemy.Resistances = new List<Resistance>(resistances);
            return enemy;
        }

        private static Resistance Resist(DamageType type, float percent)
        {
            return new Resistance { DamageType = type, Percent = percent };
        }

        [Test]
        public void ResistanceLine_Unobserved_ReadsUnknown()
        {
            var enemy = MakeEnemy(Resist(DamageType.Fire, 120f));
            var line = BestiaryPresenter.ResistanceLine(enemy, null, DamageType.Fire);

            Assert.AreEqual(BestiaryPresenter.Unknown, line.Value);
            Assert.AreEqual(BestiaryTone.Unknown, line.Tone);
            Assert.IsFalse(line.IsKnown);
        }

        [Test]
        public void ResistanceLine_ObservingOneElement_DoesNotRevealAnother()
        {
            var enemy = MakeEnemy(Resist(DamageType.Fire, 120f), Resist(DamageType.Ice, -50f));
            BestiaryOps.MarkDamageTypeObserved(_entries, enemy.SaveKey, DamageType.Ice);
            var known = BestiaryOps.Find(_entries, enemy.SaveKey);

            Assert.AreEqual("Weak -50%",
                BestiaryPresenter.ResistanceLine(enemy, known, DamageType.Ice).Value);
            Assert.AreEqual(BestiaryPresenter.Unknown,
                BestiaryPresenter.ResistanceLine(enemy, known, DamageType.Fire).Value);
        }

        [Test]
        public void ResistanceLine_Absorbing_SaysSoAndReadsAsBad()
        {
            var enemy = MakeEnemy(Resist(DamageType.Fire, 120f));
            BestiaryOps.MarkDamageTypeObserved(_entries, enemy.SaveKey, DamageType.Fire);
            var line = BestiaryPresenter.ResistanceLine(
                enemy, BestiaryOps.Find(_entries, enemy.SaveKey), DamageType.Fire);

            Assert.AreEqual("Absorbs 120%", line.Value);
            Assert.AreEqual(BestiaryTone.Bad, line.Tone);
        }

        [Test]
        public void ResistanceLine_Weakness_ReadsAsGood()
        {
            var enemy = MakeEnemy(Resist(DamageType.Ice, -50f));
            BestiaryOps.MarkDamageTypeObserved(_entries, enemy.SaveKey, DamageType.Ice);
            var line = BestiaryPresenter.ResistanceLine(
                enemy, BestiaryOps.Find(_entries, enemy.SaveKey), DamageType.Ice);

            Assert.AreEqual(BestiaryTone.Good, line.Tone);
        }

        [Test]
        public void ResistanceLine_ObservedAndNeutral_IsAKnownAnswer()
        {
            // Recording only non-Normal classifications - which the original plan proposed - would
            // leave this permanently "???" no matter how often the player tried Lightning on it.
            var enemy = MakeEnemy(Resist(DamageType.Fire, 50f));
            BestiaryOps.MarkDamageTypeObserved(_entries, enemy.SaveKey, DamageType.Lightning);
            var line = BestiaryPresenter.ResistanceLine(
                enemy, BestiaryOps.Find(_entries, enemy.SaveKey), DamageType.Lightning);

            Assert.AreNotEqual(BestiaryPresenter.Unknown, line.Value);
            Assert.AreEqual(BestiaryTone.Neutral, line.Tone);
        }

        [Test]
        public void ResistanceLine_Immune_SaysImmuneWithoutANumber()
        {
            var enemy = MakeEnemy(Resist(DamageType.Holy, 100f));
            BestiaryOps.MarkDamageTypeObserved(_entries, enemy.SaveKey, DamageType.Holy);
            var line = BestiaryPresenter.ResistanceLine(
                enemy, BestiaryOps.Find(_entries, enemy.SaveKey), DamageType.Holy);

            Assert.AreEqual("Immune", line.Value);
        }

        [Test]
        public void ResistanceLines_CoverEveryDamageType()
        {
            var enemy = MakeEnemy();
            var lines = BestiaryPresenter.ResistanceLines(enemy, null);

            Assert.AreEqual(BestiaryPresenter.DisplayedTypes.Length, lines.Count);
            Assert.AreEqual("Physical", lines[0].Label, "Normal reads as Physical on the page.");
        }

        [Test]
        public void AttackLine_IsHiddenUntilTheEnemyHasBeenSeenAttacking()
        {
            var enemy = MakeEnemy();
            enemy.AttackDamageType = DamageType.Fire;

            // Met, and hit with Ice - but never watched it swing.
            BestiaryOps.MarkDamageTypeObserved(_entries, enemy.SaveKey, DamageType.Ice);
            var known = BestiaryOps.Find(_entries, enemy.SaveKey);
            Assert.AreEqual(BestiaryPresenter.Unknown, BestiaryPresenter.AttackLine(enemy, known).Value);

            BestiaryOps.MarkAttackTypeObserved(_entries, enemy.SaveKey);
            Assert.AreEqual("Fire", BestiaryPresenter.AttackLine(enemy, known).Value);
        }

        [Test]
        public void LootLine_IsHiddenUntilTheDropHasBeenSeen()
        {
            var loot = ScriptableObject.CreateInstance<ItemSO>();
            loot.Key = "IronSword";
            loot.DisplayName = "Iron Sword";

            var enemy = MakeEnemy();
            enemy.LootItem = loot;
            BestiaryOps.MarkSeen(_entries, enemy.SaveKey);
            var known = BestiaryOps.Find(_entries, enemy.SaveKey);

            Assert.AreEqual(BestiaryPresenter.Unknown, BestiaryPresenter.LootLine(enemy, known).Value);

            BestiaryOps.MarkLootObserved(_entries, enemy.SaveKey, loot.Key);
            Assert.AreEqual("Iron Sword", BestiaryPresenter.LootLine(enemy, known).Value);
        }

        [Test]
        public void LootLine_ForAnEnemyThatCarriesNothing_SaysSoOnceMet()
        {
            var enemy = MakeEnemy();
            BestiaryOps.MarkSeen(_entries, enemy.SaveKey);

            Assert.AreEqual("Nothing",
                BestiaryPresenter.LootLine(enemy, BestiaryOps.Find(_entries, enemy.SaveKey)).Value);
            Assert.AreEqual(BestiaryPresenter.Unknown,
                BestiaryPresenter.LootLine(enemy, null).Value,
                "An enemy never met reveals nothing at all, not even that it is empty-handed.");
        }

        [Test]
        public void KillsLine_IsUnknownUntilMet_ThenCountsFromZero()
        {
            Assert.AreEqual(BestiaryPresenter.Unknown, BestiaryPresenter.KillsLine(null).Value);

            BestiaryOps.MarkSeen(_entries, "Dragon");
            Assert.AreEqual("0", BestiaryPresenter.KillsLine(BestiaryOps.Find(_entries, "Dragon")).Value);

            BestiaryOps.MarkKilled(_entries, "Dragon");
            Assert.AreEqual("1", BestiaryPresenter.KillsLine(BestiaryOps.Find(_entries, "Dragon")).Value);
        }

        [Test]
        public void DrawLines_HideTheNameUntilTheMagicHasBeenDrawnSomewhere()
        {
            var fireball = ScriptableObject.CreateInstance<Assets.Scripts.Cards.MagicSO>();
            fireball.Key = "Fireball";
            fireball.DisplayName = "Fireball";

            var enemy = MakeEnemy();
            enemy.DrawableMagics = new List<DrawableMagicEntry>
            {
                new DrawableMagicEntry { Magic = fireball, Charges = 2 }
            };

            var undiscovered = BestiaryPresenter.DrawLines(enemy, _ => false);
            Assert.AreEqual(BestiaryPresenter.Unknown, undiscovered[0].Label);
            Assert.AreEqual("x2", undiscovered[0].Value,
                "The charge count is never hidden - what a draw is worth is the decision being made.");
            Assert.AreEqual(BestiaryTone.Unknown, undiscovered[0].Tone);

            var discovered = BestiaryPresenter.DrawLines(enemy, key => key == "Fireball");
            Assert.AreEqual("Fireball", discovered[0].Label);
            Assert.AreEqual(BestiaryTone.Neutral, discovered[0].Tone);
        }

        [Test]
        public void IsDrawKnown_WithNoRecord_HidesRatherThanLeaks()
        {
            // A call site that forgets to pass the discovery record should over-hide, never leak.
            Assert.IsFalse(BestiaryPresenter.IsDrawKnown("Fireball", null));
            Assert.IsFalse(BestiaryPresenter.IsDrawKnown(null, _ => true));
        }

        [Test]
        public void StatLines_KeepAZeroOnStatsEveryUnitIsAuthoredWith()
        {
            // "No armour" is a finding; "INT 0" on a melee enemy is noise. The split is read off
            // StatCatalog.AuthoringDefault so a stat added later needs no change here.
            Assert.IsTrue(BestiaryPresenter.IsWorthShowing(Assets.Scripts.UnitStats.StatType.Endurance, 0));
            Assert.IsFalse(BestiaryPresenter.IsWorthShowing(Assets.Scripts.UnitStats.StatType.Intelligence, 0));
            Assert.IsTrue(BestiaryPresenter.IsWorthShowing(Assets.Scripts.UnitStats.StatType.Intelligence, 4));
            Assert.IsFalse(BestiaryPresenter.IsWorthShowing(Assets.Scripts.UnitStats.StatType.MaxHealth, 20),
                "Health has its own line above the stat block.");
        }

        [Test]
        public void SeenCount_CountsOnlyCataloguedEnemiesThatHaveBeenMet()
        {
            var a = MakeEnemy();
            a.Key = "A";
            var b = MakeEnemy();
            b.Key = "B";

            BestiaryOps.MarkSeen(_entries, "A");
            BestiaryOps.MarkSeen(_entries, "NotInTheCatalog");

            Assert.AreEqual(1, BestiaryPresenter.SeenCount(new List<EnemySO> { a, b }, _entries));
        }

        // ============================================================
        //  Catalog integrity
        // ============================================================
        //
        // The bestiary's denominator is the Resources catalog, so an enemy missing from it is
        // invisible on the screen even after it has been fought - the same silent-gap failure that
        // once shipped MagicComboCatalog with a duplicate and a missing combo.

        [Test]
        public void EnemyCatalog_ExistsInResources()
        {
            Assert.IsNotNull(EnemyCatalogSO.Load(),
                "Assets/Resources/EnemyCatalog.asset is missing. The hub bestiary loads it from " +
                "Resources because MenuScene wires no combat managers.");
        }

        [Test]
        public void EnemyCatalog_ContainsEveryEnemyAsset()
        {
            var catalog = EnemyCatalogSO.Load();
            Assert.IsNotNull(catalog);

            var listed = new HashSet<string>();
            foreach (var enemy in catalog.Enemies)
            {
                Assert.IsNotNull(enemy,
                    "EnemyCatalog holds a null entry. Growing the list by overriding its size is " +
                    "how that happens - edit the asset itself.");
                Assert.IsTrue(listed.Add(enemy.SaveKey),
                    $"EnemyCatalog lists '{enemy.SaveKey}' twice.");
            }

            var missing = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemySO"))
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemySO>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy != null && !listed.Contains(enemy.SaveKey))
                {
                    missing.Add(enemy.name);
                }
            }

            CollectionAssert.IsEmpty(missing,
                $"Enemy asset(s) {string.Join(", ", missing)} are not in Resources/EnemyCatalog, so " +
                "they can never appear in the bestiary however often they are fought.");
        }
    }
}
