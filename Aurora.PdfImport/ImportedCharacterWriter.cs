using System.Xml;

namespace Aurora.PdfImport;

/// <summary>
/// Writes an <see cref="ImportResult"/> to a .dnd5e character XML file.
/// Produces a file compatible with the Aurora character loader.
/// </summary>
public static class ImportedCharacterWriter
{
    private const string FormatVersion = "1.0.166.7407";

    public static void Write(ImportResult result, string outputPath)
    {
        var doc = BuildDocument(result);
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "\t", Encoding = System.Text.Encoding.UTF8 };
        using var writer = XmlWriter.Create(outputPath, settings);
        doc.WriteTo(writer);
    }

    public static string ToXmlString(ImportResult result)
    {
        var doc = BuildDocument(result);
        var sb = new System.Text.StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, IndentChars = "\t", Encoding = System.Text.Encoding.UTF8, OmitXmlDeclaration = false };
        using (var writer = XmlWriter.Create(sb, settings))
            doc.WriteTo(writer);
        return sb.ToString();
    }

    // ── Document builder ──────────────────────────────────────────────────────

    private static XmlDocument BuildDocument(ImportResult result)
    {
        var doc = new XmlDocument();

        var root = (XmlElement)doc.AppendChild(doc.CreateElement("character"))!;
        root.SetAttribute("version", FormatVersion);
        root.SetAttribute("preview", "false");

        root.AppendChild(doc.CreateComment(" Aurora - https://www.aurorabuilder.com "));
        root.AppendChild(doc.CreateComment(" information "));
        AppendInformation(doc, root);

        root.AppendChild(doc.CreateComment(" display data "));
        AppendDisplayProperties(doc, root, result);

        root.AppendChild(doc.CreateComment(" build data "));
        AppendBuild(doc, root, result);

        root.AppendChild(doc.CreateComment(" restricted sources "));
        AppendSources(doc, root);

        return doc;
    }

    // ── <information> ─────────────────────────────────────────────────────────

    private static void AppendInformation(XmlDocument doc, XmlElement root)
    {
        var info = Elem(doc, root, "information");
        Child(doc, info, "campaign",     "");
        Child(doc, info, "notes",        "");
        Child(doc, info, "traits",       "");
        Child(doc, info, "ideals",       "");
        Child(doc, info, "bonds",        "");
        Child(doc, info, "flaws",        "");
        Child(doc, info, "backstory",    "");
    }

    // ── <display-properties> ──────────────────────────────────────────────────

    private static void AppendDisplayProperties(XmlDocument doc, XmlElement root, ImportResult result)
    {
        var dp = Elem(doc, root, "display-properties");
        Child(doc, dp, "name",       result.CharacterName);
        Child(doc, dp, "race",       result.DisplayRace);
        Child(doc, dp, "class",      result.DisplayClass);
        Child(doc, dp, "background", result.DisplayBackground);
        Child(doc, dp, "level",      result.Level.ToString());
        Child(doc, dp, "favorite",   "false");
        Child(doc, dp, "local-portrait",  "");
        Child(doc, dp, "base64-portrait", "");
    }

    // ── <build> ───────────────────────────────────────────────────────────────

    private static void AppendBuild(XmlDocument doc, XmlElement root, ImportResult result)
    {
        var build = Elem(doc, root, "build");

        // <input>
        var input = Elem(doc, build, "input");
        Child(doc, input, "name",        result.CharacterName);
        Child(doc, input, "player-name", result.PlayerName);
        Child(doc, input, "gender",      result.Gender);
        Child(doc, input, "experience",  "0");
        Child(doc, input, "backstory",   result.Backstory);

        var currency = Elem(doc, input, "currency");
        foreach (string coin in new[] { "copper", "silver", "electrum", "gold", "platinum" })
            Child(doc, currency, coin, "0");
        Child(doc, currency, "equipment", "");
        Child(doc, currency, "treasure",  "");

        var org = Elem(doc, input, "organization");
        Child(doc, org, "name",   "");
        Child(doc, org, "symbol", "");
        Child(doc, org, "allies", "");

        Child(doc, input, "additional-features", "");

        var notes = Elem(doc, input, "notes");
        Child(doc, notes, "personality-traits", result.PersonalityTraits);
        Child(doc, notes, "ideals",             result.Ideals);
        Child(doc, notes, "bonds",              result.Bonds);
        Child(doc, notes, "flaws",              result.Flaws);

        // <appearance>
        var appearance = Elem(doc, build, "appearance");
        Child(doc, appearance, "age",    result.Age);
        Child(doc, appearance, "height", result.Height);
        Child(doc, appearance, "weight", result.Weight);
        Child(doc, appearance, "eyes",   result.Eyes);
        Child(doc, appearance, "skin",   result.Skin);
        Child(doc, appearance, "hair",   result.Hair);

        // <abilities>
        int points = CalculateAvailablePoints(result);
        var abilities = Elem(doc, build, "abilities");
        abilities.SetAttribute("available-points", points.ToString());
        Child(doc, abilities, "strength",     result.Strength.ToString());
        Child(doc, abilities, "dexterity",    result.Dexterity.ToString());
        Child(doc, abilities, "constitution", result.Constitution.ToString());
        Child(doc, abilities, "intelligence", result.Intelligence.ToString());
        Child(doc, abilities, "wisdom",       result.Wisdom.ToString());
        Child(doc, abilities, "charisma",     result.Charisma.ToString());

        // <elements>
        // Resolved elements (choosing from ambiguities where the user has decided).
        var allElements = result.Elements.ToList();
        allElements.AddRange(result.Ambiguities
            .Where(a => a.Chosen != null)
            .Select(a => a.Chosen!));

        int levelCount      = allElements.Count(e => e.TypeName == "Level");
        int registeredCount = allElements.Count;

        var elements = Elem(doc, build, "elements");
        elements.SetAttribute("level-count",      levelCount.ToString());
        elements.SetAttribute("registered-count", registeredCount.ToString());

        foreach (var el in allElements.OrderBy(e => e.Level).ThenBy(e => e.TypeName))
        {
            var node = Elem(doc, elements, "element");
            node.SetAttribute("type", el.TypeName);
            node.SetAttribute("name", el.Name);
            node.SetAttribute("id",   el.AuroraId);
        }

        // Aurora's loader validates element count against this node at load time.
        // We use our explicit count; the engine may register additional dynamically-granted
        // elements, producing a negative difference which the loader accepts as a warning.
        var sum = Elem(doc, build, "sum");
        sum.SetAttribute("element-count", registeredCount.ToString());
    }

    // ── <sources> ─────────────────────────────────────────────────────────────

    private static void AppendSources(XmlDocument doc, XmlElement root)
    {
        // Empty sources node — no restrictions by default.
        Elem(doc, root, "sources");
    }

    // ── Utility ───────────────────────────────────────────────────────────────

    private static int CalculateAvailablePoints(ImportResult r)
    {
        // Standard point-buy costs; Aurora expects the "available points remaining" value.
        // For imported characters we set to 0 (point-buy already spent).
        return 0;
    }

    private static XmlElement Elem(XmlDocument doc, XmlNode parent, string name)
    {
        var el = doc.CreateElement(name);
        parent.AppendChild(el);
        return el;
    }

    private static void Child(XmlDocument doc, XmlNode parent, string name, string text)
    {
        var el = doc.CreateElement(name);
        el.InnerText = text;
        parent.AppendChild(el);
    }
}
