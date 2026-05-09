using System.Text;
using System.Xml;
using Builder.Presentation;
using Builder.Presentation.Models;

namespace Aurora.App.Services;

/// <summary>
/// Extension methods that patch text-editable fields back to a character XML
/// file without requiring a full CharacterManager round-trip.
/// </summary>
public static class CharacterFileSaveExtensions
{
    /// <summary>
    /// Patches only the currency values in the character XML from the snapshot.
    /// Used by the Session page so coin edits can persist without implicitly
    /// saving unrelated pending text edits from other pages.
    /// </summary>
    public static bool SaveCurrency(this CharacterFile file, CharacterSnapshot snap)
    {
        var path = file.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var doc = new XmlDocument();
            doc.Load(path);

            var currency = doc.DocumentElement?["build"]?["input"]?["currency"];
            if (currency == null) return false;

            SetText(currency, "copper",   snap.CoinCopper.ToString());
            SetText(currency, "silver",   snap.CoinSilver.ToString());
            SetText(currency, "electrum", snap.CoinElectrum.ToString());
            SetText(currency, "gold",     snap.CoinGold.ToString());
            SetText(currency, "platinum", snap.CoinPlatinum.ToString());

            using var writer = new XmlTextWriter(path, Encoding.UTF8)
            {
                Formatting = Formatting.Indented,
                IndentChar = '\t',
                Indentation = 1,
            };
            doc.Save(writer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Patches every text-editable node in the character XML with values from
    /// the snapshot, then saves the file. Calculated and element-derived fields
    /// are left untouched.
    /// </summary>
    public static bool SaveTextEdits(this CharacterFile file, CharacterSnapshot snap)
    {
        var path = file.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        var doc = new XmlDocument();
        doc.Load(path);

        var root = doc.DocumentElement;
        if (root == null) return false;

        var buildNode = root["build"];
        if (buildNode == null) return false;

        // ── input node ──────────────────────────────────────────────────────
        var inputNode = buildNode["input"];
        if (inputNode != null)
        {
            SetText(inputNode, "name",               snap.Name);
            SetText(inputNode, "player-name",        snap.PlayerName);
            SetText(inputNode, "gender",             snap.Gender);
            SetText(inputNode, "experience",         snap.Experience.ToString());
            SetText(inputNode, "backstory",          snap.Backstory);
            SetText(inputNode, "background-trinket", snap.Trinket);

            var currency = inputNode["currency"];
            if (currency != null)
            {
                SetText(currency, "copper",    snap.CoinCopper.ToString());
                SetText(currency, "silver",    snap.CoinSilver.ToString());
                SetText(currency, "electrum",  snap.CoinElectrum.ToString());
                SetText(currency, "gold",      snap.CoinGold.ToString());
                SetText(currency, "platinum",  snap.CoinPlatinum.ToString());
                SetText(currency, "equipment", snap.InventoryEquipmentText);
                SetText(currency, "treasure",  snap.InventoryTreasureText);
            }

            var org = inputNode["organization"];
            if (org != null)
            {
                SetText(org, "name",   snap.Organisation);
                SetText(org, "allies", snap.Allies);
            }

            // notes
            var notesNode = inputNode["notes"];
            if (notesNode != null)
            {
                foreach (XmlNode note in notesNode.ChildNodes)
                {
                    if (note.Name != "note") continue;
                    var col = note.Attributes?["column"]?.Value;
                    if (col == "left")  note.InnerText = snap.Notes1;
                    if (col == "right") note.InnerText = snap.Notes2;
                }
            }

            var quest = inputNode["quest"];
            if (quest != null)
                quest.InnerText = snap.InventoryQuestText;
        }

        // ── appearance node ─────────────────────────────────────────────────
        var appearance = buildNode["appearance"];
        if (appearance != null)
        {
            SetText(appearance, "age",    snap.Age);
            SetText(appearance, "height", snap.Height);
            SetText(appearance, "weight", snap.Weight);
            SetText(appearance, "eyes",   snap.Eyes);
            SetText(appearance, "skin",   snap.Skin);
            SetText(appearance, "hair",   snap.Hair);
        }

        // ── spell prepared state (prepared casters only) ─────────────────────
        // Only write prepared state for Cleric/Druid/Wizard/Paladin/Artificer etc.
        // Known casters (Sorcerer/Bard/etc.) have no per-spell prepared toggle.
        if (snap.SpellcastingSections.Count > 0)
        {
            var magic = buildNode["magic"];
            if (magic != null)
            {
                foreach (XmlNode spellcasting in magic.ChildNodes)
                {
                    if (spellcasting.Name != "spellcasting") continue;

                    var spellcastingName = spellcasting.Attributes?["name"]?.Value ?? "";
                    var spellcastingSource = spellcasting.Attributes?["source"]?.Value ?? "";
                    var section = snap.SpellcastingSections.FirstOrDefault(candidate =>
                        string.Equals(candidate.Name, spellcastingName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(candidate.SourceId, spellcastingSource, StringComparison.OrdinalIgnoreCase));

                    section ??= snap.SpellcastingSections.FirstOrDefault(candidate =>
                        string.Equals(candidate.Name, spellcastingName, StringComparison.OrdinalIgnoreCase));

                    if (section is null || !section.IsPreparedCaster || section.SpellLevels.Count == 0)
                        continue;

                    // Build lookups by both Id and Name so we can match against whatever the XML uses.
                    var preparedById = section.SpellLevels.SelectMany(lvl => lvl.Spells)
                        .Where(s => !string.IsNullOrEmpty(s.Id))
                        .ToDictionary(s => s.Id, s => s.IsPrepared, StringComparer.OrdinalIgnoreCase);
                    var preparedByName = section.SpellLevels.SelectMany(lvl => lvl.Spells)
                        .ToDictionary(s => s.Name, s => s.IsPrepared, StringComparer.OrdinalIgnoreCase);

                    var spellsNode = spellcasting["spells"];
                    if (spellsNode == null) continue;
                    foreach (XmlNode spellNode in spellsNode.ChildNodes)
                    {
                        if (spellNode.Name != "spell") continue;

                        // Prefer matching by element ID; fall back to name.
                        var spellId   = spellNode.Attributes?["id"]?.Value;
                        var spellName = spellNode.Attributes?["name"]?.Value;
                        bool isPrepared;
                        if (spellId != null && preparedById.TryGetValue(spellId, out isPrepared))
                        { /* matched by ID */ }
                        else if (spellName != null && preparedByName.TryGetValue(spellName, out isPrepared))
                        { /* matched by name */ }
                        else continue;

                        // Don't toggle always-prepared spells (domain spells etc.).
                        var alwaysPrepared = spellNode.Attributes?["always-prepared"]?.Value;
                        if (alwaysPrepared == "true") continue;

                        var preparedAttr = spellNode.Attributes?["prepared"];
                        if (isPrepared)
                        {
                            if (preparedAttr == null)
                            {
                                var a = doc.CreateAttribute("prepared");
                                a.Value = "true";
                                spellNode.Attributes!.Append(a);
                            }
                            else
                            {
                                preparedAttr.Value = "true";
                            }
                        }
                        else
                        {
                            if (preparedAttr != null)
                                spellNode.Attributes!.Remove(preparedAttr);
                        }
                    }
                }
            }
        }

        // ── write ────────────────────────────────────────────────────────────
        using var writer = new XmlTextWriter(path, Encoding.UTF8)
        {
            Formatting  = Formatting.Indented,
            IndentChar  = '\t',
            Indentation = 1,
        };
        doc.Save(writer);
        return true;
    }

    /// <summary>
    /// Replaces the &lt;equipment&gt; XML node with the current live inventory state.
    /// Call this after adding or removing items from <c>character.Inventory.Items</c>.
    /// </summary>
    public static bool SaveInventoryItems(this CharacterFile file, Builder.Presentation.Models.Character character)
    {
        var path = file.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        var doc = new XmlDocument();
        doc.Load(path);

        var buildNode = doc.DocumentElement?["build"];
        if (buildNode == null) return false;

        // Remove old equipment node if present
        var oldEquip = buildNode["equipment"];
        if (oldEquip != null)
            buildNode.RemoveChild(oldEquip);

        // Build replacement node
        var equipNode = doc.CreateElement("equipment");

        foreach (var inv in character.Inventory.Items)
        {
            var itemNode = doc.CreateElement("item");
            itemNode.SetAttribute("identifier", inv.Identifier);
            itemNode.SetAttribute("name",       inv.Name ?? "");
            itemNode.SetAttribute("id",         inv.Item?.Id ?? "");

            if (inv.Amount > 1)
                itemNode.SetAttribute("amount", inv.Amount.ToString());
            if (!inv.IncludeInEquipmentPageInventory)
                itemNode.SetAttribute("hidden", "true");
            if (inv.IncludeInEquipmentPageDescriptionSidebar)
                itemNode.SetAttribute("sidebar", "true");

            if (inv.IsEquipped)
            {
                var equippedNode = doc.CreateElement("equipped");
                equippedNode.InnerText = "true";
                if (!string.IsNullOrWhiteSpace(inv.EquippedLocation))
                    equippedNode.SetAttribute("location", inv.EquippedLocation);
                itemNode.AppendChild(equippedNode);
            }
            if (inv.IsAttuned)
            {
                var attunNode = doc.CreateElement("attunement");
                attunNode.InnerText = "true";
                itemNode.AppendChild(attunNode);
            }

            // Details (custom name + notes)
            var detailsNode = doc.CreateElement("details");
            detailsNode.SetAttribute("card", inv.ShowCard ? "true" : "false");
            var nameNode  = doc.CreateElement("name");
            nameNode.InnerText  = inv.AlternativeName ?? "";
            var notesNode = doc.CreateElement("notes");
            notesNode.InnerText = inv.Notes ?? "";
            detailsNode.AppendChild(nameNode);
            detailsNode.AppendChild(notesNode);
            itemNode.AppendChild(detailsNode);

            equipNode.AppendChild(itemNode);
        }

        buildNode.AppendChild(equipNode);

        using var writer = new XmlTextWriter(path, Encoding.UTF8)
        {
            Formatting  = Formatting.Indented,
            IndentChar  = '\t',
            Indentation = 1,
        };
        doc.Save(writer);
        return true;
    }

    // ── Session persistence ──────────────────────────────────────────────────

    /// <summary>
    /// Reads the &lt;session&gt; node from the character XML and returns a populated
    /// <see cref="SessionState"/>. Returns a fresh (default) SessionState when no
    /// session node is present (first open, or file saved by WPF Aurora Builder).
    /// </summary>
    public static SessionState LoadSession(this CharacterFile file)
    {
        var path = file.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new SessionState();

        try
        {
            var doc = new XmlDocument();
            doc.Load(path);

            var session = doc.DocumentElement?["session"];
            if (session == null) return new SessionState();

            var state = new SessionState
            {
                CurrentHp          = ParseInt(session["currenthp"]?.InnerText,  -1),
                TempHp             = ParseInt(session["temphp"]?.InnerText,       0),
                DeathSaveSuccesses = ParseInt(session["deathsave-successes"]?.InnerText, 0),
                DeathSaveFailures  = ParseInt(session["deathsave-failures"]?.InnerText,  0),
                Inspiration        = session["inspiration"]?.InnerText == "true",
                Exhaustion         = ParseInt(session["exhaustion"]?.InnerText, 0),
            };

            // Conditions
            var condNode = session["conditions"];
            if (condNode != null)
                foreach (XmlNode c in condNode.ChildNodes)
                    if (c.Name == "condition" && !string.IsNullOrWhiteSpace(c.InnerText))
                        state.Conditions.Add(c.InnerText);

            // Spell slots used
            var slotsNode = session["spellslots"];
            if (slotsNode != null)
                foreach (XmlNode s in slotsNode.ChildNodes)
                    if (s.Name == "slot" && int.TryParse(s.Attributes?["level"]?.Value, out var lvl))
                        state.SpellSlotsUsed[lvl] = ParseInt(s.Attributes?["used"]?.Value, 0);

            // Custom resources
            var resourcesNode = session["resources"];
            if (resourcesNode != null)
                foreach (XmlNode r in resourcesNode.ChildNodes)
                {
                    if (r.Name != "resource") continue;
                    state.CustomResources.Add(new CustomResource
                    {
                        Id      = r.Attributes?["id"]?.Value    ?? Guid.NewGuid().ToString("N")[..8],
                        Name    = r.Attributes?["name"]?.Value  ?? "",
                        Max     = ParseInt(r.Attributes?["max"]?.Value,  1),
                        Used    = ParseInt(r.Attributes?["used"]?.Value, 0),
                        ResetOn = Enum.TryParse<ResetOn>(r.Attributes?["reset"]?.Value, out var ro) ? ro : ResetOn.LongRest,
                    });
                }

            return state;
        }
        catch
        {
            return new SessionState();
        }
    }

    /// <summary>
    /// Writes (or replaces) the &lt;session&gt; node in the character XML.
    /// The node is a sibling of &lt;build&gt; so the WPF Aurora Builder ignores it.
    /// </summary>
    public static bool SaveSession(this CharacterFile file, SessionState session)
    {
        var path = file.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var doc = new XmlDocument();
            doc.Load(path);

            var root = doc.DocumentElement;
            if (root == null) return false;

            // Remove existing session node
            var old = root["session"];
            if (old != null) root.RemoveChild(old);

            var node = doc.CreateElement("session");

            AppendText(doc, node, "currenthp",           session.CurrentHp.ToString());
            AppendText(doc, node, "temphp",              session.TempHp.ToString());
            AppendText(doc, node, "deathsave-successes", session.DeathSaveSuccesses.ToString());
            AppendText(doc, node, "deathsave-failures",  session.DeathSaveFailures.ToString());
            AppendText(doc, node, "inspiration",         session.Inspiration ? "true" : "false");
            AppendText(doc, node, "exhaustion",          session.Exhaustion.ToString());

            // Conditions
            var condNode = doc.CreateElement("conditions");
            foreach (var c in session.Conditions)
            {
                var cn = doc.CreateElement("condition");
                cn.InnerText = c;
                condNode.AppendChild(cn);
            }
            node.AppendChild(condNode);

            // Spell slots
            var slotsNode = doc.CreateElement("spellslots");
            foreach (var (lvl, used) in session.SpellSlotsUsed)
            {
                var sn = doc.CreateElement("slot");
                sn.SetAttribute("level", lvl.ToString());
                sn.SetAttribute("used",  used.ToString());
                slotsNode.AppendChild(sn);
            }
            node.AppendChild(slotsNode);

            // Custom resources
            var resNode = doc.CreateElement("resources");
            foreach (var r in session.CustomResources)
            {
                var rn = doc.CreateElement("resource");
                rn.SetAttribute("id",    r.Id);
                rn.SetAttribute("name",  r.Name);
                rn.SetAttribute("max",   r.Max.ToString());
                rn.SetAttribute("used",  r.Used.ToString());
                rn.SetAttribute("reset", r.ResetOn.ToString());
                resNode.AppendChild(rn);
            }
            node.AppendChild(resNode);

            root.AppendChild(node);

            using var writer = new XmlTextWriter(path, Encoding.UTF8)
            {
                Formatting  = Formatting.Indented,
                IndentChar  = '\t',
                Indentation = 1,
            };
            doc.Save(writer);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the &lt;sources&gt; XML node with the current restricted-sources state
    /// from <c>CharacterManager.Current.SourcesManager</c>.
    /// The node sits at root level (sibling of &lt;build&gt;) so the WPF builder reads it.
    /// Call this after toggling sources via <c>SourcesManager.ApplyRestrictions()</c>.
    /// </summary>
    public static bool SaveSourceRestrictions(this CharacterFile file)
    {
        var path = file.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var doc = new XmlDocument();
            doc.Load(path);

            var root = doc.DocumentElement;
            if (root == null) return false;

            var sm = CharacterManager.Current?.SourcesManager;
            if (sm == null) return false;

            // Replace the existing <sources> node
            var old = root["sources"];
            if (old != null) root.RemoveChild(old);

            var sourcesNode   = doc.CreateElement("sources");
            var restrictedNode = doc.CreateElement("restricted");

            foreach (var item in sm.RestrictedSources)
            {
                var el = doc.CreateElement("source");
                el.InnerText = item.Source.Name;
                el.SetAttribute("id", item.Source.Id);
                restrictedNode.AppendChild(el);
            }
            foreach (var elementId in sm.GetRestrictedElementIds())
            {
                var el = doc.CreateElement("element");
                el.InnerText = elementId;
                restrictedNode.AppendChild(el);
            }

            sourcesNode.AppendChild(restrictedNode);
            root.AppendChild(sourcesNode);

            using var writer = new XmlTextWriter(path, Encoding.UTF8)
            {
                Formatting  = Formatting.Indented,
                IndentChar  = '\t',
                Indentation = 1,
            };
            doc.Save(writer);
            return true;
        }
        catch { return false; }
    }

    private static int ParseInt(string? s, int fallback) =>
        int.TryParse(s, out var v) ? v : fallback;

    private static void AppendText(XmlDocument doc, XmlNode parent, string name, string value)
    {
        var el = doc.CreateElement(name);
        el.InnerText = value;
        parent.AppendChild(el);
    }

    private static void SetText(XmlNode parent, string childName, string? value)
    {
        var node = parent[childName];
        if (node != null)
            node.InnerText = value ?? "";
    }
}
