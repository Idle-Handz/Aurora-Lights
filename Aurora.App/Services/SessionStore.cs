using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using Builder.Presentation.Utilities;

namespace Aurora.App.Services;

/// <summary>
/// Owns persistence of per-character session state (HP, conditions, spell slots,
/// custom resources, attack reminders) as a JSON sidecar next to the character file:
/// <c>Name.dnd5e</c> -> <c>Name.dnd5e.session.json</c>.
///
/// Why a sidecar instead of a &lt;session&gt; node inside the character XML:
/// <list type="bullet">
/// <item><c>CharacterFile.Save()</c> - and the legacy WPF builder's save - rebuild the XML
/// from the model and drop unknown root nodes, so embedded session state was destroyed by
/// any full save that didn't explicitly re-write it.</item>
/// <item>Session state is written on every HP pip click; patching it into the XML rewrote
/// the entire character file each time.</item>
/// </list>
///
/// Older files that still carry an embedded &lt;session&gt; node are migrated on first
/// load: the node is read once and the sidecar written. The stale node is then ignored
/// (sidecar always wins) and disappears naturally on the next full save.
///
/// File lifecycle: code that deletes, renames, or moves a character file must call
/// <see cref="Delete"/> or <see cref="MoveAlongside"/> so the sidecar follows.
/// Export/share intentionally does NOT bundle the sidecar - session state is
/// device-local play state, not part of the shareable character.
/// </summary>
public static class SessionStore
{
    private const string SidecarSuffix = ".session.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string GetSidecarPath(string characterFilePath) =>
        characterFilePath + SidecarSuffix;

    public static SessionState Load(string characterFilePath) =>
        Load(characterFilePath, out _);

    /// <summary>
    /// Loads session state for a character file. Prefers the JSON sidecar; falls back to a
    /// legacy embedded &lt;session&gt; node (migrating it to a sidecar) only when no sidecar
    /// exists. <paramref name="sessionCorrupted"/> is true when stored data was present but
    /// unreadable - the caller should warn that HP/conditions/slots were reset.
    /// </summary>
    public static SessionState Load(string characterFilePath, out bool sessionCorrupted)
    {
        sessionCorrupted = false;
        if (string.IsNullOrWhiteSpace(characterFilePath))
            return new SessionState();

        string sidecarPath = GetSidecarPath(characterFilePath);
        if (File.Exists(sidecarPath))
        {
            try
            {
                // LoadTextFile retries transient locks (sync clients, AV scanners) with backoff,
                // so reaching the catch below means the sidecar is genuinely unreadable.
                var state = JsonSerializer.Deserialize<SessionState>(
                    CharacterFileIo.LoadTextFile(sidecarPath), JsonOptions);
                if (state != null)
                    return state;
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex, "SessionStore.Load");
            }

            // A sidecar existed but couldn't be read. Don't fall back to an embedded node:
            // any node still in the XML predates the sidecar and would resurrect stale data.
            sessionCorrupted = true;
            return new SessionState();
        }

