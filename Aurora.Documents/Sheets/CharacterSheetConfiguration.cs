using System;
using System.Collections.Generic;

namespace Aurora.Documents.Sheets;

public class CharacterSheetConfiguration
{
	[Obsolete("rename IsFormFillable")]
	public bool IsEditable { get; set; }

	public bool IsFormFillable => IsEditable;

	public bool IncludeFormatting { get; set; }

	public bool IsAttributeDisplayFlipped { get; set; }

	public bool IncludeArchetypesInBuild { get; set; }

	public bool IncludeCharacterPage { get; set; }

	public bool IncludeBackgroundPage { get; set; }

	public bool IncludeCompanionPage { get; set; }

	public bool IncludeSpellcastingPage { get; set; }

	public bool IncludeEquipmentPage { get; set; }

	public bool IncludeNotesPage { get; set; }

	public bool IncludeSpellcards { get; set; }

	public bool IncludeItemcards { get; set; }

	public bool IncludeAttackCards { get; set; }

	public bool IncludeFeatureCards { get; set; }

	public bool StartNewSpellCardsPage { get; set; }

	public bool StartNewItemCardsPage { get; set; }

	public bool StartNewAttackCardsPage { get; set; }

	public bool StartNewFeatureCardsPage { get; set; }

	public bool UseLegacyDetailsPage { get; set; }

	public bool UseLegacyBackgroundPage { get; set; }

	public bool UseLegacySpellcastingPage { get; set; }

	public List<string> FlattenFields { get; set; }

	public bool FlattenFieldsCollection { get; set; }

	public CharacterSheetConfiguration()
	{
		IsEditable = false;
		IncludeCharacterPage = true;
		IncludeBackgroundPage = true;
		IncludeSpellcastingPage = false;
		IncludeSpellcards = IncludeSpellcastingPage;
		IncludeItemcards = false;
		IncludeAttackCards = false;
		IncludeFeatureCards = false;
		FlattenFields = new List<string>();
	}
}
