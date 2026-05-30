using System.Xml.Linq;
using Aurora.Components.Models;

namespace Aurora.Web.Services;

public sealed class WebCharacterSessionService
{
    private readonly PhaseZeroSessionWorkspaceService _workspaceService;
    private readonly WebCharacterEngineService _engine;
    private readonly ILogger<WebCharacterSessionService> _logger;
    private string? _currentCharacterPath;
    private WebCharacterRuntimeState? _currentRuntimeState;
    private WebCharacterMagicState? _currentMagicState;

    public WebCharacterSessionService(
        PhaseZeroSessionWorkspaceService workspaceService,
        WebCharacterEngineService engine,
        ILogger<WebCharacterSessionService> logger)
    {
        _workspaceService = workspaceService;
        _engine = engine;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImportedCharacterSummary>> GetImportedCharactersAsync()
    {
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();

        List<ImportedCharacterSummary> characters = [];
        foreach (ImportedSessionFile file in workspace.ImportedFiles
                                                     .Where(file => file.Kind == ImportedContentKind.CharacterFile)
                                                     .OrderBy(file => file.FileName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                string absolutePath = await _workspaceService.ResolveWorkspacePathAsync(file.RelativePath);
                characters.Add(ReadSummary(absolutePath, file));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to inspect imported character file {RelativePath}", file.RelativePath);
            }
        }

        return characters;
    }

    public async Task<ImportedCharacterSummary?> GetCurrentCharacterAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentCharacterPath))
            return null;

        return await GetCharacterAsync(_currentCharacterPath);
    }

    public Task<WebCharacterRuntimeState?> GetCurrentRuntimeStateAsync() =>
        Task.FromResult(_currentRuntimeState);

    public async Task<ImportedCharacterSummary?> GetCharacterAsync(string relativePath)
    {
        IReadOnlyList<ImportedCharacterSummary> characters = await GetImportedCharactersAsync();
        return characters.FirstOrDefault(character =>
            string.Equals(character.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> SetCurrentCharacterAsync(string relativePath)
    {
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        ImportedCharacterSummary? character = await GetCharacterAsync(relativePath);
        if (character is null)
            return false;

        string absolutePath = await _workspaceService.ResolveWorkspacePathAsync(character.RelativePath);
        _currentRuntimeState = await _engine.OpenCharacterAsync(workspace, absolutePath, character.RelativePath);
        _currentCharacterPath = _currentRuntimeState.Summary.RelativePath;
        _currentMagicState = null;
        return true;
    }

    public async Task<bool> DeleteCharacterAsync(string relativePath)
    {
        bool deleted = await _workspaceService.DeleteCharacterFileAsync(relativePath);
        if (deleted && string.Equals(_currentCharacterPath, relativePath, StringComparison.OrdinalIgnoreCase))
        {
            ClearCurrentCharacter();
        }

        return deleted;
    }

    public void ClearCurrentCharacter()
    {
        _currentCharacterPath = null;
        _currentRuntimeState = null;
        _currentMagicState = null;
    }

    public async Task<WebCharacterRuntimeState> CreateCharacterAsync(string name, string playerName)
    {
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterRuntimeState runtimeState = await _engine.CreateCharacterAsync(workspace, name, playerName);
        string absolutePath = Path.Combine(workspace.WorkspacePath, runtimeState.Summary.RelativePath);
        string relativePath = await _workspaceService.TrackGeneratedCharacterAsync(absolutePath);
        _currentCharacterPath = relativePath;
        _currentRuntimeState = runtimeState with
        {
            Summary = runtimeState.Summary with { RelativePath = relativePath }
        };
        _currentMagicState = null;
        return _currentRuntimeState;
    }

    public async Task<byte[]> DownloadCurrentCharacterFileAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentCharacterPath))
            throw new InvalidOperationException("No character is active in the current web session.");

        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        return await _engine.ExportCharacterFileAsync(workspace, _currentCharacterPath);
    }

    public async Task<byte[]> DownloadCurrentCharacterPdfAsync()
    {
        if (_currentRuntimeState is null)
            throw new InvalidOperationException("No character is active in the current web session.");

        return await _engine.ExportCharacterPdfAsync();
    }

    public async Task<WebCharacterInfoState?> GetCurrentCharacterInfoAsync()
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            return null;
        }

        EditableCharacterInfoModel info = await _engine.GetCurrentCharacterInfoAsync();
        return new WebCharacterInfoState(_currentRuntimeState.Summary, info, _currentRuntimeState.StatusMessage);
    }

    public async Task<WebCharacterInfoState> UpdateCurrentCharacterInfoAsync(EditableCharacterInfoModel info)
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            throw new InvalidOperationException("No character is active in the current web session.");
        }

        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterInfoState state = await _engine.UpdateCurrentCharacterInfoAsync(workspace, _currentCharacterPath, info);
        _currentRuntimeState = _currentRuntimeState with
        {
            Summary = state.Summary,
            StatusMessage = state.StatusMessage
        };
        _currentCharacterPath = state.Summary.RelativePath;
        await PersistMagicIfAvailableAsync();
        return state;
    }

    public async Task<WebCharacterBuildState?> GetCurrentBuildStateAsync()
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            return null;
        }

        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        return await _engine.GetCurrentBuildStateAsync(workspace, _currentCharacterPath, _currentRuntimeState.StatusMessage);
    }

    public async Task<IReadOnlyList<WebBuildSelectionOption>> SearchCurrentBuildSelectionOptionsAsync(string entryKey, string query)
    {
        EnsureActiveCharacter();
        return await _engine.SearchBuildSelectionOptionsAsync(entryKey, query);
    }

    public async Task<WebCharacterBuildState> ChangeCurrentBuildSelectionAsync(string entryKey, string optionId)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterBuildState state = await _engine.ChangeBuildSelectionAsync(workspace, _currentCharacterPath!, entryKey, optionId);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        _currentMagicState = null;
        return state;
    }

    public async Task<WebCharacterSourceState?> GetCurrentSourceStateAsync()
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            return null;
        }

        return await _engine.GetCurrentSourceStateAsync();
    }

    public async Task<WebCharacterSourceState> ToggleCurrentSourceGroupAsync(string groupId)
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            throw new InvalidOperationException("No character is active in the current web session.");
        }

        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterSourceState state = await _engine.ToggleSourceGroupAsync(workspace, _currentCharacterPath, groupId);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterSourceState> ToggleCurrentSourceItemAsync(string sourceId)
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            throw new InvalidOperationException("No character is active in the current web session.");
        }

        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterSourceState state = await _engine.ToggleSourceItemAsync(workspace, _currentCharacterPath, sourceId);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterEquipmentState?> GetCurrentEquipmentStateAsync()
    {
        if (_currentRuntimeState is null)
        {
            return null;
        }

        EquipmentOverviewModel equipment = await _engine.GetCurrentEquipmentStateAsync();
        return new WebCharacterEquipmentState(_currentRuntimeState.Summary, equipment, _currentRuntimeState.StatusMessage);
    }

    public async Task<WebCharacterMagicState?> GetCurrentMagicStateAsync()
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            return null;
        }

        if (_currentMagicState is not null)
        {
            return _currentMagicState;
        }

        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        MagicOverviewModel magic = await _engine.GetCurrentMagicStateAsync(workspace, _currentCharacterPath);
        _currentMagicState = new WebCharacterMagicState(_currentRuntimeState.Summary, magic, _currentRuntimeState.StatusMessage);
        return _currentMagicState;
    }

    public async Task<MagicSpellDetailModel?> GetCurrentMagicSpellDetailAsync(string id)
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return await _engine.GetCurrentMagicSpellDetailAsync(id);
    }

    public async Task<WebCharacterEquipmentState> UpdateCurrentEquipmentCoinAsync(EquipmentCoinChangeModel change)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.UpdateCurrentEquipmentCoinAsync(workspace, _currentCharacterPath!, change);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<IReadOnlyList<WebEquipmentSearchResult>> SearchCurrentEquipmentItemsAsync(string query, string? slotId = null)
    {
        EnsureActiveCharacter();
        return await _engine.SearchEquipmentItemsAsync(query, slotId);
    }

    public async Task<IReadOnlyList<WebEquipmentInventoryOption>> GetCurrentEquipmentInventoryOptionsAsync(string slotId)
    {
        EnsureActiveCharacter();
        return await _engine.GetEquipmentInventoryOptionsAsync(slotId);
    }

    public async Task<WebCharacterEquipmentState> AddCurrentEquipmentItemAsync(string elementId, int amount)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.AddCurrentEquipmentItemAsync(workspace, _currentCharacterPath!, elementId, amount);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterEquipmentState> EquipCurrentEquipmentSlotAsync(string slotId, string? identifier, string? elementId)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.EquipCurrentEquipmentSlotAsync(workspace, _currentCharacterPath!, slotId, identifier, elementId);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterEquipmentState> UpdateCurrentEquipmentNoteAsync(EquipmentNoteChangeModel change)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.UpdateCurrentEquipmentNoteAsync(workspace, _currentCharacterPath!, change);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterEquipmentState> ToggleCurrentEquipmentItemAsync(string identifier, bool attunement)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.ToggleCurrentEquipmentItemAsync(workspace, _currentCharacterPath!, identifier, attunement);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterEquipmentState> ChangeCurrentEquipmentAmountAsync(EquipmentAmountChangeModel change)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.ChangeCurrentEquipmentAmountAsync(workspace, _currentCharacterPath!, change);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterEquipmentState> RemoveCurrentEquipmentItemAsync(string identifier)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.RemoveCurrentEquipmentItemAsync(workspace, _currentCharacterPath!, identifier);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterEquipmentState> UnequipCurrentEquipmentSlotAsync(string slotId)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterEquipmentState state = await _engine.UnequipCurrentEquipmentSlotAsync(workspace, _currentCharacterPath!, slotId);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        await PersistMagicIfAvailableAsync(workspace);
        return state;
    }

    public async Task<WebCharacterMagicState> ToggleCurrentMagicPreparedAsync(MagicPreparedChangeModel change)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterMagicState state = await _engine.ToggleCurrentMagicPreparedAsync(workspace, _currentCharacterPath!, change);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        _currentMagicState = state;
        return state;
    }

    public async Task<WebCharacterMagicState> ToggleCurrentMagicSlotAsync(MagicSlotToggleModel change)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterMagicState state = await _engine.ToggleCurrentMagicSlotAsync(workspace, _currentCharacterPath!, change);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        _currentMagicState = state;
        return state;
    }

    public async Task<IReadOnlyList<WebMagicSelectionOption>> SearchCurrentMagicSelectionOptionsAsync(string entryKey, string query)
    {
        EnsureActiveCharacter();
        return await _engine.SearchMagicSelectionOptionsAsync(entryKey, query);
    }

    public async Task<WebCharacterMagicState> ChangeCurrentMagicSelectionAsync(string entryKey, string elementId)
    {
        EnsureActiveCharacter();
        PhaseZeroSessionWorkspace workspace = await _workspaceService.GetWorkspaceAsync();
        WebCharacterMagicState state = await _engine.ChangeCurrentMagicSelectionAsync(workspace, _currentCharacterPath!, entryKey, elementId);
        SyncRuntimeSummary(state.Summary, state.StatusMessage);
        _currentMagicState = state;
        return state;
    }

    private void EnsureActiveCharacter()
    {
        if (_currentRuntimeState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            throw new InvalidOperationException("No character is active in the current web session.");
        }
    }

    private void SyncRuntimeSummary(ImportedCharacterSummary summary, string statusMessage)
    {
        if (_currentRuntimeState is null)
        {
            return;
        }

        _currentRuntimeState = _currentRuntimeState with
        {
            Summary = summary,
            StatusMessage = statusMessage
        };
        if (_currentMagicState is not null)
        {
            _currentMagicState = _currentMagicState with
            {
                Summary = summary,
                StatusMessage = statusMessage
            };
        }
    }

    private async Task<WebCharacterMagicState> EnsureMagicStateAsync()
    {
        WebCharacterMagicState? state = await GetCurrentMagicStateAsync();
        if (state is null)
        {
            throw new InvalidOperationException("No character is active in the current web session.");
        }

        return state;
    }

    private async Task PersistMagicIfAvailableAsync(PhaseZeroSessionWorkspace? workspace = null)
    {
        if (_currentMagicState is null || string.IsNullOrWhiteSpace(_currentCharacterPath))
        {
            return;
        }

        workspace ??= await _workspaceService.GetWorkspaceAsync();
        await _engine.PersistCurrentMagicStateAsync(workspace, _currentCharacterPath, _currentMagicState.Magic);
    }

    private static ImportedCharacterSummary ReadSummary(string absolutePath, ImportedSessionFile file)
    {
        XDocument document = XDocument.Load(absolutePath, LoadOptions.None);
        XElement? root = document.Root;
        XElement? display = root?.Element("display-properties");
        XElement? build = root?.Element("build");
        XElement? input = build?.Element("input");
        XElement? info = root?.Element("information");

        string displayName = display?.Element("name")?.Value
                             ?? input?.Element("name")?.Value
                             ?? Path.GetFileNameWithoutExtension(file.FileName);

        return new ImportedCharacterSummary(
            file.RelativePath,
            file.FileName,
            displayName,
            input?.Element("player-name")?.Value ?? string.Empty,
            display?.Element("level")?.Value ?? "1",
            display?.Element("race")?.Value ?? string.Empty,
            display?.Element("class")?.Value ?? string.Empty,
            display?.Element("background")?.Value ?? string.Empty,
            info?.Element("group")?.Value ?? string.Empty,
            root?.Attribute("version")?.Value ?? string.Empty,
            file.SizeBytes);
    }
}
