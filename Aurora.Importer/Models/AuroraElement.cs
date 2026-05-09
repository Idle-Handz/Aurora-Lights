namespace Aurora.Importer.Models;

class AuroraElement
{
    public string? name { get; set; }
    public string? type { get; set; }
    public string? source { get; set; }
    public string? id { get; set; }
    public string? index { get; set; }
    public string? source_file_path { get; set; }

    public Compendium compendium { get; set; } = new();
    public AuroraTextCollection? supports { get; set; }
    public AuroraTextCollection? requirements { get; set; }
    public AuroraTextCollection? prerequisites { get; set; }
    public string? prerequisite { get; set; }
    public string? description { get; set; }
    public string? descriptionRawXml { get; set; }
    public AuroraExtract? extract { get; set; }
    public List<AuroraBlockEntry>? additionalBlocks { get; set; }
    public AuroraSheet? sheet { get; set; }
    public AuroraSetters? setters { get; set; }
    public Spellcasting? spellcasting { get; set; }
    public Multiclass? multiclass { get; set; }
    public Rules? rules { get; set; }
}

public class Multiclass
{
    public string? id { get; set; }
    public string? prerequisite { get; set; }
    public AuroraTextCollection? requirements { get; set; }
    public AuroraSetters? setters { get; set; }
    public Rules? rules { get; set; }
}

public class Rules
{
    public List<Grant>? grants { get; set; }
    public List<Select>? selects { get; set; }
    public List<Stat>? stats { get; set; }
}

public class Stat
{
    public string? name { get; set; }
    public string? value { get; set; }
    public string? bonus { get; set; }
    public AuroraTextCollection? equipped { get; set; }
    public int? level { get; set; }
    public AuroraTextCollection? requirements { get; set; }
    public bool inline { get; set; }
    public string? alt { get; set; }
    public string? rawXml { get; set; }
}

public class Select
{
    public string? type { get; set; }
    public string? name { get; set; }
    public AuroraTextCollection? supports { get; set; }
    public int? level { get; set; }
    public AuroraTextCollection? requirements { get; set; }
    public int number { get; set; }
    public string? defaultChoice { get; set; }
    public bool optional { get; set; }
    public string? spellcasting { get; set; }
    public List<AuroraItemEntry>? items { get; set; }
    public string? rawXml { get; set; }
}

public class Grant
{
    public string? type { get; set; }
    public string? id { get; set; }
    public string? name { get; set; }
    public int? level { get; set; }
    public string? spellcasting { get; set; }
    public bool? prepared { get; set; }
    public AuroraTextCollection? requirements { get; set; }
    public string? rawXml { get; set; }
}

public class Spellcasting
{
    public string? name { get; set; }
    public string? ability { get; set; }
    public AuroraTextCollection? list { get; set; }
    public bool extend { get; set; }
    public AuroraTextCollection? extendList { get; set; }
    public bool? prepare { get; set; }
    public bool? allowReplace { get; set; }
}

public class Compendium
{
    public bool display { get; set; } = true;
}

public class AuroraSheet
{
    public bool display { get; set; } = true;
    public List<Description>? description { get; set; }
    public string? rawXml { get; set; }
    public string? alt { get; set; }
    public string? action { get; set; }
    public string? usage { get; set; }
}

public class Description
{
    public int? level { get; set; }
    public string? text { get; set; }
    public string? rawXml { get; set; }
}

public class AuroraExtract
{
    public string? description { get; set; }
    public List<AuroraItemEntry> items { get; set; } = new();
}
