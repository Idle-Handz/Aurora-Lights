using System.Security.Cryptography;
using Aurora.Documents.ExportContent;
using Aurora.Documents.ExportContent.Equipment;
using Aurora.Documents.ExportContent.Notes;
using Aurora.Documents.Resources;
using Aurora.Documents.Resources.Aurora;
using Aurora.Documents.Sheets;
using Aurora.Documents.Writers;
using iTextSharp.text.pdf;

namespace Aurora.Tests.Tests;

public sealed class AuroraDocumentsCompatibilityTests
{
    [Fact]
    public void RestoredAssembly_PreservesLegacyIdentityAndExportedTypeCount()
    {
        var assembly = typeof(CharacterSheetBase).Assembly;

        assembly.GetName().Name.Should().Be("Aurora.Documents");
        assembly.GetName().Version.Should().Be(new Version(1, 0, 94, 7407));
        assembly.GetExportedTypes().Should().HaveCount(25);
    }

    [Fact]
    public void EmbeddedPdfTemplates_PreserveLegacyNamesSizesAndContent()
    {
        var assembly = typeof(DocumentResources).Assembly;
        var expected = new Dictionary<string, (long Length, string Sha256)>
        {
            ["Aurora.Documents.Resources.Aurora.Pages.equipment.pdf"] =
                (2_607_011, "289AF8BA634AF85595FE6614E796BFDD8CBAA16CB76CB0910BB89CB0A30785F9"),
            ["Aurora.Documents.Resources.Aurora.Pages.equipment_page.pdf"] =
                (908_646, "9F2BC8A8E1C0E2714AB620991445AFCA30A3C340C8C5218E2AF83CDAEB543345"),
            ["Aurora.Documents.Resources.Aurora.Pages.notes_page.pdf"] =
                (901_285, "4A1A6EC88B6DC32D8307A3063D54B1AE68FC65B6601D53BA5B1D51535BB3344C")
        };

        assembly.GetManifestResourceNames().Should().BeEquivalentTo(expected.Keys);
        foreach ((string name, (long length, string hash)) in expected)
        {
            using Stream stream = assembly.GetManifestResourceStream(name)!;
            stream.Should().NotBeNull();
            stream.Length.Should().Be(length);
            Convert.ToHexString(SHA256.HashData(stream)).Should().Be(hash);
        }
    }

    [Fact]
    public void CharacterSheetConfiguration_PreservesLegacyDefaults()
    {
#pragma warning disable CS0618
        var configuration = new CharacterSheetConfiguration();

        configuration.IsEditable.Should().BeFalse();
#pragma warning restore CS0618
        configuration.IsFormFillable.Should().BeFalse();
        configuration.IncludeCharacterPage.Should().BeTrue();
        configuration.IncludeBackgroundPage.Should().BeTrue();
        configuration.IncludeSpellcastingPage.Should().BeFalse();
        configuration.IncludeSpellcards.Should().BeFalse();
        configuration.IncludeEquipmentPage.Should().BeFalse();
        configuration.IncludeNotesPage.Should().BeFalse();
        configuration.IncludeFormatting.Should().BeFalse();
        configuration.FlattenFields.Should().BeEmpty();
        configuration.FlattenFieldsCollection.Should().BeFalse();
    }

    [Fact]
    public void ExportModels_PreserveLegacyConstructorsAndMutableCollections()
    {
        var equipment = new EquipmentExportContent();
        var item = new InventoryItemExportContent("Torch");
        var storage = new StoredItemsExportContent("Pack");
        var vehicle = new VehicleExportContent("Wagon");
        var notes = new NotesExportContent();

        equipment.AdventuringGear.Should().BeEmpty();
        equipment.MagicItems.Should().BeEmpty();
        equipment.Valuables.Should().BeEmpty();
        equipment.StorageLocations.Should().BeEmpty();
        equipment.Coinage.Should().NotBeNull();
        equipment.Coinage.Copper.Should().BeEmpty();
        equipment.AttunedMagicItems.Should().BeEmpty();
        equipment.AttunementCurrent.Should().BeEmpty();
        equipment.AttunementMaximum.Should().BeEmpty();
        equipment.WeightCarried.Should().BeNull();

        item.Name.Should().Be("Torch");
        item.Amount.Should().BeNull();
        item.IsEquipped.Should().BeFalse();
        storage.Name.Should().Be("Pack");
        storage.Items.Should().BeEmpty();
        vehicle.Name.Should().Be("Wagon");
        vehicle.Cargo.Should().BeEmpty();
        notes.LeftNotesColumn.Should().BeEmpty();
        notes.RightNotesColumn.Should().BeEmpty();
    }

    [Fact]
    public void AuroraCharacterSheet_RequestsOnlyConfiguredOptionalContent()
    {
        var configuration = new CharacterSheetConfiguration();
        var provider = new RecordingContentProvider();
        var sheet = new AuroraCharacterSheet(configuration);

        sheet.Generate(provider);
        provider.EquipmentRequests.Should().Be(0);
        provider.NotesRequests.Should().Be(0);

        configuration.IncludeEquipmentPage = true;
        configuration.IncludeNotesPage = true;
        sheet.Generate(provider);

        provider.EquipmentRequests.Should().Be(1);
        provider.NotesRequests.Should().Be(1);
    }

