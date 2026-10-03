using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.ElementParsers;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Reflection;
using System.Xml;

namespace Aurora.Tests.Tests;

public sealed class SelectionOwnerCleanupTests
{
    [Theory]
    [InlineData("replace")]
    [InlineData("level")]
    [InlineData("requirements")]
    public async Task LosingAChoiceRemovesItsSelectionsAndNestedSpellsFromSavedState(string change)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var manager = CharacterManager.Current;
        await manager.New(false);
        var progression = (ProgressionManager)typeof(CharacterManager)
            .GetField("_progressionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        string condition = change == "requirements" ? " requirements='ID_TEST_CLEANUP_SWITCH'" : "";
        var owner = Parse("OWNER", "Background", $"<rules><select type='Language' name='Languages' number='2'{condition}/><select type='Feat' name='Feat'{condition}/></rules>");
        var replacement = Parse("REPLACEMENT", "Background");
        var toggle = Parse("SWITCH", "Proficiency");
        var language1 = Parse("LANGUAGE_ONE", "Language");
        var language2 = Parse("LANGUAGE_TWO", "Language");
        var feat = Parse("FEAT", "Feat", "<rules><select type='Spell' name='Spell'/></rules>");
        var spell = Parse("SPELL_TEST", "Spell", "<setters><set name='level'>0</set><set name='school'>Evocation</set><set name='time'>1 action</set><set name='duration'>Instantaneous</set><set name='range'>Self</set></setters>");
        var retained = Parse("RETAINED", "Proficiency");
        ElementBase[] catalog = [owner, replacement, toggle, language1, language2, feat, spell, retained];
        foreach (var element in catalog) DataManager.Current.ElementsCollection.Add(element);
        try
        {
            progression.ProgressionLevel = 1;
            manager.RegisterElement(toggle);
            manager.RegisterElement(owner);
            manager.RegisterElement(retained);
            var languageRule = owner.GetSelectRules().First();
            var featRule = owner.GetSelectRules().Last();
            var spellRule = feat.GetSelectRules().Single();
            manager.GetProgressManager(languageRule).Should().NotBeNull();
            manager.GetProgressManager(featRule).Should().NotBeNull();
            handler.SetRegisteredElement(languageRule, language1.Id, 1);
            handler.SetRegisteredElement(languageRule, language2.Id, 2);
            handler.SetRegisteredElement(featRule, feat.Id);
            handler.SetRegisteredElement(spellRule, spell.Id);
            manager.GetElements().Should().Contain(spell);

            if (change == "replace") manager.RegisterElement(replacement);
            else if (change == "requirements") manager.UnregisterElement(toggle);
            else { progression.ProgressionLevel = 0; manager.ReprocessCharacter(); }

            var remaining = manager.GetElements();
            remaining.Should().Contain(retained);
            foreach (var removed in new[] { language1, language2, feat, spell })
                remaining.Should().NotContain(removed, "its granting choice no longer exists");
            handler.GetRegisteredElement(languageRule, 1).Should().BeNull();
            handler.GetRegisteredElement(languageRule, 2).Should().BeNull();
            handler.GetRegisteredElement(featRule).Should().BeNull();
            handler.GetRegisteredElement(spellRule).Should().BeNull();

            // Exercise the actual writer sections that previously kept the orphaned IDs.
            var file = new CharacterFile("unused.dnd5e");
            typeof(CharacterFile).GetField("_document", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(file, new XmlDocument());
            foreach (string method in new[] { "CreateSumNode", "CreateMagicNode" })
            {
                var node = (XmlNode)typeof(CharacterFile).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(file, null)!;
                foreach (var removed in new[] { language1, language2, feat, spell })
                    node.OuterXml.Should().NotContain(removed.Id);
            }
        }
        finally
        {
            await manager.New(false);
            foreach (var element in catalog) DataManager.Current.ElementsCollection.Remove(element);
        }
    }

    [Fact]
    public async Task RemovingOneOwnerPreservesTheSameLanguageFromAnotherActiveChoice()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var handler = new TestSelectionRuleExpanderHandler();
        SelectionRuleExpanderContext.Current = handler;
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var manager = CharacterManager.Current;
        await manager.New(false);
        var progression = (ProgressionManager)typeof(CharacterManager)
            .GetField("_progressionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        var removedOwner = Parse("REMOVED_OWNER", "Proficiency", "<rules><select type='Language' name='Language'/></rules>");
        var retainedOwner = Parse("RETAINED_OWNER", "Proficiency", "<rules><select type='Language' name='Language'/></rules>");
        var removedLanguage = Parse("SHARED_LANGUAGE", "Language");
        var retainedLanguage = Parse("SHARED_LANGUAGE", "Language");
        try
        {
            progression.ProgressionLevel = 1;
            manager.RegisterElement(removedOwner);
            manager.RegisterElement(retainedOwner);
            removedLanguage.Aquisition.SelectedBy(removedOwner.GetSelectRules().Single());
            retainedLanguage.Aquisition.SelectedBy(retainedOwner.GetSelectRules().Single());
            manager.RegisterElement(removedLanguage);
            manager.RegisterElement(retainedLanguage);
            manager.UnregisterElement(removedOwner);
            manager.GetElements().Should().NotContain(removedLanguage);
            manager.GetElements().Should().Contain(retainedLanguage);
            manager.GetProgressManager(retainedLanguage.Aquisition.SelectRule).Should().NotBeNull();
        }
        finally { await manager.New(false); }
    }

    private static ElementBase Parse(string suffix, string type, string body = "")
    {
        var xml = new XmlDocument();
        xml.LoadXml($"<element id='ID_TEST_CLEANUP_{suffix}' name='{suffix}' type='{type}' source='Internal'><description><p>Test content.</p></description>{body}</element>");
        ElementParser parser = type switch
        {
            "Language" => new LanguageElementParser(),
            "Spell" => new SpellElementParser(),
            _ => new ElementParser()
        };
        var element = parser.ParseElement(xml.DocumentElement!);
        return type == "Proficiency" ? element.Construct<Builder.Data.Elements.Proficiency>() : element;
    }
}
