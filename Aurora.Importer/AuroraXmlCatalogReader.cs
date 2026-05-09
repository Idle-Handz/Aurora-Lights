using Aurora.Importer.Models;
using System.ComponentModel;
using System.Reflection;
using System.Xml.Linq;

namespace Aurora.Importer;

internal static class AuroraXmlCatalogReader
{
    public static AuroraImportCatalog BuildCatalog(string contentDirectory)
    {
        string[] files = Directory
            .GetFiles(contentDirectory, "*.xml", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var catalog = new AuroraImportCatalog();

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(contentDirectory, file);
            XDocument xml = XDocument.Load(file);
            var info = xml.Root?.Element("info");

            catalog.Files.Add(new AuroraFileInfo
            {
                RelativePath = relativePath,
                FullPath     = file,
                Name         = info?.Element("name")?.Value ?? Path.GetFileNameWithoutExtension(file),
                Description  = info?.Element("description")?.Value,
                Author = new Author
                {
                    name = info?.Element("author")?.Value,
                    url  = info?.Element("author")?.Attribute("url")?.Value
                },
                FileVersion = new FileVersion
                {
                    versionString = info?.Element("update")?.Attribute("version")?.Value,
                    fileName      = info?.Element("update")?.Element("file")?.Attribute("name")?.Value,
                    fileUrl       = info?.Element("update")?.Element("file")?.Attribute("url")?.Value
                }
            });

            foreach (var element in xml.Root?.Elements("element") ?? Enumerable.Empty<XElement>())
            {
                string? name   = element.Attribute("name")?.Value;
                string? source = element.Attribute("source")?.Value;
                string? id     = element.Attribute("id")?.Value;
                string? type   = element.Attribute("type")?.Value;

                if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
                    continue;

                if (string.Equals(type, "spell", StringComparison.OrdinalIgnoreCase))
                {
                    AuroraSpell spell = FillSpell(element, name, source, id);
                    spell.source_file_path = relativePath;
                    catalog.Spells.Add(spell);
                }
                else
                {
                    AuroraElement el = FillElement(element, name, source, id, type);
                    el.source_file_path = relativePath;
                    catalog.Elements.Add(el);
                }
            }
        }

        return catalog;
    }

    // ── Element fill ─────────────────────────────────────────────────────────