    [Fact]
    public void DocumentResources_ExposeTheLegacyTemplates()
    {
        var resources = new AuroraDocumentResources();

        using Stream equipment = resources.GetEquipmentPage();
        using Stream notes = resources.GetNotesPage();

        equipment.Length.Should().Be(908_646);
        notes.Length.Should().Be(901_285);
        new DocumentResources().GetResource("missing").Should().BeNull();
    }

    [Fact]
    public void NotesPageWriter_PreservesHtmlConversionAndPdfFieldWriting()
    {
        var configuration = new CharacterSheetConfiguration();
        var resources = new AuroraDocumentResources();
        string left = $"First{Environment.NewLine}{Environment.NewLine}Third";
        string right = "Right";

        var htmlWriter = new NotesPageWriter(configuration, null!);
        htmlWriter.ToHtml(left).Should().Be("<p>First</p><p>&nbsp;</p><p>Third</p>");

        using Stream template = resources.GetNotesPage();
        using var reader = new PdfReader(template);
        using var output = new MemoryStream();
        using (var stamper = new PdfStamper(reader, output))
        {
            var writer = new NotesPageWriter(configuration, stamper);
            writer.Write(new NotesExportContent
            {
                LeftNotesColumn = left,
                RightNotesColumn = right
            });
        }

        using var written = new PdfReader(output.ToArray());
        written.AcroFields.GetField("notes_page_left").Should().Be(left);
        written.AcroFields.GetField("notes_page_right").Should().Be(right);
    }

    [Fact]
    public void EquipmentPageWriter_PreservesLegacyFieldMapping()
    {
        var resources = new AuroraDocumentResources();
        var content = new EquipmentExportContent
        {
            AttunementCurrent = "1",
            AttunementMaximum = "3",
            WeightCarried = "25",
            CarryingCapacity = "150",
            DragCapacity = "300",
            AdditionalTreasure = "A folded map",
            QuestItems = "Silver key",
            AttunedMagicItems = "<p>Ring of Protection</p>"
        };
        content.Coinage.Copper = "4";
        content.Coinage.Silver = "5";
        content.Coinage.Electrum = "6";
        content.Coinage.Gold = "7";
        content.Coinage.Platinum = "8";
        content.AdventuringGear.Add(new InventoryItemExportContent("Rope")
        {
            Amount = "2",
            Weight = "10",
            IsEquipped = true
        });
        content.MagicItems.Add(new InventoryItemExportContent("Wand")
        {
            Amount = "1",
            Weight = "1"
        });
        content.Valuables.Add(new InventoryItemExportContent("Gem")
        {
            Amount = "3",
            Weight = "0"
        });
        var storage = new StoredItemsExportContent("Wagon");
        storage.Items.Add(new InventoryItemExportContent("Rations")
        {
            Amount = "6",
            Weight = "12"
        });
        content.StorageLocations.Add(storage);

        using Stream template = resources.GetEquipmentPage();
        using var reader = new PdfReader(template);
        using var output = new MemoryStream();
        using (var stamper = new PdfStamper(reader, output))
        {
            new EquipmentPageWriter(new CharacterSheetConfiguration(), stamper).Write(content);
        }

        using var written = new PdfReader(output.ToArray());
        AcroFields fields = written.AcroFields;
        fields.GetField("equipment_page_gear_name.0").Should().Be("[Rope]");
        fields.GetField("equipment_page_gear_count.0").Should().Be("2");
        fields.GetField("equipment_page_gear_weight.0").Should().Be("10");
        fields.GetField("equipment_page_magic_gear_name.0").Should().Be("Wand");
        fields.GetField("equipment_page_valuable_name.0").Should().Be("Gem");
        fields.GetField("equipment_page_coins_cp").Should().Be("4");
        fields.GetField("equipment_page_coins_pp").Should().Be("8");
        fields.GetField("equipment_page_weight_capacity").Should().Be("150");
        fields.GetField("equipment_page_vehicle_1_name").Should().Be("Wagon");
        fields.GetField("equipment_page_vehicle_1_cargo_name.0").Should().Be("Rations");
        fields.GetField("equipment_page_additional_treasure").Should().Be("A folded map");
        fields.GetField("equipment_page_quest_items").Should().Be("Silver key");
        fields.GetField("equipment_page_magic_items").Should().Contain("Ring of Protection");
    }

    private sealed class RecordingContentProvider : IExportContentProvider
    {
        public int EquipmentRequests { get; private set; }

        public int NotesRequests { get; private set; }

        public EquipmentExportContent GetEquipmentContent()
        {
            EquipmentRequests++;
            return new EquipmentExportContent();
        }

        public NotesExportContent GetNotesContent()
        {
            NotesRequests++;
            return new NotesExportContent();
        }
    }
}
