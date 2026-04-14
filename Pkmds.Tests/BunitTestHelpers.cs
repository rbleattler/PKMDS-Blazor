using Bunit;
using KristofferStrube.Blazor.FileSystem;
using KristofferStrube.Blazor.FileSystemAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Pkmds.Rcl.Models;
using Serilog.Events;

namespace Pkmds.Tests;

/// <summary>
/// Shared helper infrastructure for bUnit component tests.
/// All helpers follow the same TestAppState / TestRefreshService inner-class pattern as AppServiceTests.
/// </summary>
internal static class BunitTestHelpers
{
    private const string TestFilesPath = "../../../TestFiles";

    /// <summary>
    /// Loads a save file and constructs the matching service instances.
    /// </summary>
    internal static (SaveFile SaveFile, TestAppState AppState, TestRefreshService RefreshService, AppService AppService)
        LoadSave(string saveFileName)
    {
        var data = File.ReadAllBytes(Path.Combine(TestFilesPath, saveFileName));
        SaveUtil.TryGetSaveFile(data, out var saveFile, saveFileName)
            .Should().BeTrue($"'{saveFileName}' must load successfully");
        ParseSettings.InitFromSaveFileData(saveFile!);
        var appState = new TestAppState { SaveFile = saveFile };
        var refreshService = new TestRefreshService();
        var appService = new AppService(appState, refreshService, new LegalizationService());
        return (saveFile!, appState, refreshService, appService);
    }

    /// <summary>
    /// Creates a <see cref="BunitContext" /> with all services required by components in Pkmds.Rcl.
    /// </summary>
    internal static BunitContext CreateBunitContext(
        IAppState appState,
        IRefreshService refreshService,
        IAppService appService)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton(appState);
        ctx.Services.AddSingleton(refreshService);
        ctx.Services.AddSingleton(appService);
        ctx.Services.AddSingleton<IDragDropService>(new NullDragDropService());
        ctx.Services.AddSingleton<IFileSystemAccessService>(new NullFileSystemAccessService());
        ctx.Services.AddSingleton<ILoggingService>(new NullLoggingService());
        ctx.Services.AddSingleton<IDescriptionService>(new NullDescriptionService());
        return ctx;
    }
}

// ── Stub implementations ──────────────────────────────────────────────────────

internal class TestAppState : IAppState
{
    public string CurrentLanguage { get; set; } = "en";
    public int CurrentLanguageId => 2;
    public SaveFile? SaveFile { get; set; }
    public BoxEdit? BoxEdit => null;
    public PKM? CopiedPokemon { get; set; }
    public int? SelectedBoxNumber { get; set; }
    public int? SelectedBoxSlotNumber { get; set; }
    public int? SelectedPartySlotNumber { get; set; }
    public bool ShowProgressIndicator { get; set; }
    public string AppVersion => "Test";
    public DateTime? AppBuildDate => null;
    public int? PinnedBoxNumber { get; set; }
    public string? SaveFileName { get; set; }
    public bool SelectedSlotsAreValid => true;
    public bool IsHaXEnabled { get; set; }
    public SpriteStyle SpriteStyle { get; set; }
}

internal class TestRefreshService : IRefreshService
{
    public int RefreshCount { get; private set; }

    public void Refresh() => RefreshCount++;
    public void RefreshBoxState() { }
    public void RefreshPartyState() { }
    public void RefreshBoxAndPartyState() { }
    public void RefreshTheme(bool isDarkMode) { }
    public void ShowUpdateMessage() { }
    public void RequestJumpToPartyBox() { }

#pragma warning disable CS0067
    public event Action? OnAppStateChanged;
    public event Action? OnBoxStateChanged;
    public event Action? OnPartyStateChanged;
    public event Action? OnUpdateAvailable;
    public event Action<bool>? OnThemeChanged;
    public event Action? OnRequestJumpToPartyBox;
#pragma warning restore CS0067
}

