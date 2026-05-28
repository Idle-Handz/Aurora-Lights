namespace Aurora.Importer;

public static class SpellcastingExtensionText
{
    public static string? JoinEntries(IEnumerable<string?> entries)
    {
        var values = entries
            .SelectMany(entry => SplitEntries(entry))
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToList();

        return values.Count == 0 ? null : string.Join(", ", values);
    }

    public static List<string> SplitEntries(string? input)
    {
        var values = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
            return values;

        int paren = 0, bracket = 0, brace = 0;
        var cur = new System.Text.StringBuilder();

        foreach (char ch in input)
        {
            switch (ch)
            {
                case '(': paren++; break;
                case ')': paren = Math.Max(0, paren - 1); break;
                case '[': bracket++; break;
                case ']': bracket = Math.Max(0, bracket - 1); break;
                case '{': brace++; break;
                case '}': brace = Math.Max(0, brace - 1); break;
            }

            if (ch == ',' && paren == 0 && bracket == 0 && brace == 0)
            {
                AddCurrent();
                continue;
            }

            cur.Append(ch);
        }

        AddCurrent();
        return values;

        void AddCurrent()
        {
            string value = cur.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
                values.Add(value);
            cur.Clear();
        }
    }
}
