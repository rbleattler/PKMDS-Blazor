namespace Pkmds.Tests;

/// <summary>
/// Tests for <see cref="AppService.GetDirectEvolutions" /> and the evolution-related logic
/// that lives in <c>MainTab</c> (level-bump, Wurmple EC/PID, nickname sync).
/// </summary>
public class EvolveTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    private static AppService CreateService(SaveFile? saveFile = null)
    {
        var appState = new TestAppState { SaveFile = saveFile };
        return new AppService(appState, new TestRefreshService());
    }

    /// <summary>Creates a Gen 6 PK6 with the given species.</summary>
    private static PK6 MakePk6(ushort species, byte form = 0)
    {
        var pk = new PK6 { Species = species, Form = form };
        return pk;
    }

    /// <summary>Creates a Gen 3 PK3 with the given species.</summary>
    private static PK3 MakePk3(ushort species)
    {
        var pk = new PK3 { Species = species };
        return pk;
    }

    // ── GetDirectEvolutions ───────────────────────────────────────────────

    [Fact]
    public void GetDirectEvolutions_FinalEvolution_ReturnsEmpty()
    {
        // Arrange — Charizard has no further evolutions
        var service = CreateService();
        var pk = MakePk6((ushort)Species.Charizard);

        // Act
        var result = service.GetDirectEvolutions(pk);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetDirectEvolutions_SingleEvolution_ReturnsOneMethod()
    {
        // Arrange — Caterpie → Metapod (one branch)
        var service = CreateService();
        var pk = MakePk6((ushort)Species.Caterpie);

        // Act
        var result = service.GetDirectEvolutions(pk);

        // Assert
        result.Should().ContainSingle()
            .Which.Species.Should().Be((ushort)Species.Metapod);
    }

    [Fact]
    public void GetDirectEvolutions_Eevee_ReturnsMultipleMethods()
    {
        // Arrange — Eevee has 8 evolution branches in Gen 6
        var service = CreateService();
        var pk = MakePk6((ushort)Species.Eevee);

        // Act
        var result = service.GetDirectEvolutions(pk);

        // Assert
        result.Should().HaveCountGreaterThan(1);
        result.Should().Contain(m => m.Species == (ushort)Species.Vaporeon);
        result.Should().Contain(m => m.Species == (ushort)Species.Jolteon);
        result.Should().Contain(m => m.Species == (ushort)Species.Flareon);
    }

    [Fact]
    public void GetDirectEvolutions_Nincada_ExcludesShedinja()
    {
        // Arrange — Nincada evolves into Ninjask (primary) and Shedinja (side-effect).
        // GetDirectEvolutions must exclude LevelUpShedinja entries.
        var service = CreateService();
        var pk = MakePk6((ushort)Species.Nincada);

        // Act
        var result = service.GetDirectEvolutions(pk);

        // Assert — only Ninjask; Shedinja is a side-effect, not a direct choice
        result.Should().ContainSingle()
            .Which.Species.Should().Be((ushort)Species.Ninjask);
    }

    // ── ApplyEvolution helpers (tested via AppService indirectly) ─────────
    //    The actual mutation lives in MainTab, but we can validate the PKHeX
    //    APIs used (Wurmple EC/PID matching, level-bump logic) directly here.

    [Fact]
    public void WurmpleGen6_GetWurmpleEncryptionConstant_ProducesMatchingEvoGroup()
    {
        // Arrange
        const WurmpleEvolution evoGroup = WurmpleEvolution.Silcoon;

        // Act
        var ec = WurmpleUtil.GetWurmpleEncryptionConstant(evoGroup);

        // Assert — the EC must resolve back to the same group
        WurmpleUtil.GetWurmpleEvoVal(ec).Should().Be(evoGroup);
    }

    [Fact]
    public void WurmpleGen3_PIDMustMatchEvoGroup()
    {
        // Arrange — simulate what ApplyEvolution does for Gen 3 Wurmple
        var pk = MakePk3((ushort)Species.Wurmple);
        const WurmpleEvolution evoGroup = WurmpleEvolution.Cascoon;

        // Act — loop until a PID matches the desired branch (same loop as ApplyEvolution)
        uint pid;
        var rnd = Util.Rand;
        do
        {
            pid = rnd.Rand32();
        } while (evoGroup != WurmpleUtil.GetWurmpleEvoVal(pid));

        pk.PID = pid;

        // Assert — EC getter on Gen3 returns PID
        pk.EncryptionConstant.Should().Be(pk.PID);
        WurmpleUtil.GetWurmpleEvoVal(pk.EncryptionConstant).Should().Be(evoGroup);
    }

    [Fact]
    public void GetDirectEvolutions_Wurmple_ReturnsSilcoonAndCascoon()
    {
        // Arrange
        var service = CreateService();
        var pk = MakePk6((ushort)Species.Wurmple);

        // Act
        var result = service.GetDirectEvolutions(pk);

        // Assert
        result.Should().Contain(m => m.Species == (ushort)Species.Silcoon);
        result.Should().Contain(m => m.Species == (ushort)Species.Cascoon);
    }

    [Fact]
    public void LevelBump_IsNeeded_WhenCurrentLevelBelowMethodLevel()
    {
        // The level-bump guard in ApplyEvolution:
        //   if (method.Level > 0 && Pokemon.CurrentLevel < method.Level)
        //       Pokemon.CurrentLevel = method.Level;
        var pk = MakePk6((ushort)Species.Nincada);
        pk.CurrentLevel = 5; // below Nincada's evolution level of 20

        var service = CreateService();
        var evolutions = service.GetDirectEvolutions(pk);
        var ninjaskMethod = evolutions.Single(m => m.Species == (ushort)Species.Ninjask);

        // After a bump, level should be at least the method's required level
        var requiredLevel = ninjaskMethod.Level;
        if (requiredLevel > 0 && pk.CurrentLevel < requiredLevel)
        {
            pk.CurrentLevel = requiredLevel;
        }

        pk.CurrentLevel.Should().BeGreaterThanOrEqualTo(requiredLevel);
    }

    [Fact]
    public void GetDestinationForm_PreservesFormWhenAnyForm()
    {
        // Arrange — most evolutions use form = AnyForm (byte.MaxValue), meaning the form is preserved
        var service = CreateService();
        var pk = MakePk6((ushort)Species.Caterpie);

        var evolutions = service.GetDirectEvolutions(pk);
        var method = evolutions.Single();

        // Act
        var destForm = method.GetDestinationForm(pk.Form);

        // Assert — Caterpie has form 0; GetDestinationForm should pass it through
        destForm.Should().Be(pk.Form);
    }

    // ── TryPlacePokemonInFirstAvailableSlot ───────────────────────────────

    [Fact]
    public void TryPlacePokemonInFirstAvailableSlot_NoSaveFile_ReturnsFalse()
    {
        var service = CreateService();
        var pk = MakePk6((ushort)Species.Shedinja);

        var result = service.TryPlacePokemonInFirstAvailableSlot(pk);

        result.Should().BeFalse();
    }

    // ── Sealed test doubles ────────────────────────────────────────────────

    private sealed class TestAppState : IAppState
    {
        public string CurrentLanguage { get; set; } = "en";
        public int CurrentLanguageId => 2;
        public SaveFile? SaveFile { get; set; }
        public BoxEdit? BoxEdit => null;
        public PKM? CopiedPokemon { get; set; }
        public int? SelectedBoxNumber { get; set; }
        public int? SelectedBoxSlotNumber { get; set; }
        public int? SelectedPartySlotNumber { get; set; }
        public int? PinnedBoxNumber { get; set; }
        public bool ShowProgressIndicator { get; set; }
        public string AppVersion => "Test";
        public DateTime? AppBuildDate => null;
        public bool SelectedSlotsAreValid => true;
        public bool IsHaXEnabled { get; set; }
        public SpriteStyle SpriteStyle { get; set; }
    }

    private sealed class TestRefreshService : IRefreshService
    {
        public event Action? OnAppStateChanged { add { } remove { } }
        public event Action? OnBoxStateChanged { add { } remove { } }
        public event Action? OnPartyStateChanged { add { } remove { } }
        public event Action? OnUpdateAvailable { add { } remove { } }
        public event Action<bool>? OnThemeChanged { add { } remove { } }
        public event Action? OnRequestJumpToPartyBox { add { } remove { } }

        public void Refresh() { }
        public void RefreshBoxState() { }
        public void RefreshPartyState() { }
        public void RefreshBoxAndPartyState() { }
        public void RefreshTheme(bool isDarkMode) { }
        public void ShowUpdateMessage() { }
        public void RequestJumpToPartyBox() { }
    }
}
