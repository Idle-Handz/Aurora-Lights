using Aurora.Components.Models;
using System.Xml;

namespace Aurora.Web.Services;

/// <summary>Persists preparation by stable profile identity, never across unrelated profiles.</summary>
public static class WebMagicStatePersistence
{
    public static void ToggleSlot(MagicOverviewModel model, int levelNumber, int oneBasedSlot)
    {
        var level = model.SpellLevels.FirstOrDefault(candidate => candidate.Level == levelNumber);
        if (level is null) return;
        level.UsedSlots = Math.Clamp(level.UsedSlots, 0, Math.Max(0, level.TotalSlots));
        if (oneBasedSlot < 1 || oneBasedSlot > level.TotalSlots) return;
        level.UsedSlots = level.UsedSlots == oneBasedSlot ? oneBasedSlot - 1 : oneBasedSlot;
    }

    public static void Load(string path, MagicOverviewModel model)
    {
        var document = new XmlDocument();
        document.Load(path);
        foreach (var section in model.Sections)
        {
            var node = FindProfile(document, model, section);
            if (node is null) continue;
            var spells = node.SelectNodes("spells/spell")!.Cast<XmlElement>().ToList();
            foreach (var spell in section.SpellLevels.SelectMany(level => level.Spells))
            {
                var saved = FindSpell(spells, spell);
                if (saved is not null)
                    spell.IsPrepared = spell.IsAlwaysPrepared || saved.GetAttribute("prepared").Equals("true", StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var level in model.SpellLevels)
        {
            var slot = document.DocumentElement?["session"]?["spellslots"]?.SelectSingleNode($"slot[@level='{level.Level}']") as XmlElement;
            if (int.TryParse(slot?.GetAttribute("used"), out int used))
                level.UsedSlots = Math.Clamp(used, 0, Math.Max(0, level.TotalSlots));
        }
        Refresh(model);
    }

    public static void Copy(MagicOverviewModel source, MagicOverviewModel target)
    {
        foreach (var section in target.Sections)
        {
            var old = source.Sections.SingleOrDefault(candidate => Same(candidate.Id, section.Id));
            if (old is null) continue;
            var byId = old.SpellLevels.SelectMany(level => level.Spells)
                .GroupBy(spell => spell.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().IsPrepared, StringComparer.OrdinalIgnoreCase);
            foreach (var spell in section.SpellLevels.SelectMany(level => level.Spells))
                if (byId.TryGetValue(spell.Id, out bool prepared)) spell.IsPrepared = spell.IsAlwaysPrepared || prepared;
        }
        foreach (var level in target.SpellLevels)
        {
            var old = source.SpellLevels.FirstOrDefault(candidate => candidate.Level == level.Level);
            if (old is not null) level.UsedSlots = Math.Clamp(old.UsedSlots, 0, Math.Max(0, level.TotalSlots));
        }
        Refresh(target);
    }

    public static void Save(string path, MagicOverviewModel model)
    {
        var document = new XmlDocument();
        document.Load(path);
        var root = document.DocumentElement ?? throw new InvalidOperationException("Character file has no root element.");
        var magicNode = Child(document, Child(document, root, "build"), "magic");
        foreach (var section in model.Sections)
        {
            var node = FindProfile(document, model, section);
            if (node is null)
            {
                node = document.CreateElement("spellcasting");
                node.SetAttribute("name", section.Label);
                int separator = section.Id.IndexOf('|');
                if (separator >= 0) node.SetAttribute("source", section.Id[..separator]);
                magicNode.AppendChild(node);
            }
            var spellsNode = Child(document, node, "spells");
            var savedSpells = spellsNode.ChildNodes.OfType<XmlElement>().Where(child => child.Name == "spell").ToList();
            foreach (var spell in section.SpellLevels.SelectMany(level => level.Spells))
            {
                var saved = FindSpell(savedSpells, spell);
                if (saved is null)
                {
                    if (!spell.IsPrepared && !spell.IsAlwaysPrepared) continue;
                    saved = document.CreateElement("spell");
                    saved.SetAttribute("id", spell.Id);
                    saved.SetAttribute("name", spell.Name);
                    spellsNode.AppendChild(saved);
                    savedSpells.Add(saved);
                }
                if (spell.IsPrepared || spell.IsAlwaysPrepared) saved.SetAttribute("prepared", "true");
                else saved.RemoveAttribute("prepared");
            }
        }

        var slots = Child(document, Child(document, root, "session"), "spellslots");
        slots.RemoveAll();
        foreach (var level in model.SpellLevels)
        {
            level.UsedSlots = Math.Clamp(level.UsedSlots, 0, Math.Max(0, level.TotalSlots));
            if (level.UsedSlots == 0) continue;
            var slot = document.CreateElement("slot");
            slot.SetAttribute("level", level.Level.ToString());
            slot.SetAttribute("used", level.UsedSlots.ToString());
            slots.AppendChild(slot);
        }
        string temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            document.Save(temporaryPath);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        Refresh(model);
    }

    public static void Refresh(MagicOverviewModel model)
    {
        foreach (var section in model.Sections)
            section.PreparedCount = section.SpellLevels.SelectMany(level => level.Spells)
                .Count(spell => spell.IsPrepared && !spell.IsAlwaysPrepared);
        MagicSpellAccessClassifier.Apply(model);
    }

    private static XmlElement? FindProfile(XmlDocument document, MagicOverviewModel model, MagicSpellcastingSectionModel section)
    {
        var nodes = document.DocumentElement?["build"]?["magic"]?.ChildNodes.OfType<XmlElement>()
            .Where(node => node.Name == "spellcasting").ToList() ?? [];
        var exact = nodes.FirstOrDefault(node => Same($"{node.GetAttribute("source")}|{node.GetAttribute("name")}", section.Id));
        if (exact is not null) return exact;
        // Old files may omit source; names are usable only when they identify one profile.
        if (model.Sections.Count(candidate => Same(candidate.Label, section.Label)) != 1) return null;
        return nodes.SingleOrDefault(node => string.IsNullOrEmpty(node.GetAttribute("source"))
            && Same(node.GetAttribute("name"), section.Label));
    }

    private static XmlElement? FindSpell(IReadOnlyList<XmlElement> nodes, MagicSpellListEntryModel spell) =>
        nodes.FirstOrDefault(node => Same(node.GetAttribute("id"), spell.Id))
        ?? nodes.FirstOrDefault(node => string.IsNullOrEmpty(node.GetAttribute("id")) && Same(node.GetAttribute("name"), spell.Name));

    private static XmlElement Child(XmlDocument document, XmlElement parent, string name)
    {
        if (parent[name] is { } existing) return existing;
        var node = document.CreateElement(name);
        parent.AppendChild(node);
        return node;
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
