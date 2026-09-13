using Microsoft.Data.Sqlite;
using System.Xml;

namespace Aurora.Importer;

/// <summary>Reads the complete spellcasting declarations preserved by Translator data version 11.</summary>
public static class TranslatorSpellcastingReader
{
    public static Dictionary<long, XmlElement> ReadProfiles(SqliteConnection connection)
    {
        var profiles = new Dictionary<long, XmlElement>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT owner_element_id, raw_xml FROM spellcasting_profiles;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            long owner = reader.GetInt64(0);
            if (reader.IsDBNull(1) || string.IsNullOrWhiteSpace(reader.GetString(1)))
                throw new InvalidDataException($"Spellcasting for element {owner} requires an XML reimport. Refresh with AuroraTranslator.");

            var document = new XmlDocument { XmlResolver = null };
            using var input = new StringReader(reader.GetString(1));
            using var xmlReader = XmlReader.Create(input, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            document.Load(xmlReader);
            if (document.DocumentElement is not { Name: "spellcasting", NamespaceURI: "" } profile)
                throw new InvalidDataException($"Invalid spellcasting XML for element {owner}.");
            profiles.Add(owner, profile);
        }
        return profiles;
    }
}