internal class NullLoggingService : ILoggingService
{
    public bool IsVerboseLoggingEnabled { get; set; }
#pragma warning disable CS0067
    public event Action<LogEventLevel>? OnLoggingConfigurationChanged;
#pragma warning restore CS0067
}

internal class NullDescriptionService : IDescriptionService
{
    public Task<MoveSummary?> GetMoveInfoAsync(int moveId, GameVersion version) => Task.FromResult<MoveSummary?>(null);
    public Task<AbilitySummary?> GetAbilityInfoAsync(int abilityId, GameVersion version) => Task.FromResult<AbilitySummary?>(null);
    public Task<ItemSummary?> GetItemInfoAsync(string itemName, GameVersion version) => Task.FromResult<ItemSummary?>(null);
    public Task<string?> GetTmMoveNameAsync(string tmNumber, GameVersion version) => Task.FromResult<string?>(null);
    public Task<string?> GetHmMoveNameAsync(string hmKey, GameVersion version) => Task.FromResult<string?>(null);
}

internal class NullDragDropService : IDragDropService
{
    public PKM? DraggedPokemon { get; set; }
    public int? DragSourceBoxNumber { get; set; }
    public int DragSourceSlotNumber { get; set; }
    public bool IsDragSourceParty { get; set; }
    public bool IsDragging => false;

    public void StartDrag(PKM? pokemon, int? boxNumber, int slotNumber, bool isParty) { }
    public void EndDrag() { }
    public void ClearDrag() { }
}

/// <summary>
/// Stub for <see cref="IFileSystemAccessService" />.
/// All picker methods are not supported in the test environment.
/// </summary>
internal class NullFileSystemAccessService : IFileSystemAccessService
{
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<bool> IsSupportedAsync() => Task.FromResult(false);

    public Task<FileSystemFileHandle[]> ShowOpenFilePickerAsync()
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle[]> ShowOpenFilePickerAsync(OpenFilePickerOptionsStartInWellKnownDirectory? options)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle[]> ShowOpenFilePickerAsync(OpenFilePickerOptionsStartInFileSystemHandle? options)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle[]> ShowOpenFilePickerAsync(FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle[]> ShowOpenFilePickerAsync(OpenFilePickerOptionsStartInWellKnownDirectory? options, FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle[]> ShowOpenFilePickerAsync(OpenFilePickerOptionsStartInFileSystemHandle? options, FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle> ShowSaveFilePickerAsync()
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle> ShowSaveFilePickerAsync(SaveFilePickerOptionsStartInWellKnownDirectory? options)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle> ShowSaveFilePickerAsync(SaveFilePickerOptionsStartInFileSystemHandle? options)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle> ShowSaveFilePickerAsync(FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle> ShowSaveFilePickerAsync(SaveFilePickerOptionsStartInWellKnownDirectory? options, FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemFileHandle> ShowSaveFilePickerAsync(SaveFilePickerOptionsStartInFileSystemHandle? options, FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemDirectoryHandle> ShowDirectoryPickerAsync()
        => throw new NotSupportedException();

    public Task<FileSystemDirectoryHandle> ShowDirectoryPickerAsync(DirectoryPickerOptionsStartInWellKnownDirectory? options)
        => throw new NotSupportedException();

    public Task<FileSystemDirectoryHandle> ShowDirectoryPickerAsync(DirectoryPickerOptionsStartInFileSystemHandle? options)
        => throw new NotSupportedException();

    public Task<FileSystemDirectoryHandle> ShowDirectoryPickerAsync(FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemDirectoryHandle> ShowDirectoryPickerAsync(DirectoryPickerOptionsStartInWellKnownDirectory? options, FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();

    public Task<FileSystemDirectoryHandle> ShowDirectoryPickerAsync(DirectoryPickerOptionsStartInFileSystemHandle? options, FileSystemOptions fileSystemOptions)
        => throw new NotSupportedException();
}
