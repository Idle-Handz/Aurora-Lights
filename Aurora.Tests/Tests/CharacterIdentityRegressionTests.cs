using System.Reflection;
using System.Xml.Linq;
using Aurora.App.Services;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

public sealed class CharacterIdentityRegressionTests
{
    [Fact]
    public Task ReopeningLastLoadedCharacterAfterAnotherTabWasActivatedPreservesItsBuild() =>
        WithCharacters(async (service, fileA, fileB) =>
        {
            var tabs = new CharacterTabService();
            var tabA = await OpenAsync(service, tabs, fileA);
            var tabB = await OpenAsync(service, tabs, fileB);
            service.IsPreloaded(fileB).Should().BeTrue("the immediate preload still matches the engine");

            tabs.CloseTab(tabB);
            await CharacterContext.ReleaseAsync(tabB);
            using (await CharacterContext.EnterAsync(tabA))
                CharacterManager.Current.Character.Abilities.Strength.BaseScore.Should().Be(12);

            service.IsPreloaded(fileB).Should().BeFalse("a file identity cannot validate a mutable singleton after a tab swap");
            var reopened = await OpenAsync(service, tabs, fileB);
            reopened.Character!.Abilities.Strength.BaseScore.Should().Be(18);
            (await BuildService.SaveTabAsync(reopened)).Should().BeNull();

            await CharacterContext.InvalidateAsync();
            (await service.LoadCharacterAsync(fileB)).Success.Should().BeTrue();
            service.CurrentCharacter!.Name.Should().Be("Character B");
            service.CurrentCharacter.Abilities.Strength.BaseScore.Should().Be(18,
                "saving reopened B must never write A's abilities into B's file");
        });

