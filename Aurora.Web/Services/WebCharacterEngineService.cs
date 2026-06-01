using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Models.Sources;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using Builder.Presentation.ViewModels.Shell.Items;
using Aurora.Components.Models;
using System.Text;
using System.Xml;

namespace Aurora.Web.Services;

public sealed class WebCharacterEngineService
{
    private static readonly HashSet<string> ItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Item", "Weapon", "Armor", "Magic Item", "Ammunition",
        "Tool", "Mount", "Vehicle", "Pack", "Gear", "Adventuring Gear"
    };

    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly ILogger<WebCharacterEngineService> _logger;
    private readonly WebApplicationContext _applicationContext;
    private readonly WebSelectionRuleExpanderHandler _selectionHandler = new();
    private readonly WebSpellcastingSectionHandler _spellcastingHandler = new();
    private bool _fileLoggerInitialized;

    private sealed record MagicRuleSelectionGroup(
        string Label,
        IReadOnlyList<MagicRuleSelectionEntry> Entries);

    private sealed record MagicRuleSelectionEntry(
        string Key,
        SelectRule Rule,
        int Number,
        string Label,
        string? CurrentName,
        int RequiredLevel);

    private sealed record SelectionOption(
        string Id,
        string Name,
        string Description,
        string Source,
        string Requirements);

    private sealed record BuildRuleSelection(
        string Key,
        string GroupId,
        string GroupLabel,
        SelectRule Rule,
        int Number,
        string Label,
        string Type,
        string? CurrentName,
        int RequiredLevel);

    public WebCharacterEngineService(ILogger<WebCharacterEngineService> logger)
    {
        _logger = logger;
        _applicationContext = new WebApplicationContext();

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        ApplicationContext.SetCurrent(_applicationContext);
        SelectionRuleExpanderContext.Current = _selectionHandler;
        SpellcastingSectionContext.Current = _spellcastingHandler;
        MessageDialogContext.Current = new WebMessageDialogService();
        ExternalLauncherContext.Current = new WebExternalLauncher();
    }

    public async Task<WebCharacterRuntimeState> OpenCharacterAsync(PhaseZeroSessionWorkspace workspace, string absolutePath, string relativePath)
    {
        await _operationLock.WaitAsync();
        try
        {
            await PrepareWorkspaceAsync(workspace);

            CharacterLoadCompatibilityService.PrepareForCharacterLoad();

            CharacterFile file = new(absolutePath);
            CharacterFile.LoadResult result = await file.Load();

            Character character = CharacterManager.Current.Character;
            CharacterLoadCompatibilityService.RestoreEquippedSlots(character);

            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
            string status = result.Success ? "Character loaded into the current web session." : result.Message;
            return new WebCharacterRuntimeState(summary, false, status);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterRuntimeState> CreateCharacterAsync(
        PhaseZeroSessionWorkspace workspace,
        string name,
        string playerName,
        string group)
    {
        await _operationLock.WaitAsync();
        try
        {
            await PrepareWorkspaceAsync(workspace);

            CharacterLoadCompatibilityService.PrepareForCharacterLoad();
            Character character = await CharacterManager.Current.New(initializeFirstLevel: true);
            character.Name = string.IsNullOrWhiteSpace(name) ? "New Character" : name.Trim();
            character.PlayerName = playerName.Trim();

            string directory = Path.Combine(workspace.WorkspacePath, "characters");
            Directory.CreateDirectory(directory);

            string safeName = string.Concat(character.Name.Select(ch =>
                Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));

            string path = DataManager.Current.GetCombinedCharacterFilePath(safeName, directory);
            if (File.Exists(path))
            {
                string timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
                path = DataManager.Current.GetCombinedCharacterFilePath($"{safeName}_{timestamp}", directory);
            }

            CharacterFile file = new(path);
            file.CollectionGroupName = string.IsNullOrWhiteSpace(group) ? "Characters" : group.Trim();
            file.Save(character);

            string relativePath = Path.GetRelativePath(workspace.WorkspacePath, path);
            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(path), character, file);
            return new WebCharacterRuntimeState(summary, true, "Character created in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<byte[]> ExportCharacterFileAsync(PhaseZeroSessionWorkspace workspace, string relativePath)
    {
        await _operationLock.WaitAsync();
        try
        {
            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            return await File.ReadAllBytesAsync(absolutePath);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<byte[]> ExportCharacterPdfAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            return WebCharacterPdfExporter.CreateSummaryPdf();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<EditableCharacterInfoModel> GetCurrentCharacterInfoAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            Character? character = CharacterManager.Current?.Character;
            if (character is null)
            {
                throw new InvalidOperationException("No character is active in the current web session.");
            }

            return BuildInfoModel(character);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterInfoState> UpdateCurrentCharacterInfoAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        EditableCharacterInfoModel info)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character? character = CharacterManager.Current?.Character;
            if (character is null)
            {
                throw new InvalidOperationException("No character is active in the current web session.");
            }

            ApplyInfoModel(character, info);

            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            CharacterFile file = new(absolutePath);
            file.Save();

            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
            EditableCharacterInfoModel refreshedInfo = BuildInfoModel(character);
            return new WebCharacterInfoState(summary, refreshedInfo, "Character details saved in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterSourceState> GetCurrentSourceStateAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            return new WebCharacterSourceState(
                BuildSourceGroups(),
                "Source restrictions reflect the current browser session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterSourceState> ToggleSourceGroupAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string groupId)
    {
        await _operationLock.WaitAsync();
        try
        {
            SourcesGroup? group = CharacterManager.Current.SourcesManager.SourceGroups
                .FirstOrDefault(candidate => string.Equals(candidate.Name, groupId, StringComparison.Ordinal));
            if (group is null)
            {
                throw new InvalidOperationException("The requested source group was not found.");
            }

            if (group.AllowUnchecking)
            {
                group.SetIsChecked(group.IsChecked == true ? false : (bool?)true, updateChildren: true);
                ApplyAndPersistSourceRestrictions(workspace, relativePath);
            }

            return new WebCharacterSourceState(
                BuildSourceGroups(),
                "Source restrictions updated for the current browser session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterSourceState> ToggleSourceItemAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string sourceId)
    {
        await _operationLock.WaitAsync();
        try
        {
            SourceItem? item = CharacterManager.Current.SourcesManager.SourceGroups
                .SelectMany(group => group.Sources)
                .FirstOrDefault(candidate => string.Equals(candidate.Source.Id, sourceId, StringComparison.Ordinal));
            if (item is null)
            {
                throw new InvalidOperationException("The requested source item was not found.");
            }

            if (item.AllowUnchecking)
            {
                item.SetIsChecked(item.IsChecked != true, updateChildren: true, updateParent: true);
                ApplyAndPersistSourceRestrictions(workspace, relativePath);
            }

            return new WebCharacterSourceState(
                BuildSourceGroups(),
                "Source restrictions updated for the current browser session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<EquipmentOverviewModel> GetCurrentEquipmentStateAsync()
    {
        await _operationLock.WaitAsync();
        try
        {
            Character? character = CharacterManager.Current?.Character;
            if (character is null)
            {
                throw new InvalidOperationException("No character is active in the current web session.");
            }

            return BuildEquipmentModel(character);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<MagicOverviewModel> GetCurrentMagicStateAsync(PhaseZeroSessionWorkspace workspace, string relativePath)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character? character = CharacterManager.Current?.Character;
            if (character is null)
            {
                throw new InvalidOperationException("No character is active in the current web session.");
            }

            MagicOverviewModel magic = BuildMagicModel(character);
            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            ApplyPersistedMagicState(absolutePath, magic);
            return magic;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<MagicSpellDetailModel?> GetCurrentMagicSpellDetailAsync(string id)
    {
        await _operationLock.WaitAsync();
        try
        {
            return BuildMagicSpellDetail(id);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterBuildState> GetCurrentBuildStateAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string statusMessage)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            CharacterFile file = new(absolutePath);
            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
            IReadOnlyList<WebBuildSelectionGroup> groups = BuildWebSelectionGroups();
            return new WebCharacterBuildState(
                summary,
                groups,
                groups.Sum(group => group.Entries.Count(entry => entry.CurrentName is null)),
                statusMessage);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<IReadOnlyList<WebBuildSelectionOption>> SearchBuildSelectionOptionsAsync(string entryKey, string query)
    {
        await _operationLock.WaitAsync();
        try
        {
            BuildRuleSelection selection = FindBuildRuleSelection(entryKey);
            IEnumerable<SelectionOption> options = GetSelectionOptions(selection.Rule);

            if (!string.IsNullOrWhiteSpace(query))
            {
                options = options.Where(option =>
                    option.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || option.Source.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || option.Requirements.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return options
                .Take(200)
                .Select(option => new WebBuildSelectionOption(
                    option.Id,
                    option.Name,
                    option.Description,
                    option.Source,
                    option.Requirements))
                .ToList();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterBuildState> ChangeBuildSelectionAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string entryKey,
        string optionId)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            BuildRuleSelection selection = FindBuildRuleSelection(entryKey);
            ApplySelection(selection, optionId);
            CharacterManager.Current.ReprocessCharacter();

            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            CharacterFile file = new(absolutePath);
            file.Save();

            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
            IReadOnlyList<WebBuildSelectionGroup> groups = BuildWebSelectionGroups();
            return new WebCharacterBuildState(
                summary,
                groups,
                groups.Sum(group => group.Entries.Count(entry => entry.CurrentName is null)),
                "Build selection updated in the current browser session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<IReadOnlyList<WebEquipmentSearchResult>> SearchEquipmentItemsAsync(string query, string? slotId = null)
    {
        await _operationLock.WaitAsync();
        try
        {
            IEnumerable<ElementBase> source = DataManager.Current.ElementsCollection.Where(element =>
                string.IsNullOrWhiteSpace(slotId)
                    ? IsSearchableInventoryElement(element)
                    : IsElementCompatibleWithSlot(element, slotId));

            if (!string.IsNullOrWhiteSpace(query))
            {
                source = source.Where(element => !string.IsNullOrWhiteSpace(element.Name)
                    && element.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return source
                .Where(element => !string.IsNullOrWhiteSpace(element.Name))
                .OrderBy(element => element.Name)
                .Take(200)
                .Select(element => new WebEquipmentSearchResult(
                    element.Id,
                    element.Name ?? string.Empty,
                    element.Type,
                    element.Source ?? string.Empty,
                    GetDescription(element)))
                .ToList();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<IReadOnlyList<WebEquipmentInventoryOption>> GetEquipmentInventoryOptionsAsync(string slotId)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            return character.Inventory.Items
                .Where(item => IsItemCompatibleWithSlot(item, slotId))
                .OrderBy(item => item.DisplayName ?? item.Name ?? string.Empty)
                .Select(item => new WebEquipmentInventoryOption(item.Identifier, item.DisplayName ?? item.Name ?? string.Empty))
                .ToList();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> AddCurrentEquipmentItemAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string elementId,
        int amount)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            if (!AddItem(character, elementId, amount))
            {
                throw new InvalidOperationException("The requested item could not be added.");
            }

            return SaveCurrentEquipmentState(workspace, relativePath, character, "Item added to inventory in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> EquipCurrentEquipmentSlotAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string slotId,
        string? identifier,
        string? elementId)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            bool success = !string.IsNullOrWhiteSpace(identifier)
                ? EquipToSlot(character, slotId, identifier)
                : !string.IsNullOrWhiteSpace(elementId) && AddAndEquipToSlot(character, slotId, elementId);

            if (!success)
            {
                throw new InvalidOperationException("The requested gear change could not be applied.");
            }

            return SaveCurrentEquipmentState(workspace, relativePath, character, "Gear slot updated in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<IReadOnlyList<WebMagicSelectionOption>> SearchMagicSelectionOptionsAsync(string entryKey, string query)
    {
        await _operationLock.WaitAsync();
        try
        {
            MagicRuleSelectionEntry selection = FindMagicRuleSelection(entryKey);
            IEnumerable<SelectionOption> options = GetSelectionOptions(selection.Rule);

            if (!string.IsNullOrWhiteSpace(query))
            {
                options = options.Where(option =>
                    option.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || option.Source.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            return options
                .Take(200)
                .Select(option => new WebMagicSelectionOption(
                    option.Id,
                    option.Name,
                    option.Source,
                    option.Description,
                    option.Requirements))
                .ToList();
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterMagicState> ChangeCurrentMagicSelectionAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string entryKey,
        string elementId)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            MagicRuleSelectionEntry selection = FindMagicRuleSelection(entryKey);
            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);

            MagicOverviewModel persistedBefore = BuildMagicModel(character);
            ApplyPersistedMagicState(absolutePath, persistedBefore);

            SelectionRuleExpanderContext.Current?.SetRegisteredElement(selection.Rule, elementId, selection.Number);
            CharacterManager.Current.ReprocessCharacter();

            CharacterFile file = new(absolutePath);
            file.Save();

            MagicOverviewModel magic = BuildMagicModel(character);
            CopyPersistedMagicState(persistedBefore, magic);
            PersistMagicState(absolutePath, magic);

            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
            return new WebCharacterMagicState(summary, magic, "Known spell selection updated for the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> UpdateCurrentEquipmentCoinAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        EquipmentCoinChangeModel change)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            switch (change.CoinId)
            {
                case "cp":
                    character.Inventory.Coins.Copper = change.Value;
                    break;
                case "sp":
                    character.Inventory.Coins.Silver = change.Value;
                    break;
                case "ep":
                    character.Inventory.Coins.Electrum = change.Value;
                    break;
                case "gp":
                    character.Inventory.Coins.Gold = change.Value;
                    break;
                case "pp":
                    character.Inventory.Coins.Platinum = change.Value;
                    break;
            }

            return SaveCurrentEquipmentState(workspace, relativePath, character, "Equipment currency updated in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> UpdateCurrentEquipmentNoteAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        EquipmentNoteChangeModel change)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            switch (change.NoteId)
            {
                case "equipment":
                    character.Inventory.Equipment = change.Value;
                    break;
                case "treasure":
                    character.Inventory.Treasure = change.Value;
                    break;
                case "quest":
                    character.Inventory.QuestItems = change.Value;
                    break;
            }

            return SaveCurrentEquipmentState(workspace, relativePath, character, "Equipment notes updated in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> ToggleCurrentEquipmentItemAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string identifier,
        bool attunement)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            RefactoredEquipmentItem? item = character.Inventory.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.Identifier, identifier, StringComparison.Ordinal));
            if (item is null)
            {
                throw new InvalidOperationException("The requested inventory item was not found.");
            }

            if (attunement)
            {
                if (item.IsAttuned)
                    item.DeactivateAttunement();
                else
                    item.Activate(equip: item.IsEquipped, attune: true);
            }
            else
            {
                if (item.IsEquipped)
                    item.Deactivate();
                else
                    item.Activate(equip: true, attune: item.IsAttuned);
            }

            character.Inventory.CalculateAttunedItemCount();
            return SaveCurrentEquipmentState(workspace, relativePath, character, "Equipment state updated in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> ChangeCurrentEquipmentAmountAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        EquipmentAmountChangeModel change)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            RefactoredEquipmentItem? item = character.Inventory.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.Identifier, change.Identifier, StringComparison.Ordinal));
            if (item is null)
            {
                throw new InvalidOperationException("The requested inventory item was not found.");
            }

            item.Amount = Math.Max(1, change.Amount);
            return SaveCurrentEquipmentState(workspace, relativePath, character, "Item quantity updated in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> RemoveCurrentEquipmentItemAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string identifier)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            RefactoredEquipmentItem? item = character.Inventory.Items.FirstOrDefault(candidate =>
                string.Equals(candidate.Identifier, identifier, StringComparison.Ordinal));
            if (item is null)
            {
                throw new InvalidOperationException("The requested inventory item was not found.");
            }

            if (item.IsEquipped)
            {
                string location = item.EquippedLocation ?? string.Empty;
                if (location == "Armor")
                    character.Inventory.UnequipArmor();
                else if (location is "Primary Hand" or "Two-Handed" or "Two-Handed (Versatile)")
                    character.Inventory.UnequipPrimary();
                else if (location == "Secondary Hand")
                    character.Inventory.UnequipSecondary();
                else
                    item.Deactivate();
            }
            else
            {
                item.Deactivate();
            }

            character.Inventory.Items.Remove(item);
            character.Inventory.CalculateAttunedItemCount();
            return SaveCurrentEquipmentState(workspace, relativePath, character, "Item removed from inventory in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterEquipmentState> UnequipCurrentEquipmentSlotAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        string slotId)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            switch (slotId)
            {
                case "armor":
                    character.Inventory.UnequipArmor();
                    break;
                case "main-hand":
                    character.Inventory.UnequipPrimary();
                    break;
                case "off-hand":
                    character.Inventory.UnequipSecondary();
                    break;
                default:
                    throw new InvalidOperationException("The requested gear slot was not found.");
            }

            character.Inventory.CalculateAttunedItemCount();
            return SaveCurrentEquipmentState(workspace, relativePath, character, "Gear slot updated in the current web session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterMagicState> ToggleCurrentMagicPreparedAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        MagicPreparedChangeModel change)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            MagicOverviewModel magic = BuildMagicModel(character);
            ApplyPersistedMagicState(absolutePath, magic);

            MagicSpellListEntryModel? spell = magic.SpellLevels
                .SelectMany(level => level.Spells)
                .FirstOrDefault(candidate => string.Equals(candidate.Id, change.SpellId, StringComparison.Ordinal));
            if (spell is null || spell.IsAlwaysPrepared)
            {
                ImportedCharacterSummary unchangedSummary = BuildSummary(relativePath, new FileInfo(absolutePath), character, new CharacterFile(absolutePath));
                return new WebCharacterMagicState(unchangedSummary, magic, "Spell preparation updated for the current browser session.");
            }

            if (change.Value && !spell.IsPrepared && magic.IsPreparedCaster && magic.MaxPrepared > 0 && magic.PreparedCount >= magic.MaxPrepared)
            {
                ImportedCharacterSummary unchangedSummary = BuildSummary(relativePath, new FileInfo(absolutePath), character, new CharacterFile(absolutePath));
                return new WebCharacterMagicState(unchangedSummary, magic, "Prepared spell limit reached for the current browser session.");
            }

            spell.IsPrepared = change.Value;
            magic.PreparedCount = magic.SpellLevels
                .SelectMany(level => level.Spells)
                .Count(candidate => candidate.IsPrepared && !candidate.IsAlwaysPrepared);

            PersistMagicState(absolutePath, magic);

            CharacterFile file = new(absolutePath);
            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
            return new WebCharacterMagicState(summary, magic, "Spell preparation saved in the current browser session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<WebCharacterMagicState> ToggleCurrentMagicSlotAsync(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        MagicSlotToggleModel change)
    {
        await _operationLock.WaitAsync();
        try
        {
            Character character = RequireCurrentCharacter();
            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            MagicOverviewModel magic = BuildMagicModel(character);
            ApplyPersistedMagicState(absolutePath, magic);

            MagicSpellLevelModel? level = magic.SpellLevels.FirstOrDefault(candidate => candidate.Level == change.Level);
            if (level is not null)
            {
                level.UsedSlots = change.SlotIndex < level.UsedSlots
                    ? change.SlotIndex
                    : change.SlotIndex + 1;
            }

            PersistMagicState(absolutePath, magic);

            CharacterFile file = new(absolutePath);
            ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
            return new WebCharacterMagicState(summary, magic, "Spell slot usage saved in the current browser session.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task PersistCurrentMagicStateAsync(PhaseZeroSessionWorkspace workspace, string relativePath, MagicOverviewModel magic)
    {
        await _operationLock.WaitAsync();
        try
        {
            string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
            PersistMagicState(absolutePath, magic);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task PrepareWorkspaceAsync(PhaseZeroSessionWorkspace workspace)
    {
        string importsRoot = Path.Combine(workspace.WorkspacePath, "imports");
        string documentsRoot = Path.Combine(workspace.WorkspacePath, "documents");
        Directory.CreateDirectory(documentsRoot);

        _applicationContext.Settings.DocumentsRootDirectory = documentsRoot;
        _applicationContext.Settings.AdditionalCustomDirectory = string.Empty;
        _applicationContext.Settings.AdditionalCustomDirectories =
            Directory.Exists(importsRoot) ? [importsRoot] : [];
        _applicationContext.Settings.PlayerName = string.Empty;

        DataManager.Current.InitializeDirectories();
        if (!_fileLoggerInitialized)
        {
            DataManager.Current.InitializeFileLogger();
            _fileLoggerInitialized = true;
        }

        _selectionHandler.RemoveAllExpanders();
        _spellcastingHandler.ResetPreparedState();

        await DataManager.Current.InitializeElementDataAsync();
    }

    private static ImportedCharacterSummary BuildSummary(string relativePath, FileInfo fileInfo, Character character, CharacterFile characterFile) =>
        new(
            relativePath,
            fileInfo.Name,
            string.IsNullOrWhiteSpace(character.Name) ? characterFile.DisplayName : character.Name,
            character.PlayerName ?? string.Empty,
            character.Level.ToString(),
            character.Race ?? characterFile.DisplayRace ?? string.Empty,
            character.Class ?? characterFile.DisplayClass ?? string.Empty,
            character.Background ?? characterFile.DisplayBackground ?? string.Empty,
            characterFile.CollectionGroupName ?? string.Empty,
            characterFile.DisplayVersion ?? string.Empty,
            fileInfo.Length);

    private static Character RequireCurrentCharacter() =>
        CharacterManager.Current?.Character
        ?? throw new InvalidOperationException("No character is active in the current web session.");

    private static WebCharacterEquipmentState SaveCurrentEquipmentState(
        PhaseZeroSessionWorkspace workspace,
        string relativePath,
        Character character,
        string statusMessage)
    {
        string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
        CharacterFile file = new(absolutePath);
        file.Save();

        ImportedCharacterSummary summary = BuildSummary(relativePath, new FileInfo(absolutePath), character, file);
        return new WebCharacterEquipmentState(summary, BuildEquipmentModel(character), statusMessage);
    }

    private static EquipmentOverviewModel BuildEquipmentModel(Character character)
    {
        character.Inventory.CalculateAttunedItemCount();

        return new EquipmentOverviewModel
        {
            Attacks = character.AttacksSection.Items
                .Where(attack => attack.IsDisplayed)
                .Select(attack => new EquipmentAttackModel(
                    attack.Name.Content ?? string.Empty,
                    attack.DisplayCalculatedAttack ?? attack.Attack.Content ?? string.Empty,
                    attack.DisplayCalculatedDamage ?? attack.Damage.Content ?? string.Empty,
                    attack.Range.Content ?? string.Empty))
                .ToList(),
            GearSlots =
            [
                new EquipmentGearSlotModel("armor", "Armor", character.Inventory.EquippedArmor?.DisplayName ?? character.Inventory.EquippedArmor?.Name),
                new EquipmentGearSlotModel("main-hand", "Main Hand", character.Inventory.EquippedPrimary?.DisplayName ?? character.Inventory.EquippedPrimary?.Name),
                new EquipmentGearSlotModel("off-hand", "Off Hand", character.Inventory.EquippedSecondary?.DisplayName ?? character.Inventory.EquippedSecondary?.Name)
            ],
            InventoryItems = character.Inventory.Items
                .Where(item => item.IncludeInEquipmentPageInventory)
                .Select(item => new EquipmentInventoryItemModel(
                    item.Identifier,
                    item.DisplayName ?? item.Name ?? string.Empty,
                    item.Amount,
                    item.IsStackable,
                    item.IsEquipped,
                    item.EquippedLocation ?? string.Empty,
                    item.IsAttunable,
                    item.IsAttuned,
                    item.DisplayWeight ?? string.Empty,
                    item.DisplayPrice ?? string.Empty,
                    item.IsEquippable))
                .ToList(),
            Copper = character.Inventory.Coins.Copper,
            Silver = character.Inventory.Coins.Silver,
            Electrum = character.Inventory.Coins.Electrum,
            Gold = character.Inventory.Coins.Gold,
            Platinum = character.Inventory.Coins.Platinum,
            AttunedCount = character.Inventory.AttunedItemCount,
            AttunedMax = character.Inventory.MaxAttunedItemCount,
            EquipmentNotes = character.Inventory.Equipment ?? string.Empty,
            TreasureNotes = character.Inventory.Treasure ?? string.Empty,
            QuestNotes = character.Inventory.QuestItems ?? string.Empty
        };
    }

    private static IReadOnlyList<WebBuildSelectionGroup> BuildWebSelectionGroups() =>
        BuildRuleSelections()
            .GroupBy(selection => (selection.GroupId, selection.GroupLabel))
            .Select(group => new WebBuildSelectionGroup(
                group.Key.GroupId,
                group.Key.GroupLabel,
                group.Select(selection => new WebBuildSelectionEntry(
                        selection.Key,
                        selection.Label,
                        selection.Type,
                        selection.CurrentName,
                        selection.RequiredLevel))
                    .ToList()))
            .ToList();

    private static IReadOnlyList<BuildRuleSelection> BuildRuleSelections()
    {
        CharacterManager manager = CharacterManager.Current;
        var classManagers = manager.ClassProgressionManagers;
        List<(string GroupId, string GroupLabel, SelectRule Rule, int Number, string Label, string Type, string? CurrentName, int RequiredLevel)> raw = [];

        foreach (SelectRule rule in manager.SelectionRules)
        {
            string type = rule.Attributes.Type ?? "Other";
            if (string.Equals(type, "Spell", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            (string groupId, string groupLabel) = ClassifyBuildSelectionGroup(manager, classManagers, rule, type);
            for (int number = 1; number <= rule.Attributes.Number; number++)
            {
                string ruleName = rule.Attributes.Name ?? type;
                string label = rule.Attributes.Number > 1 ? $"{ruleName} ({number})" : ruleName;
                raw.Add((groupId, groupLabel, rule, number, label, type, ResolveSelectionName(rule, number), rule.Attributes.RequiredLevel));
            }
        }

        string[] order = ["race", "class", "background", "languages", "proficiencies"];
        return raw
            .OrderBy(entry =>
            {
                int index = Array.IndexOf(order, entry.GroupId);
                return index < 0 ? order.Length : index;
            })
            .ThenBy(entry => entry.GroupLabel)
            .ThenBy(entry => entry.RequiredLevel)
            .ThenBy(entry => entry.Label)
            .Select((entry, index) => new BuildRuleSelection(
                index.ToString(),
                entry.GroupId,
                entry.GroupLabel,
                entry.Rule,
                entry.Number,
                entry.Label,
                entry.Type,
                entry.CurrentName,
                entry.RequiredLevel))
            .ToList();
    }

    private static (string GroupId, string GroupLabel) ClassifyBuildSelectionGroup(
        CharacterManager manager,
        IEnumerable<ClassProgressionManager> classManagers,
        SelectRule rule,
        string type)
    {
        var progressManager = manager.GetProgressManager(rule);
        var classManager = classManagers.FirstOrDefault(candidate => ReferenceEquals(candidate, progressManager));
        if (classManager is not null)
        {
            string label = classManager.ClassElement?.Name ?? "Class";
            return ($"class:{label}", label);
        }

        if (BuildRaceTypes.Contains(type))
        {
            return ("race", "Race");
        }

        if (BuildClassTypes.Contains(type))
        {
            return ("class", "Class");
        }

        if (BuildBackgroundTypes.Contains(type))
        {
            return ("background", "Background");
        }

        if (BuildLanguageTypes.Contains(type))
        {
            return ("languages", "Languages");
        }

        if (BuildProficiencyTypes.Contains(type))
        {
            return ("proficiencies", "Proficiencies");
        }

        return ($"other:{type}", type);
    }

    private static BuildRuleSelection FindBuildRuleSelection(string entryKey) =>
        BuildRuleSelections()
            .FirstOrDefault(entry => string.Equals(entry.Key, entryKey, StringComparison.Ordinal))
        ?? throw new InvalidOperationException("The requested build selection could not be found.");

    private void ApplySelection(BuildRuleSelection selection, string optionId)
    {
        if (selection.Rule.Attributes.IsList)
        {
            SelectionRuleListItem? item = selection.Rule.Attributes.ListItems?
                .FirstOrDefault(candidate => string.Equals(candidate.ID.ToString(), optionId, StringComparison.Ordinal));
            if (item is null)
            {
                throw new InvalidOperationException("The requested list option could not be found.");
            }

            _selectionHandler.SetRegisteredListItem(selection.Rule, item, selection.Number);
            return;
        }

        SelectionRuleExpanderContext.Current?.SetRegisteredElement(selection.Rule, optionId, selection.Number);
    }

    private static string? ResolveSelectionName(SelectRule rule, int number)
    {
        try
        {
            object? current = SelectionRuleExpanderContext.Current?.GetRegisteredElement(rule, number);
            if (current is null)
            {
                return null;
            }

            if (current is SelectionRuleListItem listItem)
            {
                return listItem.Text;
            }

            return (string?)((dynamic)current).Name;
        }
        catch
        {
            return null;
        }
    }

    private static readonly HashSet<string> BuildRaceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Race", "Sub Race", "Racial Trait", "Dragonmark", "Variant", "Race Variant", "Heritage", "Lineage"
    };

    private static readonly HashSet<string> BuildClassTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Class", "Archetype"
    };

    private static readonly HashSet<string> BuildBackgroundTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Background", "Background Feature", "Background Variant", "Background Characteristics",
        "Deity", "Alignment", "Bond", "Flaw", "Ideal", "Personality Trait"
    };

    private static readonly HashSet<string> BuildLanguageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Language"
    };

    private static readonly HashSet<string> BuildProficiencyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Proficiency", "Skill", "Tool Proficiency", "Armor Proficiency", "Weapon Proficiency", "Expertise"
    };

    private static IReadOnlyList<MagicKnownSpellGroupModel> BuildMagicRuleGroups() =>
        BuildMagicRuleSelectionGroups()
            .Select(group => new MagicKnownSpellGroupModel(
                group.Label,
                group.Entries
                    .Select(entry => new MagicKnownSpellEntryModel(entry.Key, entry.Label, entry.CurrentName))
                    .ToList()))
            .ToList();

    private static IReadOnlyList<MagicRuleSelectionGroup> BuildMagicRuleSelectionGroups()
    {
        var cm = CharacterManager.Current;
        var handler = SelectionRuleExpanderContext.Current;
        var byClass = new Dictionary<string, List<MagicRuleSelectionEntry>>(StringComparer.Ordinal);

        foreach (SelectRule rule in cm.SelectionRules)
        {
            if (rule.Attributes.Type != "Spell")
            {
                continue;
            }

            var progressManager = cm.GetProgressManager(rule);
            var classManager = cm.ClassProgressionManagers.FirstOrDefault(manager => ReferenceEquals(manager, progressManager));
            string groupLabel = classManager?.ClassElement?.Name ?? "Spells";

            if (!byClass.TryGetValue(groupLabel, out List<MagicRuleSelectionEntry>? entries))
            {
                entries = [];
                byClass[groupLabel] = entries;
            }

            for (int number = 1; number <= rule.Attributes.Number; number++)
            {
                string? currentName = null;
                try
                {
                    var current = handler?.GetRegisteredElement(rule, number);
                    if (current is not null)
                    {
                        currentName = (string?)((dynamic)current).Name;
                    }
                }
                catch
                {
                }

                string label = rule.Attributes.Number > 1
                    ? $"{rule.Attributes.Name ?? rule.Attributes.Type} ({number})"
                    : (rule.Attributes.Name ?? rule.Attributes.Type);

                entries.Add(new MagicRuleSelectionEntry(
                    string.Empty,
                    rule,
                    number,
                    label,
                    currentName,
                    rule.Attributes.RequiredLevel));
            }
        }

        List<MagicRuleSelectionGroup> groups = [];
        int groupIndex = 0;
        foreach ((string groupLabel, List<MagicRuleSelectionEntry> rawEntries) in byClass)
        {
            List<MagicRuleSelectionEntry> orderedEntries = rawEntries
                .OrderBy(entry => entry.RequiredLevel)
                .ThenBy(entry => entry.Label)
                .Select((entry, entryIndex) => entry with { Key = $"{groupIndex}:{entryIndex}" })
                .ToList();

            groups.Add(new MagicRuleSelectionGroup(groupLabel, orderedEntries));
            groupIndex++;
        }

        return groups;
    }

    private static MagicRuleSelectionEntry FindMagicRuleSelection(string entryKey) =>
        BuildMagicRuleSelectionGroups()
            .SelectMany(group => group.Entries)
            .FirstOrDefault(entry => string.Equals(entry.Key, entryKey, StringComparison.Ordinal))
        ?? throw new InvalidOperationException("The requested spell selection could not be found.");

    private static IReadOnlyList<SelectionOption> GetSelectionOptions(SelectRule rule)
    {
        try
        {
            if (rule.Attributes.IsList)
            {
                return (rule.Attributes.ListItems ?? [])
                    .Select(item => new SelectionOption(
                        item.ID.ToString(),
                        item.Text,
                        item.Text,
                        string.Empty,
                        string.Empty))
                    .ToList();
            }

            var interpreter = new ExpressionInterpreter();
            interpreter.InitializeWithSelectionRule(rule);

            IEnumerable<ElementBase> baseCollection = DataManager.Current.ElementsCollection
                .Where(element => element.Type.Equals(rule.Attributes.Type));

            IEnumerable<ElementBase> elements;
            if (!rule.Attributes.ContainsSupports())
            {
                elements = baseCollection;
            }
            else
            {
                try
                {
                    elements = interpreter.EvaluateSupportsExpression<ElementBase>(
                        rule.Attributes.Supports,
                        baseCollection,
                        rule.Attributes.SupportsElementIdRange());
                }
                catch
                {
                    elements = SpellFallbackOptions(rule, baseCollection);
                }
            }

            List<SelectionOption> options = elements
                .Where(element => !string.IsNullOrWhiteSpace(element.Name))
                .OrderBy(element => element.Name)
                .Select(element => new SelectionOption(
                    element.Id,
                    element.Name!,
                    GetFeatureDescription(element),
                    element.Source ?? string.Empty,
                    element.HasRequirements ? FormatRequirements(element.Requirements) : string.Empty))
                .ToList();

            if (options.Count == 0 && rule.Attributes.Type == "Spell")
            {
                options = SpellFallbackOptions(rule, baseCollection)
                    .Where(element => !string.IsNullOrWhiteSpace(element.Name))
                    .OrderBy(element => element.Name)
                    .Select(element => new SelectionOption(
                        element.Id,
                        element.Name!,
                        GetFeatureDescription(element),
                        element.Source ?? string.Empty,
                        element.HasRequirements ? FormatRequirements(element.Requirements) : string.Empty))
                    .ToList();
            }

            return DeduplicateOptions(options);
        }
        catch
        {
            return [];
        }
    }

    private static List<SelectionOption> DeduplicateOptions(List<SelectionOption> options)
    {
        List<SelectionOption> result = new(options.Count);
        foreach (IGrouping<(string Name, string Description), SelectionOption> group in options.GroupBy(option => (option.Name, option.Description)))
        {
            if (group.Count() == 1)
            {
                result.Add(group.First());
                continue;
            }

            string combinedSources = string.Join(", ",
                group.Select(option => option.Source)
                    .Where(source => !string.IsNullOrEmpty(source))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(source => source));

            result.Add(group.First() with { Source = combinedSources });
        }

        return result;
    }

    private static string FormatRequirements(string requirements)
    {
        if (string.IsNullOrWhiteSpace(requirements))
        {
            return string.Empty;
        }

        IEnumerable<string> tokens = System.Text.RegularExpressions.Regex
            .Split(requirements, @"[,;]+|&&|\|\|")
            .Select(part => part.Trim(' ', '!', '(', ')'));

        List<string> parts = [];
        foreach (string token in tokens)
        {
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var match = System.Text.RegularExpressions.Regex.Match(token, @"^\[(\w+):(\d+)\]$");
            if (match.Success)
            {
                string key = match.Groups[1].Value.ToLowerInvariant();
                string value = match.Groups[2].Value;
                parts.Add(key switch
                {
                    "str" => $"STR {value}+",
                    "dex" => $"DEX {value}+",
                    "con" => $"CON {value}+",
                    "int" => $"INT {value}+",
                    "wis" => $"WIS {value}+",
                    "cha" => $"CHA {value}+",
                    "level" => $"Level {value}",
                    _ => $"{key.ToUpperInvariant()} {value}",
                });
                continue;
            }

            if (token.StartsWith("ID_", StringComparison.OrdinalIgnoreCase))
            {
                ElementBase? element = DataManager.Current.ElementsCollection
                    .FirstOrDefault(candidate => string.Equals(candidate.Id, token, StringComparison.OrdinalIgnoreCase));
                if (element is not null && !string.IsNullOrWhiteSpace(element.Name))
                {
                    parts.Add(element.Name!);
                }

                continue;
            }

            if (token.Contains('[') || token.Contains(':'))
            {
                continue;
            }

            parts.Add(token);
        }

        return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
    }

    private static IEnumerable<ElementBase> SpellFallbackOptions(SelectRule rule, IEnumerable<ElementBase> spellBase)
    {
        bool isCantrip = rule.Attributes.ContainsSupports()
                         && rule.Attributes.Supports.Contains("Cantrip", StringComparison.OrdinalIgnoreCase);

        string? className = null;
        if (rule.Attributes.ContainsSpellcastingName())
        {
            className = rule.Attributes.SpellcastingName;
        }

        if (className is null && rule.Attributes.ContainsSupports())
        {
            string firstWord = System.Text.RegularExpressions.Regex
                .Match(rule.Attributes.Supports, @"(?<!\$\()[A-Za-z][A-Za-z0-9 ]+")
                .Value
                .Trim();
            if (!string.IsNullOrEmpty(firstWord))
            {
                className = firstWord;
            }
        }

        if (className is null)
        {
            return [];
        }

        return spellBase.Where(element =>
        {
            if (element.Supports is null || !element.Supports.Contains(className))
            {
                return false;
            }

            int level = 0;
            try { level = (int)((dynamic)element).Level; } catch { }
            return isCantrip ? level == 0 : level > 0;
        });
    }

    private static bool IsSearchableInventoryElement(ElementBase element) =>
        ItemTypes.Contains(element.Type)
        && !string.IsNullOrWhiteSpace(element.Name)
        && !element.Name.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase);

    private static bool AddItem(Character character, string elementId, int amount = 1)
    {
        ElementBase? element = DataManager.Current.ElementsCollection.GetElement(elementId);
        if (element is null)
        {
            return false;
        }

        try
        {
            Type elementType = element.GetType();
            var ctor = typeof(RefactoredEquipmentItem)
                .GetConstructors()
                .FirstOrDefault(constructor =>
                {
                    var parameters = constructor.GetParameters();
                    return parameters.Length >= 1
                           && parameters[0].ParameterType.IsAssignableFrom(elementType)
                           && parameters.Skip(1).All(parameter => parameter.HasDefaultValue);
                });

            if (ctor is null)
            {
                return false;
            }

            object?[] args = ctor.GetParameters()
                .Select((parameter, index) => index == 0 ? (object?)element : parameter.DefaultValue)
                .ToArray();

            var item = (RefactoredEquipmentItem)ctor.Invoke(args);
            item.Amount = Math.Max(1, amount);
            character.Inventory.Items.Add(item);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool AddAndEquipToSlot(Character character, string slotId, string elementId)
    {
        if (!AddItem(character, elementId))
        {
            return false;
        }

        RefactoredEquipmentItem item = character.Inventory.Items.Last();
        return EquipToSlot(character, slotId, item.Identifier);
    }

    private static bool EquipToSlot(Character character, string slotId, string identifier)
    {
        RefactoredEquipmentItem? item = character.Inventory.Items.FirstOrDefault(candidate => candidate.Identifier == identifier);
        if (item is null)
        {
            return false;
        }

        switch (slotId)
        {
            case "armor":
                if (character.Inventory.EquippedArmor is not null)
                {
                    character.Inventory.UnequipArmor();
                }
                character.Inventory.EquipArmor(item);
                break;
            case "main-hand":
                if (character.Inventory.EquippedPrimary is not null)
                {
                    character.Inventory.UnequipPrimary();
                }
                character.Inventory.EquipPrimary(item, item.IsTwoHandTarget());
                break;
            case "off-hand":
                if (character.Inventory.EquippedSecondary is not null)
                {
                    character.Inventory.UnequipSecondary();
                }
                character.Inventory.EquipSecondary(item);
                break;
            default:
                return false;
        }

        character.Inventory.CalculateAttunedItemCount();
        return true;
    }

    private static bool IsItemCompatibleWithSlot(RefactoredEquipmentItem item, string slotId) => slotId switch
    {
        "armor" => item.IsArmorTarget(),
        "main-hand" => item.IsOneHandTarget() || item.IsTwoHandTarget() || item.IsPrimaryTarget(),
        "off-hand" => item.IsSecondaryTarget() || item.IsOneHandTarget(),
        _ => false
    };

    private static bool IsElementCompatibleWithSlot(ElementBase element, string slotId)
    {
        try
        {
            dynamic dynamicElement = element;
            string slotValue = ((string?)dynamicElement.Slot ?? string.Empty).ToLowerInvariant();
            IEnumerable<string> rawSlots = (IEnumerable<string>?)dynamicElement.Slots ?? [];
            HashSet<string> slots = rawSlots.Select(slot => slot.ToLowerInvariant()).ToHashSet();

            return slotId switch
            {
                "armor" => element.Type == "Armor"
                           && (slots.Contains("armor") || slots.Contains("body")
                               || slotValue == "armor" || slotValue == "body"),
                "main-hand" => element.Type == "Weapon"
                               && (slots.Contains("onehand") || slots.Contains("twohand")
                                   || slots.Contains("primary")
                                   || slotValue is "onehand" or "twohand"),
                "off-hand" => (element.Type == "Weapon"
                                   && (slots.Contains("onehand") || slots.Contains("secondary")))
                               || (element.Type == "Armor"
                                   && (slots.Contains("secondary") || slotValue.Contains("secondary"))),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private static string GetFeatureDescription(object element)
    {
        try
        {
            dynamic dynamicElement = element;
            var sheetDescription = dynamicElement.SheetDescription as System.Collections.IList;
            if (sheetDescription is not null && sheetDescription.Count > 0)
            {
                dynamic first = sheetDescription[0]!;
                return (string)(first.Description ?? string.Empty);
            }

            string raw = (string)(dynamicElement.Description ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim();
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static string GetDescription(ElementBase element)
    {
        try
        {
            if (element.SheetDescription?.Count > 0)
            {
                string? description = element.SheetDescription[0].Description?.Trim();
                if (!string.IsNullOrEmpty(description))
                {
                    return description;
                }
            }
        }
        catch
        {
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(element.Description))
            {
                return ElementDescriptionGenerator.GeneratePlainDescription(element.Description).Trim();
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static MagicOverviewModel BuildMagicModel(Character character)
    {
        var cm = CharacterManager.Current;
        var spellInfo = cm.GetSpellcastingInformations().FirstOrDefault(info => !info.IsExtension);

        string spellDc = string.Empty;
        string spellAttack = string.Empty;
        try
        {
            if (spellInfo is not null)
            {
                int dc = cm.StatisticsCalculator.StatisticValues.GetValue(spellInfo.GetSpellcasterSpellSaveStatisticName());
                int attack = cm.StatisticsCalculator.StatisticValues.GetValue(spellInfo.GetSpellcasterSpellAttackStatisticName());
                spellDc = dc.ToString();
                spellAttack = attack >= 0 ? $"+{attack}" : attack.ToString();
            }
        }
        catch
        {
        }

        bool preparedCaster = spellInfo?.Prepare ?? false;
        int maxPrepared = preparedCaster
            ? cm.StatisticsCalculator.StatisticValues.GetValue(spellInfo!.GetPrepareAmountStatisticName())
            : 0;

        IReadOnlyList<(string Id, string Name)> cantrips = CollectCantrips();
        IReadOnlyList<MagicSpellLevelModel> spellLevels = CollectSpellLevels(
            preparedCaster,
            spellInfo?.InitialSupportedSpellsExpression?.Supports ?? string.Empty,
            GetPreparedIds(spellInfo?.Name ?? string.Empty),
            isSpellbookCaster: preparedCaster
                && cm.SelectionRules.Any(rule => rule.Attributes.Type == "Spell")
                && string.IsNullOrEmpty(spellInfo?.InitialSupportedSpellsExpression?.Supports));

        return new MagicOverviewModel
        {
            HasSpellcasting = cm.Status.HasSpellcasting,
            IsPreparedCaster = preparedCaster,
            SpellcastingClass = spellInfo?.Name ?? string.Empty,
            SpellcastingAbility = spellInfo?.AbilityName ?? string.Empty,
            SpellcastingDc = spellDc,
            SpellcastingAttack = spellAttack,
            PreparedCount = spellLevels.SelectMany(level => level.Spells).Count(spell => spell.IsPrepared && !spell.IsAlwaysPrepared),
            MaxPrepared = maxPrepared,
            KnownSpellGroups = BuildMagicRuleGroups(),
            Cantrips = cantrips.Select(cantrip => new MagicSpellListEntryModel(cantrip.Id, cantrip.Name, true, true)).ToList(),
            SpellLevels = spellLevels
        };
    }

    private static void ApplyPersistedMagicState(string absolutePath, MagicOverviewModel magic)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return;
        }

        try
        {
            XmlDocument document = new();
            document.Load(absolutePath);

            Dictionary<string, bool> preparedById = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> preparedByName = new(StringComparer.OrdinalIgnoreCase);

            XmlNode? buildNode = document.DocumentElement?["build"];
            XmlNode? magicNode = buildNode?["magic"];
            if (magicNode is not null)
            {
                foreach (XmlNode spellcastingNode in magicNode.ChildNodes)
                {
                    if (spellcastingNode.Name != "spellcasting")
                    {
                        continue;
                    }

                    XmlNode? spellsNode = spellcastingNode["spells"];
                    if (spellsNode is null)
                    {
                        continue;
                    }

                    foreach (XmlNode spellNode in spellsNode.ChildNodes)
                    {
                        if (spellNode.Name != "spell")
                        {
                            continue;
                        }

                        bool isPrepared = string.Equals(spellNode.Attributes?["prepared"]?.Value, "true", StringComparison.OrdinalIgnoreCase);
                        string? spellId = spellNode.Attributes?["id"]?.Value;
                        string? spellName = spellNode.Attributes?["name"]?.Value;

                        if (!string.IsNullOrWhiteSpace(spellId))
                        {
                            preparedById[spellId] = isPrepared;
                        }

                        if (!string.IsNullOrWhiteSpace(spellName))
                        {
                            preparedByName[spellName] = isPrepared;
                        }
                    }
                }
            }

            foreach (MagicSpellListEntryModel spell in magic.SpellLevels.SelectMany(level => level.Spells))
            {
                if (spell.IsAlwaysPrepared)
                {
                    spell.IsPrepared = true;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(spell.Id) && preparedById.TryGetValue(spell.Id, out bool preparedByIdentifier))
                {
                    spell.IsPrepared = preparedByIdentifier;
                }
                else if (preparedByName.TryGetValue(spell.Name, out bool preparedBySpellName))
                {
                    spell.IsPrepared = preparedBySpellName;
                }
            }

            XmlNode? sessionNode = document.DocumentElement?["session"];
            XmlNode? spellSlotsNode = sessionNode?["spellslots"];
            if (spellSlotsNode is not null)
            {
                Dictionary<int, int> usedByLevel = [];
                foreach (XmlNode slotNode in spellSlotsNode.ChildNodes)
                {
                    if (slotNode.Name != "slot")
                    {
                        continue;
                    }

                    if (int.TryParse(slotNode.Attributes?["level"]?.Value, out int level)
                        && int.TryParse(slotNode.Attributes?["used"]?.Value, out int used))
                    {
                        usedByLevel[level] = Math.Max(0, used);
                    }
                }

                foreach (MagicSpellLevelModel level in magic.SpellLevels)
                {
                    if (usedByLevel.TryGetValue(level.Level, out int used))
                    {
                        level.UsedSlots = Math.Clamp(used, 0, level.TotalSlots);
                    }
                }
            }

            magic.PreparedCount = magic.SpellLevels
                .SelectMany(level => level.Spells)
                .Count(spell => spell.IsPrepared && !spell.IsAlwaysPrepared);
        }
        catch
        {
        }
    }

    private static void CopyPersistedMagicState(MagicOverviewModel source, MagicOverviewModel target)
    {
        Dictionary<string, bool> preparedById = source.SpellLevels
            .SelectMany(level => level.Spells)
            .Where(spell => !string.IsNullOrWhiteSpace(spell.Id))
            .ToDictionary(spell => spell.Id, spell => spell.IsPrepared, StringComparer.OrdinalIgnoreCase);

        Dictionary<string, bool> preparedByName = source.SpellLevels
            .SelectMany(level => level.Spells)
            .ToDictionary(spell => spell.Name, spell => spell.IsPrepared, StringComparer.OrdinalIgnoreCase);

        foreach (MagicSpellListEntryModel spell in target.SpellLevels.SelectMany(level => level.Spells))
        {
            if (spell.IsAlwaysPrepared)
            {
                spell.IsPrepared = true;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(spell.Id) && preparedById.TryGetValue(spell.Id, out bool preparedByIdentifier))
            {
                spell.IsPrepared = preparedByIdentifier;
            }
            else if (preparedByName.TryGetValue(spell.Name, out bool preparedBySpellName))
            {
                spell.IsPrepared = preparedBySpellName;
            }
        }

        Dictionary<int, int> usedByLevel = source.SpellLevels.ToDictionary(level => level.Level, level => level.UsedSlots);
        foreach (MagicSpellLevelModel level in target.SpellLevels)
        {
            if (usedByLevel.TryGetValue(level.Level, out int used))
            {
                level.UsedSlots = Math.Clamp(used, 0, level.TotalSlots);
            }
        }

        target.PreparedCount = target.SpellLevels
            .SelectMany(level => level.Spells)
            .Count(spell => spell.IsPrepared && !spell.IsAlwaysPrepared);
    }

    private static void PersistMagicState(string absolutePath, MagicOverviewModel magic)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return;
        }

        XmlDocument document = new();
        document.Load(absolutePath);

        Dictionary<string, bool> preparedById = magic.SpellLevels
            .SelectMany(level => level.Spells)
            .Where(spell => !string.IsNullOrWhiteSpace(spell.Id))
            .ToDictionary(spell => spell.Id, spell => spell.IsPrepared, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, bool> preparedByName = magic.SpellLevels
            .SelectMany(level => level.Spells)
            .ToDictionary(spell => spell.Name, spell => spell.IsPrepared, StringComparer.OrdinalIgnoreCase);

        XmlNode? buildNode = document.DocumentElement?["build"];
        XmlNode? magicNode = buildNode?["magic"];
        if (magicNode is not null)
        {
            foreach (XmlNode spellcastingNode in magicNode.ChildNodes)
            {
                if (spellcastingNode.Name != "spellcasting")
                {
                    continue;
                }

                XmlNode? spellsNode = spellcastingNode["spells"];
                if (spellsNode is null)
                {
                    continue;
                }

                foreach (XmlNode spellNode in spellsNode.ChildNodes)
                {
                    if (spellNode.Name != "spell")
                    {
                        continue;
                    }

                    string? spellId = spellNode.Attributes?["id"]?.Value;
                    string? spellName = spellNode.Attributes?["name"]?.Value;
                    if (!TryGetPreparedValue(preparedById, preparedByName, spellId, spellName, out bool isPrepared))
                    {
                        continue;
                    }

                    string? alwaysPrepared = spellNode.Attributes?["always-prepared"]?.Value;
                    if (string.Equals(alwaysPrepared, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    XmlAttributeCollection? attributes = spellNode.Attributes;
                    XmlAttribute? preparedAttribute = attributes?["prepared"];
                    if (isPrepared)
                    {
                        if (preparedAttribute is null)
                        {
                            XmlAttribute created = document.CreateAttribute("prepared");
                            created.Value = "true";
                            attributes?.Append(created);
                        }
                        else
                        {
                            preparedAttribute.Value = "true";
                        }
                    }
                    else if (preparedAttribute is not null)
                    {
                        attributes?.Remove(preparedAttribute);
                    }
                }
            }
        }

        XmlElement root = document.DocumentElement ?? throw new InvalidOperationException("Character file is missing a root element.");
        XmlElement sessionNode = root["session"] ?? document.CreateElement("session");
        if (sessionNode.ParentNode is null)
        {
            root.AppendChild(sessionNode);
        }

        XmlElement spellSlotsNode = sessionNode["spellslots"] ?? document.CreateElement("spellslots");
        if (spellSlotsNode.ParentNode is null)
        {
            sessionNode.AppendChild(spellSlotsNode);
        }
        spellSlotsNode.RemoveAll();

        foreach (MagicSpellLevelModel level in magic.SpellLevels.Where(level => level.UsedSlots > 0))
        {
            XmlElement slotNode = document.CreateElement("slot");
            slotNode.SetAttribute("level", level.Level.ToString());
            slotNode.SetAttribute("used", level.UsedSlots.ToString());
            spellSlotsNode.AppendChild(slotNode);
        }

        using XmlTextWriter writer = new(absolutePath, Encoding.UTF8)
        {
            Formatting = Formatting.Indented,
            IndentChar = '\t',
            Indentation = 1
        };
        document.Save(writer);
    }

    private static bool TryGetPreparedValue(
        IReadOnlyDictionary<string, bool> preparedById,
        IReadOnlyDictionary<string, bool> preparedByName,
        string? spellId,
        string? spellName,
        out bool prepared)
    {
        if (!string.IsNullOrWhiteSpace(spellId) && preparedById.TryGetValue(spellId, out prepared))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(spellName) && preparedByName.TryGetValue(spellName, out prepared))
        {
            return true;
        }

        prepared = false;
        return false;
    }

    private static MagicSpellDetailModel? BuildMagicSpellDetail(string id)
    {
        try
        {
            var element = DataManager.Current.ElementsCollection
                .FirstOrDefault(candidate => candidate.Type == "Spell"
                    && candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (element is null)
            {
                return null;
            }

            dynamic spell = element;

            int level = 0;
            string subtitle = string.Empty;
            string castingTime = string.Empty;
            string range = string.Empty;
            string components = string.Empty;
            string duration = string.Empty;

            try { level = (int)spell.Level; } catch { }
            try { subtitle = (string)(spell.Underline ?? string.Empty); } catch { }
            try { castingTime = (string)(spell.CastingTime ?? string.Empty); } catch { }
            try { range = (string)(spell.Range ?? string.Empty); } catch { }
            try { duration = (string)(spell.Duration ?? string.Empty); } catch { }
            try { components = (string)spell.GetComponentsString(); } catch { }

            string body = string.Empty;
            try
            {
                string raw = (string)(spell.Description ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    body = ElementDescriptionGenerator.GeneratePlainDescription(raw).Trim();
                }
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(castingTime) && string.IsNullOrEmpty(range))
            {
                List<string> bodyLines = [];
                foreach (string line in body.Split('\n').Select(line => line.Trim()))
                {
                    if (TryParseSpellHeader(line, "Casting Time", out string headerCastingTime)) { castingTime = headerCastingTime; continue; }
                    if (TryParseSpellHeader(line, "Range", out string headerRange)) { range = headerRange; continue; }
                    if (TryParseSpellHeader(line, "Components", out string headerComponents)) { components = headerComponents; continue; }
                    if (TryParseSpellHeader(line, "Duration", out string headerDuration)) { duration = headerDuration; continue; }
                    bodyLines.Add(line);
                }

                body = string.Join("\n", bodyLines).Trim();
            }

            return new MagicSpellDetailModel(
                element.Id,
                element.Name ?? string.Empty,
                element.Source ?? string.Empty,
                level,
                subtitle,
                castingTime,
                range,
                components,
                duration,
                body);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<SourceRestrictionGroupModel> BuildSourceGroups() =>
        CharacterManager.Current.SourcesManager.SourceGroups
            .Select(group => new SourceRestrictionGroupModel(
                group.Name,
                group.Name,
                group.Underline,
                group.AllowUnchecking,
                group.IsChecked,
                group.Sources.Select(item => new SourceRestrictionItemModel(
                    item.Source.Id,
                    item.Source.Name ?? string.Empty,
                    item.IsChecked,
                    item.AllowUnchecking,
                    !item.AllowUnchecking)).ToList()))
            .ToList();

    private static EditableCharacterInfoModel BuildInfoModel(Character character) =>
        new()
        {
            Name = character.Name ?? string.Empty,
            PlayerName = character.PlayerName ?? string.Empty,
            Experience = character.Experience,
            Deity = character.Deity ?? string.Empty,
            Alignment = character.Alignment ?? string.Empty,
            Age = character.Age ?? string.Empty,
            Gender = character.Gender ?? string.Empty,
            Height = character.Height ?? string.Empty,
            Weight = character.Weight ?? string.Empty,
            Eyes = character.Eyes ?? string.Empty,
            Hair = character.Hair ?? string.Empty,
            Skin = character.Skin ?? string.Empty,
            Backstory = character.Backstory ?? string.Empty,
            Organisation = character.OrganisationName ?? string.Empty,
            Allies = character.Allies ?? string.Empty,
            Trinket = character.Trinket?.Content ?? character.Trinket?.OriginalContent ?? string.Empty,
            Notes1 = character.Notes1 ?? string.Empty,
            Notes2 = character.Notes2 ?? string.Empty
        };

    private static void ApplyInfoModel(Character character, EditableCharacterInfoModel info)
    {
        character.Name = info.Name;
        character.PlayerName = info.PlayerName;
        character.Experience = info.Experience;
        character.Deity = info.Deity;
        character.Alignment = info.Alignment;
        character.Backstory = info.Backstory;
        character.OrganisationName = info.Organisation;
        character.Allies = info.Allies;
        character.Notes1 = info.Notes1;
        character.Notes2 = info.Notes2;
        character.Gender = info.Gender;
        character.Eyes = info.Eyes;
        character.Skin = info.Skin;
        character.Hair = info.Hair;
        character.AgeField.Content = info.Age;
        character.HeightField.Content = info.Height;
        character.WeightField.Content = info.Weight;
        character.BackgroundStory.Content = info.Backstory;
        if (!string.IsNullOrEmpty(info.Trinket))
        {
            character.Trinket.Content = info.Trinket;
        }
    }

    private static void ApplyAndPersistSourceRestrictions(PhaseZeroSessionWorkspace workspace, string relativePath)
    {
        CharacterManager.Current.SourcesManager.ApplyRestrictions(reprocess: true);
        string absolutePath = ResolveWorkspaceFile(workspace, relativePath);
        SaveSourceRestrictions(absolutePath);
    }

    private static void SaveSourceRestrictions(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return;
        }

        XmlDocument document = new();
        document.Load(absolutePath);

        XmlElement? root = document.DocumentElement;
        if (root is null)
        {
            return;
        }

        XmlNode? existing = root["sources"];
        if (existing is not null)
        {
            root.RemoveChild(existing);
        }

        XmlElement sourcesNode = document.CreateElement("sources");
        XmlElement restrictedNode = document.CreateElement("restricted");

        foreach (SourceItem item in CharacterManager.Current.SourcesManager.RestrictedSources)
        {
            XmlElement sourceElement = document.CreateElement("source");
            sourceElement.InnerText = item.Source.Name;
            sourceElement.SetAttribute("id", item.Source.Id);
            restrictedNode.AppendChild(sourceElement);
        }

        foreach (string elementId in CharacterManager.Current.SourcesManager.GetRestrictedElementIds())
        {
            XmlElement elementNode = document.CreateElement("element");
            elementNode.InnerText = elementId;
            restrictedNode.AppendChild(elementNode);
        }

        sourcesNode.AppendChild(restrictedNode);
        root.AppendChild(sourcesNode);

        using XmlTextWriter writer = new(absolutePath, Encoding.UTF8)
        {
            Formatting = Formatting.Indented,
            IndentChar = '\t',
            Indentation = 1
        };

        document.Save(writer);
    }

    private static IReadOnlyCollection<string> GetPreparedIds(string spellcastingName) =>
        SpellcastingSectionContext.Current?.GetPreparedIds(spellcastingName) ?? Array.Empty<string>();

    private static IReadOnlyList<(string Id, string Name)> CollectCantrips() =>
        CharacterManager.Current.GetElements()
            .Where(element => element.Type == "Spell" && GetSpellLevel(element) == 0)
            .Select(element => (element.Id ?? element.Name ?? string.Empty, element.Name ?? string.Empty))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Item2))
            .DistinctBy(entry => entry.Item1)
            .OrderBy(entry => entry.Item2)
            .ToList();

    private static IReadOnlyList<MagicSpellLevelModel> CollectSpellLevels(
        bool isPreparedCaster,
        string supportsExpr,
        IReadOnlyCollection<string> preparedIds,
        bool isSpellbookCaster = false)
    {
        int[] totalSlots = new int[10];
        try
        {
            var info = CharacterManager.Current.GetSpellcastingInformations().FirstOrDefault(candidate => !candidate.IsExtension);
            if (info is not null)
            {
                for (int level = 1; level <= 9; level++)
                {
                    totalSlots[level] = CharacterManager.Current.StatisticsCalculator.StatisticValues.GetValue(info.GetSlotStatisticName(level));
                }
            }
        }
        catch
        {
        }

        int maxSlot = 0;
        for (int level = 9; level >= 1; level--)
        {
            if (totalSlots[level] > 0)
            {
                maxSlot = level;
                break;
            }
        }

        if (isPreparedCaster && isSpellbookCaster)
        {
            return CollectPreparedCasterSpellLevels(string.Empty, preparedIds, totalSlots, maxSlot, true);
        }

        if (isPreparedCaster && !string.IsNullOrEmpty(supportsExpr))
        {
            return CollectPreparedCasterSpellLevels(supportsExpr, preparedIds, totalSlots, maxSlot, false);
        }

        var spellsByLevel = CharacterManager.Current.GetElements()
            .Where(element => element.Type == "Spell")
            .Select(element => (Name: element.Name ?? string.Empty, Id: element.Id ?? string.Empty, Level: GetSpellLevel(element)))
            .Where(spell => spell.Level > 0 && !string.IsNullOrEmpty(spell.Name))
            .GroupBy(spell => spell.Level)
            .ToDictionary(group => group.Key, group =>
                group.GroupBy(spell => spell.Id)
                    .Select(grouped => grouped.First())
                    .OrderBy(spell => spell.Name)
                    .ToList());

        List<MagicSpellLevelModel> result = [];
        for (int level = 1; level <= 9; level++)
        {
            spellsByLevel.TryGetValue(level, out List<(string Name, string Id, int Level)>? spells);
            List<MagicSpellListEntryModel> entries = (spells ?? [])
                .Select(spell => new MagicSpellListEntryModel(spell.Id, spell.Name, true, true))
                .ToList();
            if (entries.Count > 0 || totalSlots[level] > 0)
            {
                result.Add(new MagicSpellLevelModel(level, entries, totalSlots[level], 0));
            }
        }

        return result;
    }

    private static IReadOnlyList<MagicSpellLevelModel> CollectPreparedCasterSpellLevels(
        string supportsExpr,
        IReadOnlyCollection<string> preparedIds,
        int[] totalSlots,
        int maxSlot,
        bool isSpellbookCaster)
    {
        int effectiveMax = maxSlot > 0 ? maxSlot : 9;
        HashSet<string> alwaysPreparedIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (var element in CharacterManager.Current.GetElements().Where(element => element.Type == "Spell"))
        {
            try
            {
                dynamic dynamicElement = element;
                if ((bool)dynamicElement.Aquisition.WasGranted && (bool)dynamicElement.Aquisition.GrantRule.IsAlwaysPrepared())
                {
                    alwaysPreparedIds.Add((string)dynamicElement.Id);
                }
            }
            catch
            {
            }
        }

        var sourcesManager = CharacterManager.Current.SourcesManager;
        HashSet<string> restrictedIds = new(sourcesManager.GetRestrictedElementIds(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> restrictedSources = new(sourcesManager.GetUndefinedRestrictedSourceNames(), StringComparer.OrdinalIgnoreCase);

        bool IsRestricted(string id, string source) =>
            restrictedIds.Contains(id) || restrictedSources.Contains(source);

        List<(string Name, string Id, int Level, string Source)> allSpellList;
        if (isSpellbookCaster)
        {
            allSpellList = CharacterManager.Current.GetElements()
                .Where(element => element.Type == "Spell")
                .Select(element => (element.Name ?? string.Empty, element.Id ?? string.Empty, GetSpellLevel(element), element.Source ?? string.Empty))
                .Where(spell => spell.Item3 > 0 && spell.Item3 <= effectiveMax && !string.IsNullOrEmpty(spell.Item1))
                .GroupBy(spell => spell.Item2)
                .Select(group => group.First())
                .OrderBy(spell => spell.Item1)
                .ToList();
        }
        else
        {
            var spellBase = DataManager.Current.ElementsCollection.Where(element => element.Type == "Spell");

            IEnumerable<object> filteredSpells;
            try
            {
                dynamic interpreter = new ExpressionInterpreter();
                filteredSpells = (IEnumerable<object>)interpreter.EvaluateSupportsExpression(supportsExpr, spellBase.Cast<object>());
            }
            catch
            {
                filteredSpells = spellBase.Where(element => element.Supports != null && element.Supports.Contains(supportsExpr)).Cast<object>();
            }

            var classSpells = filteredSpells
                .Select(element => (
                    Name: (string?)((dynamic)element).Name ?? string.Empty,
                    Id: (string?)((dynamic)element).Id ?? string.Empty,
                    Level: GetSpellLevel(element),
                    Source: (string?)((dynamic)element).Source ?? string.Empty))
                .Where(spell => spell.Level > 0 && spell.Level <= effectiveMax && !string.IsNullOrEmpty(spell.Name))
                .ToList();

            HashSet<string> classSpellIds = new(classSpells.Select(spell => spell.Id), StringComparer.OrdinalIgnoreCase);

            var extraSpells = CharacterManager.Current.GetElements()
                .Where(element => element.Type == "Spell")
                .Select(element => (
                    Name: element.Name ?? string.Empty,
                    Id: element.Id ?? string.Empty,
                    Level: GetSpellLevel(element),
                    Source: element.Source ?? string.Empty))
                .Where(spell => spell.Level > 0
                    && spell.Level <= effectiveMax
                    && !string.IsNullOrEmpty(spell.Name)
                    && !classSpellIds.Contains(spell.Id))
                .ToList();

            allSpellList = classSpells.Concat(extraSpells)
                .GroupBy(spell => spell.Id)
                .Select(group => group.First())
                .OrderBy(spell => spell.Name)
                .ToList();
        }

        var byLevel = allSpellList
            .Where(spell => !IsRestricted(spell.Id, spell.Source) || preparedIds.Contains(spell.Id) || alwaysPreparedIds.Contains(spell.Id))
            .GroupBy(spell => spell.Level)
            .ToDictionary(group => group.Key, group =>
                group.GroupBy(spell => spell.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(nameGroup =>
                    {
                        var preferred = nameGroup.FirstOrDefault(spell => preparedIds.Contains(spell.Id) || alwaysPreparedIds.Contains(spell.Id));
                        return string.IsNullOrEmpty(preferred.Id) ? nameGroup.First() : preferred;
                    })
                    .OrderBy(spell => spell.Name)
                    .ToList());

        List<MagicSpellLevelModel> result = [];
        for (int level = 1; level <= 9; level++)
        {
            byLevel.TryGetValue(level, out List<(string Name, string Id, int Level, string Source)>? spells);
            List<MagicSpellListEntryModel> entries = (spells ?? [])
                .Select(spell => new MagicSpellListEntryModel(
                    spell.Id,
                    spell.Name,
                    alwaysPreparedIds.Contains(spell.Id) || preparedIds.Contains(spell.Id),
                    alwaysPreparedIds.Contains(spell.Id)))
                .ToList();
            if (entries.Count > 0 || totalSlots[level] > 0)
            {
                result.Add(new MagicSpellLevelModel(level, entries, totalSlots[level], 0));
            }
        }

        return result;
    }

    private static bool TryParseSpellHeader(string line, string key, out string value)
    {
        string prefix = key + ":";
        if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = line[prefix.Length..].Trim();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static int GetSpellLevel(object element)
    {
        try { return (int)((dynamic)element).Level; }
        catch { return 0; }
    }

    private static string ResolveWorkspaceFile(PhaseZeroSessionWorkspace workspace, string relativePath)
    {
        string absolutePath = Path.GetFullPath(Path.Combine(workspace.WorkspacePath, relativePath));
        string root = Path.GetFullPath(workspace.WorkspacePath);
        if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Requested file is outside the current session workspace.");

        return absolutePath;
    }
}
