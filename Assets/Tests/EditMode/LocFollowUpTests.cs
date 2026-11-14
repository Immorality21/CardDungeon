using System;
using Assets.Scripts.Cards;
using Assets.Scripts.Cards.Buffs;
using Assets.Scripts.Combat;
using Assets.Scripts.Heroes;
using Assets.Scripts.Heroes.UI;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>Enum names and asset keys stay off the screen: every one reads through the text table.</summary>
    public class LocFollowUpTests
    {
        [Test]
        public void BuffNames_EveryMember_HasWordsOfItsOwn()
        {
            foreach (BuffType type in Enum.GetValues(typeof(BuffType)))
            {
                if (type == BuffType.None)
                {
                    continue;
                }
                string name = BuffNames.Of(type);
                Assert.IsFalse(string.IsNullOrEmpty(name), type.ToString());
                Assert.IsFalse(name.Contains("."), $"{type} shows its key: {name}");
            }
            Assert.AreEqual("Fire resistance", BuffNames.Of(BuffType.FireResistance));
            Assert.AreEqual("Divine Protection", BuffNames.Of(BuffType.DivineProtection));
        }

        [Test]
        public void DamageTypeNames_Normal_ReadsPhysical()
        {
            Assert.AreEqual("Physical", DamageTypeNames.Of(DamageType.Normal));
            Assert.AreEqual("Lightning", DamageTypeNames.Of(DamageType.Lightning));
        }

        [Test]
        public void UnitTraits_SeveralAtOnce_JoinWithTheSeparator()
        {
            Assert.AreEqual("Undead/Demon", UnitTraitOps.DisplayName(UnitTraits.Undead | UnitTraits.Demon));
            foreach (UnitTraits trait in Enum.GetValues(typeof(UnitTraits)))
            {
                if (trait != UnitTraits.None)
                {
                    Assert.IsFalse(UnitTraitOps.DisplayName(trait).Contains("."), trait.ToString());
                }
            }
        }

        [Test]
        public void DescribePayload_NamesWhatANodeTeaches_NotItsKey()
        {
            var learns = new SphereGridNode { Key = "n", Kind = SphereNodeKind.MagicKnown, GrantedMagicKey = "SacredShield", GrantedCharges = 2 };
            var resist = new SphereGridNode { Key = "r", Kind = SphereNodeKind.Resistance, ResistType = DamageType.Normal, ResistPercent = 5 };

            StringAssert.Contains("Sacred Shield", SphereGridPresenter.DescribePayload(learns));
            StringAssert.Contains("Physical", SphereGridPresenter.DescribePayload(resist));
        }

        [Test]
        public void DescribePayload_AKeyWithNoText_FallsBackToTheKey()
        {
            var node = new SphereGridNode { Key = "n", Kind = SphereNodeKind.MagicKnown, GrantedMagicKey = "NoSuchAbility", GrantedCharges = 1 };

            StringAssert.Contains("NoSuchAbility", SphereGridPresenter.DescribePayload(node));
        }

        /// <summary>No em dash anywhere on screen (owner, 2026-11-13): a colon, a comma or brackets instead.</summary>
        [Test]
        public void TextTable_NoEntry_UsesAnEmDash()
        {
            var table = UnityEngine.Resources.Load<ImmoralityGaming.Localization.TextTableSO>(
                ImmoralityGaming.Localization.TextTableSO.ResourcePath);
            Assert.IsNotNull(table);
            var offenders = new System.Collections.Generic.List<string>();
            foreach (var entry in table.Entries)
            {
                if (entry != null && entry.Values != null && entry.Values.Exists(v => v != null && v.Contains("—")))
                {
                    offenders.Add(entry.Key);
                }
            }
            Assert.IsEmpty(offenders, "Em dashes in: " + string.Join(", ", offenders));
        }
    }
}
