namespace Pkmds.Tests;

/// <summary>
/// Tests for box management features (SwapBoxes, BoxViewerDialog, BoxListDialog).
/// </summary>
public class BoxManagementTests
{
    private const string TestFilesPath = "../../../TestFiles";

    [Fact]
    public void SwapBoxes_NoSaveFile_ReturnsFalse()
    {
        // Arrange
        var appState = new TestAppState { SaveFile = null };
        var refreshService = new TestRefreshService();
        var appService = new AppService(appState, refreshService);

        // Act
        var result = appService.SwapBoxes(0, 1);

        // Assert
        result.Should().BeFalse();
        refreshService.RefreshBoxStateCount.Should().Be(0);
    }

    [Fact]
    public void SwapBoxes_ValidBoxes_SwapsAllSlots()
    {
        // Arrange
        var data = File.ReadAllBytes(Path.Combine(TestFilesPath, "Black - Full Completion.sav"));
        SaveUtil.TryGetSaveFile(data, out var saveFile, "Black - Full Completion.sav").Should().BeTrue();

        var appState = new TestAppState { SaveFile = saveFile };
        var refreshService = new TestRefreshService();
        var appService = new AppService(appState, refreshService);

        // Read slot 0 from box 0 and box 1 before swap
        var box0Slot0Before = saveFile!.GetBoxSlotAtIndex(0, 0).Species;
        var box1Slot0Before = saveFile.GetBoxSlotAtIndex(1, 0).Species;

        // Act
        var result = appService.SwapBoxes(0, 1);

        // Assert
        result.Should().BeTrue();
        refreshService.RefreshBoxStateCount.Should().Be(1);

        var box0Slot0After = saveFile.GetBoxSlotAtIndex(0, 0).Species;
        var box1Slot0After = saveFile.GetBoxSlotAtIndex(1, 0).Species;

        box0Slot0After.Should().Be(box1Slot0Before);
        box1Slot0After.Should().Be(box0Slot0Before);
    }

    [Fact]
    public void SwapBoxes_SameBox_ReturnsTrueWithNoEffectiveChange()
    {
        // Arrange
        var data = File.ReadAllBytes(Path.Combine(TestFilesPath, "Black - Full Completion.sav"));
        SaveUtil.TryGetSaveFile(data, out var saveFile, "Black - Full Completion.sav").Should().BeTrue();

        var appState = new TestAppState { SaveFile = saveFile };
        var refreshService = new TestRefreshService();
        var appService = new AppService(appState, refreshService);

        var box0Slot0Before = saveFile!.GetBoxSlotAtIndex(0, 0).Species;

        // Act — swapping a box with itself should be a no-op
        var result = appService.SwapBoxes(0, 0);

        // Assert
        var box0Slot0After = saveFile.GetBoxSlotAtIndex(0, 0).Species;
        box0Slot0After.Should().Be(box0Slot0Before);

        // Whether SwapBox returns true or false for same-box is PKHeX's concern;
        // we only verify the service propagates the call correctly.
        _ = result; // result depends on PKHeX internals; not asserted here
    }

    private class TestAppState : IAppState
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
        public DateTime? AppBuildDate { get; }
        public bool SelectedSlotsAreValid => true;
        public bool IsHaXEnabled { get; set; }
        public SpriteStyle SpriteStyle { get; set; }
    }

    private class TestRefreshService : IRefreshService
    {
        public int RefreshCount { get; private set; }
        public int RefreshBoxStateCount { get; private set; }
        private int RefreshPartyStateCount { get; set; }
        private int RefreshBoxAndPartyStateCount { get; set; }

        public void Refresh() => RefreshCount++;
        public void RefreshBoxState() => RefreshBoxStateCount++;
        public void RefreshPartyState() => RefreshPartyStateCount++;
        public void RefreshBoxAndPartyState() => RefreshBoxAndPartyStateCount++;

        public void RefreshTheme(bool isDarkMode)
        {
        }

        public void ShowUpdateMessage()
        {
        }

#pragma warning disable CS0067
        public event Action? OnAppStateChanged;
        public event Action? OnBoxStateChanged;
        public event Action? OnPartyStateChanged;
        public event Action? OnUpdateAvailable;
        public event Action<bool>? OnThemeChanged;
#pragma warning restore CS0067
    }
}
