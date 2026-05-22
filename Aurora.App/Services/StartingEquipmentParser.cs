using System.Xml;
using Aurora.Components.Models;

namespace Aurora.App.Services;

/// <summary>Parses a &lt;starting-equipment&gt; XmlNode into a StartingEquipmentBlock.</summary>
public static class StartingEquipmentParser
{
    /// <summary>
    /// Parses the element's &lt;starting-equipment&gt; child node.
    /// Returns <see cref="StartingEquipmentBlock.Empty"/> when the node is absent or empty.
    /// </summary>
    public static StartingEquipmentBlock Parse(XmlNode? elementNode)
    {
        XmlNode? node = elementNode?["starting-equipment"];
        if (node == null)
            return StartingEquipmentBlock.Empty;

        GoldAlternative? goldAlt = null;
        var choices    = new List<EquipmentChoice>();
        var fixedItems = new List<EquipmentItem>();
        int fixedGold  = 0;
        var fixedCoins = CoinGrant.Empty;

        foreach (XmlNode child in node.ChildNodes)
        {
            switch (child.Name)
            {
                case "gold-alternative":
                    goldAlt = ParseGoldAlternative(child);
                    break;

                case "choice":
                    var choice = ParseChoice(child);
                    if (choice != null)
                        choices.Add(choice);
                    break;

                case "item":
                    var item = ParseItem(child);
                    if (item != null)
                        fixedItems.Add(item);
                    break;

                case "gold":
                    int gold = ParseInt(child.Attributes?["amount"]?.Value, 0);
                    fixedGold += gold;
                    fixedCoins = fixedCoins.AddGold(gold);
                    break;

                case "coins":
                    fixedCoins = fixedCoins.Add(ParseCoins(child));
                    break;
            }
        }

        return new StartingEquipmentBlock
        {
            GoldAlternative = goldAlt,
            Choices         = choices,
            FixedItems      = fixedItems,
            FixedGold       = fixedGold,
            FixedCoins      = fixedCoins,
        };
    }

    private static GoldAlternative? ParseGoldAlternative(XmlNode node)
    {
        string? roll   = node.Attributes?["roll"]?.Value;
        string? amount = node.Attributes?["amount"]?.Value;

        if (roll != null)
        {
            return new GoldAlternative
            {
                Roll       = roll,
                Multiplier = ParseInt(node.Attributes?["multiplier"]?.Value, 1),
            };
        }

        if (amount != null && int.TryParse(amount, out int gp))
        {
            return new GoldAlternative { Amount = gp };
        }

        return null;
    }

    private static EquipmentChoice? ParseChoice(XmlNode node)
    {
        var options = new List<EquipmentOption>();

        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.Name != "option") continue;

            string label = child.Attributes?["label"]?.Value ?? string.Empty;
            var items    = new List<EquipmentItem>();
            var coins    = CoinGrant.Empty;

            foreach (XmlNode optionChild in child.ChildNodes)
            {
                switch (optionChild.Name)
                {
                    case "item":
                        var item = ParseItem(optionChild);
                        if (item != null)
                            items.Add(item);
                        break;

                    case "gold":
                        coins = coins.AddGold(ParseInt(optionChild.Attributes?["amount"]?.Value, 0));
                        break;

                    case "coins":
                        coins = coins.Add(ParseCoins(optionChild));
                        break;
                }
            }

            options.Add(new EquipmentOption { Label = label, Items = items, Coins = coins });
        }

        if (options.Count == 0) return null;
        return new EquipmentChoice { Options = options };
    }

    private static EquipmentItem? ParseItem(XmlNode node)
    {
        string? id       = node.Attributes?["id"]?.Value;
        string? category = node.Attributes?["category"]?.Value;
        string? name     = node.Attributes?["name"]?.Value;
        int     count    = ParseInt(node.Attributes?["count"]?.Value, 1);

        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(category))
            return null;

        return new EquipmentItem { Id = id, Category = category, Count = count, Name = name };
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out int result) ? result : fallback;

    private static CoinGrant ParseCoins(XmlNode node) =>
        new(
            ParseInt(node.Attributes?["cp"]?.Value, 0),
            ParseInt(node.Attributes?["sp"]?.Value, 0),
            ParseInt(node.Attributes?["ep"]?.Value, 0),
            ParseInt(node.Attributes?["gp"]?.Value, 0),
            ParseInt(node.Attributes?["pp"]?.Value, 0));
}
