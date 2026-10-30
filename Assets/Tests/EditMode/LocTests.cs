using System.Collections.Generic;
using ImmoralityGaming.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tests.EditMode
{
    /// <summary>
    /// The text lookup: a key gives the words in the current language, falls back to the default
    /// language, and shows the key itself when the table has no entry (a missing text is visible,
    /// never blank).
    /// </summary>
    public class LocTests
    {
        private TextTableSO _table;

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<TextTableSO>();
            _table.Languages = new List<string> { "en", "nl" };
            _table.Entries = new List<TextEntry>
            {
                new TextEntry { Key = "ui.hub.forge.name", Values = new List<string> { "Forge", "Smidse" } },
                new TextEntry { Key = "hero.warrior.name", Values = new List<string> { "Warrior", "" } },
                new TextEntry { Key = "combat.damage", Values = new List<string> { "{target}: {damage} damage" } },
            };
            Loc.SetTable(_table);
        }

        [TearDown]
        public void TearDown()
        {
            Loc.SetTable(null);
            Object.DestroyImmediate(_table);
        }

        [Test]
        public void Get_KnownKey_ReturnsDefaultLanguage()
        {
            Assert.AreEqual("Forge", Loc.Get("ui.hub.forge.name"));
        }

        [Test]
        public void Get_OtherLanguage_ReturnsItsWording()
        {
            Loc.Language = "nl";
            Assert.AreEqual("Smidse", Loc.Get("ui.hub.forge.name"));
        }

        [Test]
        public void Get_LanguageMissingWording_FallsBackToDefault()
        {
            Loc.Language = "nl";
            Assert.AreEqual("Warrior", Loc.Get("hero.warrior.name"));
        }

        [Test]
        public void Get_UnknownLanguage_FallsBackToDefault()
        {
            Loc.Language = "fr";
            Assert.AreEqual("Forge", Loc.Get("ui.hub.forge.name"));
        }

        [Test]
        public void Get_UnknownKey_ReturnsKey()
        {
            Assert.AreEqual("ui.nothing", Loc.Get("ui.nothing"));
        }

        [Test]
        public void Format_NamedPlaceholders_AreFilledInAnyOrder()
        {
            Assert.AreEqual("Goblin: 18 damage", Loc.Format("combat.damage", ("damage", 18), ("target", "Goblin")));
        }

        [Test]
        public void Fill_UnknownPlaceholder_IsLeftVisible()
        {
            Assert.AreEqual("{oops} left", Loc.Fill("{oops} left", ("damage", 3)));
        }

        [Test]
        public void Fill_ValueContainingPlaceholder_IsNotSubstitutedInto()
        {
            Assert.AreEqual("{target} hits Goblin", Loc.Fill("{name} hits {target}", ("name", "{target}"), ("target", "Goblin")));
        }

        [Test]
        public void Find_AfterEntryAdded_NeedsRebuildIndex()
        {
            _table.Entries.Add(new TextEntry { Key = "ui.new", Values = new List<string> { "New" } });
            _table.RebuildIndex();
            Assert.AreEqual("New", Loc.Get("ui.new"));
        }

        [Test]
        public void Localize_KeyRefsUnderRoot_AreReplaced()
        {
            var root = new VisualElement();
            var label = new Label("#ui.hub.forge.name") { tooltip = "#hero.warrior.name" };
            var plain = new Label("Plain");
            root.Add(label);
            root.Add(plain);

            Loc.Localize(root);

            Assert.AreEqual("Forge", label.text);
            Assert.AreEqual("Warrior", label.tooltip);
            Assert.AreEqual("Plain", plain.text);
        }
    }
}