    private static AuroraElement FillElement(XElement element, string? name, string? source, string? id, string? type)
    {
        var el = new AuroraElement
        {
            name   = name,
            type   = type ?? "auroraElement",
            source = source,
            id     = id,
            index  = BuildSlug(name)
        };

        foreach (var child in element.Elements())
        {
            string cn = child.Name.LocalName;
            bool handled = false;

            if (cn == "compendium")
            {
                el.compendium.display = Convert.ToBoolean(child.Attribute("display")?.Value ?? "true");
                handled = true;
            }
            if (cn == "supports")
            {
                el.supports = ParseTextCollection(child.Value);
                handled = true;
            }
            if (cn == "requirements")
            {
                el.requirements = ParseTextCollection(child.Value);
                handled = true;
            }
            if (cn == "prerequisites")
            {
                el.prerequisites = ParsePrerequisitesCollection(child);
                handled = true;
            }
            if (cn == "prerequisite")
            {
                el.prerequisite = child.Value;
                handled = true;
            }
            if (cn == "description")
            {
                // Store inner XML (e.g. "<p>text</p>") not child.Value (strips tags).
                // GeneratePlainDescription expects HTML that HTMLWorker can parse into paragraphs.
                el.description       = string.Concat(child.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)));
                el.descriptionRawXml = child.ToString(SaveOptions.DisableFormatting);
                handled = true;
            }
            if (cn == "extract")
            {
                el.extract = new AuroraExtract
                {
                    description = child.Element("description")?.Value,
                    items       = ParseItemEntries(child)
                };
                handled = true;
            }
            if (cn == "sheet")
            {
                el.sheet = new AuroraSheet
                {
                    rawXml  = child.ToString(SaveOptions.DisableFormatting),
                    display = child.Attribute("display") != null
                        ? Convert.ToBoolean(child.Attribute("display")!.Value) : true,
                    alt    = child.Attribute("alt")?.Value,
                    action = child.Attribute("action")?.Value,
                    usage  = child.Attribute("usage")?.Value
                };
                var descs = child.Elements("description").ToList();
                if (descs.Any())
                {
                    el.sheet.description = descs.Select(d => new Description
                    {
                        level  = d.Attribute("level")?.Value is { } lv ? Convert.ToInt32(lv) : null,
                        text   = d.Value,
                        rawXml = d.ToString(SaveOptions.DisableFormatting)
                    }).ToList();
                }
                handled = true;
            }
            if (cn == "setters" || cn == "setter")
            {
                el.setters ??= new AuroraSetters();
                FillSetters(el.setters, child);
                handled = true;
            }
            if (cn == "spellcasting")
            {
                el.spellcasting = new Spellcasting
                {
                    name         = child.Attribute("name")?.Value,
                    ability      = child.Attribute("ability")?.Value,
                    prepare      = ParseBool(child.Attribute("prepare")?.Value),
                    allowReplace = ParseBool(child.Attribute("allowReplace")?.Value),
                    extend       = ParseBool(child.Attribute("extend")?.Value) ?? false,
                    list         = child.Element("list") is { } listEl ? ParseTextCollection(listEl.Value) : null,
                    extendList   = child.Element("extend") is { } extEl ? ParseTextCollection(extEl.Value) : null
                };
                handled = true;
            }
            if (cn == "multiclass")
            {
                el.multiclass = new Multiclass
                {
                    id           = child.Attribute("id")?.Value,
                    prerequisite = child.Element("prerequisite")?.Value,
                    requirements = child.Element("requirements") is { } reqEl ? ParseTextCollection(reqEl.Value) : null
                };
                if (child.Element("setters") is { } mcSetters)
                {
                    el.multiclass.setters = new AuroraSetters();
                    FillSetters(el.multiclass.setters, mcSetters);
                }
                if (child.Element("rules") is { } mcRules)
                    el.multiclass.rules = FillRules(mcRules);
                handled = true;
            }
            if (cn == "rules")
            {
                el.rules = FillRules(child);
                handled = true;
            }
            if (cn == "grant")
            {
                el.rules ??= new Rules { grants = new(), selects = new(), stats = new() };
                el.rules.grants!.Add(ParseGrant(child));
                handled = true;
            }
            if (cn == "select")
            {
                el.rules ??= new Rules { grants = new(), selects = new(), stats = new() };
                el.rules.selects!.Add(ParseSelect(child));
                handled = true;
            }
            if (cn == "stat")
            {
                el.rules ??= new Rules { grants = new(), selects = new(), stats = new() };
                el.rules.stats!.Add(ParseStat(child));
                handled = true;
            }