        return LoadFromEmbeddedNode(characterFilePath, out sessionCorrupted);
    }

    /// <summary>
    /// Writes the sidecar atomically (temp file + move). Returns false when the character
    /// file itself doesn't exist (avoids creating orphaned sidecars) or the write fails.
    /// </summary>
    public static bool Save(string characterFilePath, SessionState state)
    {
        if (string.IsNullOrWhiteSpace(characterFilePath) || !File.Exists(characterFilePath))
            return false;

        try
        {
            // Atomic temp+move write with transient-lock retries, matching the .dnd5e itself.
            CharacterFileIo.SaveTextFileAtomic(
                GetSidecarPath(characterFilePath),
                JsonSerializer.Serialize(state, JsonOptions));
            return true;
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "SessionStore.Save");
            return false;
        }
    }

    /// <summary>Deletes the sidecar (best effort). Call when the character file is deleted.</summary>
    public static void Delete(string characterFilePath)
    {
        if (string.IsNullOrWhiteSpace(characterFilePath))
            return;

        try
        {
            string sidecarPath = GetSidecarPath(characterFilePath);
            if (File.Exists(sidecarPath))
                File.Delete(sidecarPath);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "SessionStore.Delete");
        }
    }

    /// <summary>
    /// Moves the sidecar to follow a renamed or moved character file (best effort).
    /// Any rename/move flow must call this after relocating the .dnd5e itself.
    /// </summary>
    public static void MoveAlongside(string oldCharacterFilePath, string newCharacterFilePath)
    {
        if (string.IsNullOrWhiteSpace(oldCharacterFilePath) ||
            string.IsNullOrWhiteSpace(newCharacterFilePath) ||
            string.Equals(oldCharacterFilePath, newCharacterFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            string oldSidecar = GetSidecarPath(oldCharacterFilePath);
            if (File.Exists(oldSidecar))
                File.Move(oldSidecar, GetSidecarPath(newCharacterFilePath), overwrite: true);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex, "SessionStore.MoveAlongside");
        }
    }

    // Legacy <session> node migration.
    private static SessionState LoadFromEmbeddedNode(string characterFilePath, out bool sessionCorrupted)
    {
        sessionCorrupted = false;
        if (!File.Exists(characterFilePath))
            return new SessionState();

        try
        {
            var doc = CharacterFileIo.LoadXmlDocument(characterFilePath);
            var session = doc.DocumentElement?["session"];
            if (session == null)
                return new SessionState();

            var state = ParseSessionNode(session);

            // One-time migration: persist the node's data as the sidecar so it survives
            // the next full save (which drops unknown root nodes from the XML).
            Save(characterFilePath, state);
            return state;
        }
        catch (Exception ex)
        {
            // A corrupt or unreadable <session> node degrades to a fresh state rather than
            // blocking the character; flag it so the caller can warn the player.
            DebugLogService.Instance.LogException(ex, "SessionStore.LoadFromEmbeddedNode");
            sessionCorrupted = true;
            return new SessionState();
        }
    }

    private static SessionState ParseSessionNode(XmlElement session)
    {
        var state = new SessionState
        {
            CurrentHp          = ParseInt(session["currenthp"]?.InnerText, -1),
            TempHp             = ParseInt(session["temphp"]?.InnerText, 0),
            DeathSaveSuccesses = ParseInt(session["deathsave-successes"]?.InnerText, 0),
            DeathSaveFailures  = ParseInt(session["deathsave-failures"]?.InnerText, 0),
            Inspiration        = session["inspiration"]?.InnerText == "true",
            Exhaustion         = ParseInt(session["exhaustion"]?.InnerText, 0),
        };

        var condNode = session["conditions"];
        if (condNode != null)
            foreach (XmlNode c in condNode.ChildNodes)
                if (c.Name == "condition" && !string.IsNullOrWhiteSpace(c.InnerText))
                    state.Conditions.Add(c.InnerText);

        var slotsNode = session["spellslots"];
        if (slotsNode != null)
            foreach (XmlNode s in slotsNode.ChildNodes)
                if (s.Name == "slot" && int.TryParse(s.Attributes?["level"]?.Value, out var lvl))
                    state.SpellSlotsUsed[lvl] = ParseInt(s.Attributes?["used"]?.Value, 0);

        var resourcesNode = session["resources"];
        if (resourcesNode != null)
            foreach (XmlNode r in resourcesNode.ChildNodes)
            {
                if (r.Name != "resource") continue;
                state.CustomResources.Add(new CustomResource
                {
                    Id      = r.Attributes?["id"]?.Value    ?? Guid.NewGuid().ToString("N")[..8],
                    Name    = r.Attributes?["name"]?.Value  ?? "",
                    Max     = ParseInt(r.Attributes?["max"]?.Value, 1),
                    Used    = ParseInt(r.Attributes?["used"]?.Value, 0),
                    ResetOn = Enum.TryParse<ResetOn>(r.Attributes?["reset"]?.Value, out var ro) ? ro : ResetOn.LongRest,
                });
            }

        var attackRemindersNode = session["attack-reminders"];
        if (attackRemindersNode != null)
        {
            foreach (XmlNode reminderNode in attackRemindersNode.ChildNodes)
            {
                if (reminderNode.Name == "weapon")
                {
                    var id = reminderNode.Attributes?["identifier"]?.Value;
                    if (!string.IsNullOrWhiteSpace(id))
                        state.AttackReminderWeaponIds.Add(id);
                }
                else if (reminderNode.Name == "hidden-default")
                {
                    var key = reminderNode.Attributes?["key"]?.Value;
                    if (!string.IsNullOrWhiteSpace(key))
                        state.HiddenDefaultAttackReminderKeys.Add(key);
                }
            }
        }

        return state;
    }

    private static int ParseInt(string? s, int fallback) =>
        int.TryParse(s, out var v) ? v : fallback;
}