    [Fact]
    public Task ClosingAndDiscardingEditsInvalidatesThePreloadWithoutAnotherTabSwap() =>
        WithCharacters(async (service, fileA, _) =>
        {
            var tabs = new CharacterTabService();
            var tab = await OpenAsync(service, tabs, fileA);
            tab.Character!.Abilities.Strength.BaseScore = 20;
            tabs.CloseTab(tab);
            await CharacterContext.ReleaseAsync(tab);

            service.IsPreloaded(fileA).Should().BeFalse();
            var reopened = await OpenAsync(service, tabs, fileA);
            reopened.Character!.Abilities.Strength.BaseScore.Should().Be(12);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AnExternallyChangedFileIsNotReusedAsAPreload(bool deleted) =>
        WithCharacters(async (service, fileA, _) =>
        {
            (await service.LoadCharacterAsync(fileA)).Success.Should().BeTrue();
            service.IsPreloaded(fileA).Should().BeTrue();
            if (deleted) File.Delete(fileA.FilePath);
            else File.AppendAllText(fileA.FilePath, "\n<!-- changed externally -->");
            service.IsPreloaded(fileA).Should().BeFalse();
        });

    [Theory]
    [InlineData("uninitialized")]
    [InlineData("malformed")]
    [InlineData("missing")]
    public Task FailedLoadCannotAttachOrSaveThePreviousCharacter(string failure) =>
        WithCharacters(async (service, fileA, fileB) =>
        {
            (await service.LoadCharacterAsync(fileA)).Success.Should().BeTrue();
            byte[] originalA = File.ReadAllBytes(fileA.FilePath);
            DamageFile(fileB, failure);
            byte[]? damagedB = File.Exists(fileB.FilePath) ? File.ReadAllBytes(fileB.FilePath) : null;

            var result = await service.LoadCharacterAsync(fileB);
            result.Success.Should().BeFalse();
            service.CurrentCharacter.Should().BeNull();
            service.CurrentCharacterFile.Should().BeNull();
            service.IsPreloaded(fileB).Should().BeFalse();
            CharacterContext.ActiveTab.Should().BeNull();

            var failedTab = new CharacterTab(fileB);
            Func<Task> save = () => BuildService.SaveTabAsync(failedTab);
            await save.Should().ThrowAsync<Exception>();
            failedTab.Character.Should().BeNull();
            File.ReadAllBytes(fileA.FilePath).Should().Equal(originalA);
            if (damagedB is null)
                File.Exists(fileB.FilePath).Should().BeFalse("a failed save must not create a copy of A at B's missing path");
            else
                File.ReadAllBytes(fileB.FilePath).Should().Equal(damagedB);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedContextSwapDoesNotClaimOrAttachThePreviousCharacter(bool restoreFromMemory) =>
        WithCharacters(async (service, fileA, fileB) =>
        {
            var tabs = new CharacterTabService();
            var tabA = await OpenAsync(service, tabs, fileA);
            byte[] originalB = File.ReadAllBytes(fileB.FilePath);
            var tabB = new CharacterTab(fileB);
            if (restoreFromMemory)
            {
                var document = XDocument.Load(fileB.FilePath);
                document.Root!.Element("display-properties")!.Remove();
                tabB.StateXml = System.Text.Encoding.UTF8.GetBytes(document.ToString());
            }
            else
            {
                DamageFile(fileB, "uninitialized");
            }

            // File list metadata can outlive the file's valid display node.
            tabB.File.DisplayLevel = "1";
            string originalPath = fileB.FilePath;
            var originalStamp = fileB.LastKnownDiskStamp;
            Func<Task> swap = async () => { using var scope = await CharacterContext.EnterAsync(tabB); };
            await swap.Should().ThrowAsync<InvalidDataException>();
            CharacterContext.ActiveTab.Should().BeNull();
            tabB.Character.Should().BeNull();
            fileB.FilePath.Should().Be(originalPath);
            if (restoreFromMemory)
            {
                fileB.LastKnownDiskStamp.Should().Be(originalStamp);
                File.ReadAllBytes(fileB.FilePath).Should().Equal(originalB);
            }

            using (await CharacterContext.EnterAsync(tabA))
            {
                tabA.Character!.Name.Should().Be("Character A");
                tabA.Character.Abilities.Strength.BaseScore.Should().Be(12);
            }
        });

    [Fact]
    public Task MissingElementsRemainAUsablePartialLoadInBothEntryPoints() =>
        WithCharacters(async (service, fileA, fileB) =>
        {
            var document = XDocument.Load(fileB.FilePath);
            document.Root!.Element("build")!.Element("sum")!.Add(
                new XElement("element", new XAttribute("id", "ID_MISSING_IDENTITY_FEAT"), new XAttribute("type", "Feat")));
            document.Save(fileB.FilePath);

            var result = await service.LoadCharacterAsync(fileB);
            result.Success.Should().BeFalse("missing content is reported while preserving the usable character");
            service.CurrentCharacter!.Name.Should().Be("Character B");
            service.CurrentCharacterFile.Should().BeSameAs(fileB);
            service.IsPreloaded(fileB).Should().BeTrue();

            (await service.LoadCharacterAsync(fileA)).Success.Should().BeTrue();
            var partialTab = new CharacterTab(fileB);
            using (await CharacterContext.EnterAsync(partialTab))
            {
                partialTab.Character!.Name.Should().Be("Character B");
                partialTab.Character.Abilities.Strength.BaseScore.Should().Be(18);
                CharacterContext.ActiveTab.Should().BeSameAs(partialTab);
            }
        });

    private static async Task<CharacterTab> OpenAsync(CharacterService service, CharacterTabService tabs, CharacterFile file)
    {
        // Match the page's preload-or-load decision, then open and claim its resulting character.
        if (!service.IsPreloaded(file))
            (await service.LoadCharacterAsync(file)).Success.Should().BeTrue();
        var tab = tabs.OpenTab(file, service.CurrentCharacter!);
        tab.Snapshot = CharacterSnapshot.From(tab.Character!);
        CharacterContext.ClaimAfterLoad(tab);
        return tab;
    }

    private static void DamageFile(CharacterFile file, string failure)
    {
        file.InitializeDisplayPropertiesFromFilePath();
        switch (failure)
        {
            case "uninitialized":
                var document = XDocument.Load(file.FilePath);
                document.Root!.Element("display-properties")!.Remove();
                document.Save(file.FilePath);
                break;
            case "malformed": File.WriteAllText(file.FilePath, "<character"); break;
            case "missing": File.Delete(file.FilePath); break;
        }
    }

    private static async Task WithCharacters(Func<CharacterService, CharacterFile, CharacterFile, Task> test)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        DataManager.Current.InitializeDirectories();
        var catalog = DataManager.Current.ElementsCollection;
        var originalElements = catalog.ToArray();
        var originalSelection = SelectionRuleExpanderContext.Current;
        var originalSpells = SpellcastingSectionContext.Current;
        var manager = CharacterManager.Current;
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "identity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await CharacterContext.InvalidateAsync();
            await manager.New(false);
            SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
            SpellcastingSectionContext.Current = new TestSpellHandler();
            catalog.Clear();
            for (int number = 1; number <= 2; number++)
            {
                var level = new LevelElement
                {
                    ElementHeader = new ElementHeader($"Level {number}", "Level", "Internal", $"ID_IDENTITY_LEVEL_{number}"),
                    Level = number,
                    RequiredExperience = (number - 1) * 300,
                };
                level.ElementSetters.Add(new("Level", number.ToString()));
                catalog.Add(level);
                if (number == 1) manager.RegisterElement(level);
            }
            manager.Character.Name = "Character A";
            manager.Character.Abilities.Strength.BaseScore = 12;
            var fileA = new CharacterFile(Path.Combine(root, "A.dnd5e"));
            fileA.Save(manager.Character).Should().BeTrue();
            manager.Character.Name = "Character B";
            manager.Character.Abilities.Strength.BaseScore = 18;
            var fileB = new CharacterFile(Path.Combine(root, "B.dnd5e"));
            fileB.Save(manager.Character).Should().BeTrue();

            var service = new CharacterService();
            // The fixture supplies its complete minimal catalog. Keep these tests about
            // actual character hydration, independent of the machine's content database.
            typeof(CharacterService).GetField("_elementsInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(service, true);
            await test(service, fileA, fileB);
        }
        finally
        {
            await CharacterContext.InvalidateAsync();
            await manager.New(false);
            catalog.Clear();
            catalog.AddRange(originalElements);
            SelectionRuleExpanderContext.Current = originalSelection;
            SpellcastingSectionContext.Current = originalSpells;
            Directory.Delete(root, recursive: true);
        }
    }
}