            if (!handled)
            {
                el.additionalBlocks ??= new();
                el.additionalBlocks.Add(ParseBlockEntry(child));
            }
        }

        return el;
    }

    private static AuroraSpell FillSpell(XElement spellElement, string? name, string? source, string? id)
    {
        var spell = new AuroraSpell
        {
            name   = name,
            source = source,
            aurora_id = id,
            index  = BuildSlug(name)
        };

        foreach (var child in spellElement.Elements())
        {
            string cn = child.Name.LocalName;

            if (cn == "compendium")
                spell.compendium_display = Convert.ToBoolean(child.Attribute("display")?.Value ?? "true");

            if (cn == "supports")
            {
                var supports = ParseTextCollection(child.Value);
                spell.supports = supports;
                spell.classes = new();
                if (supports != null)
                    foreach (var s in supports)
                        spell.classes.Add(new BaseApiClass { name = s, index = s.ToLower().Replace(" ", "-") });
            }

            if (cn == "description")
            {
                spell.descriptionRawXml = child.ToString(SaveOptions.DisableFormatting);
                spell.desc = new();
                if (child.Value.Contains("At Higher Levels."))
                {
                    spell.higher_level = new();
                    int idx = child.Value.IndexOf("At Higher Levels.", StringComparison.Ordinal);
                    spell.desc.Add(child.Value[..(idx - 1)]);
                    spell.higher_level.Add(child.Value[idx..]);
                }
                else
                {
                    spell.desc.Add(child.Value);
                }
            }

            if (cn == "setters")
            {
                spell.setters = new AuroraSetters();
                FillSetters(spell.setters, child);
            }
        }

        if (spell.setters != null)
        {
            spell.url  ??= spell.setters.sourceUrl;
            if (spell.setters.level != 0) spell.level = spell.setters.level;
            if (!string.IsNullOrWhiteSpace(spell.setters.school))
                spell.school = new BaseApiClass { index = spell.setters.school.ToLower() };
            spell.casting_time   = spell.setters.time;
            spell.duration       = spell.setters.duration;
            spell.range          = spell.setters.range;
            spell.components   ??= new();
            if (spell.setters.hasVerbalComponent)   spell.components.Add("V");
            if (spell.setters.hasSomaticComponent)  spell.components.Add("S");
            if (spell.setters.hasMaterialComponent) spell.components.Add("M");
            spell.material      = spell.setters.materialComponent;
            spell.concentration = spell.setters.isConcentration;
            spell.ritual        = spell.setters.isRitual;
        }

        string desc = string.Join(" ", spell.desc ?? new()).ToLowerInvariant();
        if (desc.Contains("melee spell attack"))        spell.attack_type = "melee";
        else if (desc.Contains("ranged spell attack"))  spell.attack_type = "ranged";

        return spell;
    }

    // ── Rules parsing ────────────────────────────────────────────────────────

    private static Rules FillRules(XElement parent) => new()
    {
        grants  = parent.Elements("grant").Select(ParseGrant).ToList(),
        selects = parent.Elements("select").Select(ParseSelect).ToList(),
        stats   = parent.Elements("stat").Select(ParseStat).ToList()
    };

    private static Grant ParseGrant(XElement e) => new()
    {
        type         = e.Attribute("type")?.Value,
        id           = e.Attribute("id")?.Value,
        name         = e.Attribute("name")?.Value,
        level        = e.Attribute("level")?.Value is { } lv ? Convert.ToInt32(lv) : null,
        spellcasting = e.Attribute("spellcasting")?.Value,
        prepared     = e.Attribute("prepared")?.Value is { } p ? p == "true" : null,
        requirements = ParseTextCollection(e.Attribute("requirements")?.Value),
        rawXml       = e.ToString(SaveOptions.DisableFormatting)
    };

    private static Select ParseSelect(XElement e) => new()
    {
        type          = e.Attribute("type")?.Value,
        name          = e.Attribute("name")?.Value,
        supports      = ParseTextCollection(e.Attribute("supports")?.Value),
        level         = e.Attribute("level")?.Value is { } lv ? Convert.ToInt32(lv) : null,
        requirements  = ParseTextCollection(e.Attribute("requirements")?.Value),
        number        = e.Attribute("number")?.Value is { } n ? Convert.ToInt32(n) : 1,
        defaultChoice = e.Attribute("default")?.Value,
        optional      = ParseBool(e.Attribute("optional")?.Value) ?? false,
        spellcasting  = e.Attribute("spellcasting")?.Value,
        items         = ParseItemEntries(e),
        rawXml        = e.ToString(SaveOptions.DisableFormatting)
    };

    private static Stat ParseStat(XElement e) => new()
    {
        name         = e.Attribute("name")?.Value,
        value        = e.Attribute("value")?.Value,
        bonus        = e.Attribute("bonus")?.Value,
        equipped     = ParseTextCollection(e.Attribute("equipped")?.Value),
        level        = e.Attribute("level")?.Value is { } lv ? Convert.ToInt32(lv) : null,
        requirements = ParseTextCollection(e.Attribute("requirements")?.Value),
        inline       = ParseBool(e.Attribute("inline")?.Value) ?? false,
        alt          = e.Attribute("alt")?.Value,
        rawXml       = e.ToString(SaveOptions.DisableFormatting)
    };

    // ── Setters ──────────────────────────────────────────────────────────────

    private static void FillSetters(AuroraSetters setters, XElement parent)
    {
        var props = typeof(AuroraSetters).GetProperties().ToList();

        foreach (var setter in parent.Elements("set"))
        {
            string? setterName = setter.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(setterName)) continue;

            var entry = new AuroraSetterEntry { name = setterName, value = setter.Value };
            foreach (var attr in setter.Attributes().Where(a => a.Name.LocalName != "name"))
                entry.attributes[attr.Name.LocalName] = attr.Value;
            setters.entries.Add(entry);

            if (string.Equals(setterName, "keywords", StringComparison.OrdinalIgnoreCase))
            {
                setters.keywords = SplitTopLevel(setter.Value, ',');
                continue;
            }
            if (string.Equals(setterName, "names", StringComparison.OrdinalIgnoreCase))
            {
                setters.names ??= new();
                setters.names.Add(new Names
                {
                    type  = entry.GetAttribute("type"),
                    names = SplitTopLevel(setter.Value, ',')
                });
                continue;
            }
            if (string.Equals(setterName, "multiclass proficiencies", StringComparison.OrdinalIgnoreCase))
            {
                setters.multiclass_proficiencies = SplitTopLevel(setter.Value, ',');
                continue;
            }

            string normalizedName = setterName.Replace("-", "_").Replace(" ", "_");
            PropertyInfo? prop = props.FirstOrDefault(
                p => string.Equals(p.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (prop == null) continue;

            string content = setter.Value;
            if (prop.PropertyType == typeof(string))
            {
                prop.SetValue(setters, content);
            }
            else if (!string.IsNullOrWhiteSpace(content))
            {
                try { prop.SetValue(setters, TypeDescriptor.GetConverter(prop.PropertyType).ConvertFromString(content)); }
                catch { /* keep raw entry */ }
            }
        }
    }

    // ── Collection helpers ───────────────────────────────────────────────────

    private static AuroraTextCollection? ParseTextCollection(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var col = new AuroraTextCollection { raw = raw.Trim() };
        col.AddRange(SplitTopLevel(raw, ','));
        return col;
    }

    private static AuroraTextCollection? ParsePrerequisitesCollection(XElement el)
    {
        var nested = el.Elements("prerequisite")
            .Select(x => x.Value?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (nested.Any())
        {
            var col = new AuroraTextCollection { raw = string.Join(", ", nested) };
            col.AddRange(nested!);
            return col;
        }
        return ParseTextCollection(el.Value);
    }

    private static List<AuroraItemEntry> ParseItemEntries(XElement parent) =>
        parent.Elements("item").Select(item =>
        {
            var entry = new AuroraItemEntry { value = item.Value?.Trim() };
            foreach (var attr in item.Attributes())
                entry.attributes[attr.Name.LocalName] = attr.Value;
            return entry;
        }).ToList();

    private static AuroraBlockEntry ParseBlockEntry(XElement el)
    {
        var block = new AuroraBlockEntry
        {
            name   = el.Name.LocalName,
            value  = el.Value,
            rawXml = el.ToString(SaveOptions.DisableFormatting)
        };
        foreach (var attr in el.Attributes())
            block.attributes[attr.Name.LocalName] = attr.Value;
        return block;
    }

    private static List<string> SplitTopLevel(string input, char sep)
    {
        var values = new List<string>();
        if (string.IsNullOrWhiteSpace(input)) return values;

        int paren = 0, bracket = 0, brace = 0;
        var cur = new System.Text.StringBuilder();

        foreach (char ch in input)
        {
            switch (ch)
            {
                case '(': paren++;   break;
                case ')': paren   = Math.Max(0, paren - 1);   break;
                case '[': bracket++; break;
                case ']': bracket = Math.Max(0, bracket - 1); break;
                case '{': brace++;  break;
                case '}': brace   = Math.Max(0, brace - 1);   break;
            }
            if (ch == sep && paren == 0 && bracket == 0 && brace == 0)
            {
                string v = cur.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(v)) values.Add(v);
                cur.Clear();
                continue;
            }
            cur.Append(ch);
        }
        string last = cur.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(last)) values.Add(last);
        return values;
    }

    private static bool? ParseBool(string? value) =>
        bool.TryParse(value, out bool b) ? b : null;

    private static string? BuildSlug(string? value) =>
        value?.Trim().ToLower().Replace(" ", "-");
}
