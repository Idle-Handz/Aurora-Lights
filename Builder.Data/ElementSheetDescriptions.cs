using System.Collections.Generic;
using System.Text;

namespace Builder.Data;

public class ElementSheetDescriptions : List<ElementSheetDescriptions.SheetDescription>
{
	public class SheetDescription
	{
		public string Description { get; set; }

		public int Level { get; set; }

		public string Usage { get; set; }

		public string Action { get; set; }

		public bool HasUsage { get; set; }

		public bool HasAction { get; set; }

		public SheetDescription(string description)
			: this(description, 1)
		{
		}

		public SheetDescription(string description, int level)
		{
			Description = description;
			Level = level;
		}
	}

	public string AlternateName { get; set; }

	public string Usage { get; set; }

	public string Action { get; set; }

	public bool DisplayOnSheet { get; set; }

	public bool HasAlternateName => !string.IsNullOrWhiteSpace(AlternateName);

	public bool HasUsage => !string.IsNullOrWhiteSpace(Usage);

	public bool HasAction => !string.IsNullOrWhiteSpace(Action);

	public string Text
	{
		get
		{
			StringBuilder stringBuilder = new StringBuilder();
			using (Enumerator enumerator = GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					SheetDescription current = enumerator.Current;
					stringBuilder.AppendLine(current.Description);
				}
			}
			return stringBuilder.ToString().Trim();
		}
	}

	public ElementSheetDescriptions()
		: this(string.Empty, displayOnSheet: true)
	{
	}

	public ElementSheetDescriptions(string alternateName, bool displayOnSheet)
	{
		AlternateName = alternateName;
		DisplayOnSheet = displayOnSheet;
	}
}
