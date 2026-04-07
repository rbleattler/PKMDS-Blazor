namespace Pkmds.Tests;

/// <summary>
/// Tests for party stats (current HP, status condition) preservation
/// across stat recalculation and save/load cycles.
/// </summary>
public class PartyStatsTests
{
    private const string TestFilesPath = "../../../TestFiles";

    // ── LoadPokemonStats HP preservation ────────────────────────────────────

    [Fact]
    public void LoadPokemonStats_PartyPokemon_PreservesCurrentHp()
    {
        // Arrange — load a save and get a party Pokémon with stats present
        var (saveFile, appState, refreshService, appService) =
            BunitTestHelpers.LoadSave("Lets-Go-Pikachu-All-Pokemon.bin");

        var pkm = saveFile.GetPartySlotAtIndex(0);
        pkm.PartyStatsPresent.Should().BeTrue("party Pokémon should have stats present");

        // Set current HP to a non-max value
        var maxHp = pkm.Stat_HPMax;
        pkm.Stat_HPCurrent = 1;

        // Act — recalculate stats (this would previously overwrite HP)
        appService.LoadPokemonStats(pkm);

        // Assert — current HP should be preserved (clamped to new max)
        pkm.Stat_HPCurrent.Should().Be(1, "current HP should be preserved after stat recalculation");
        pkm.Stat_HPMax.Should().BeGreaterThan(0, "max HP should be recalculated");
    }

    [Fact]
    public void LoadPokemonStats_PartyPokemon_ClampsHpToNewMax()
    {
        // Arrange
        var (saveFile, appState, refreshService, appService) =
            BunitTestHelpers.LoadSave("Lets-Go-Pikachu-All-Pokemon.bin");

        var pkm = saveFile.GetPartySlotAtIndex(0);
        // Set current HP absurdly high (above what recalculated max could be)
        pkm.Stat_HPCurrent = ushort.MaxValue;

        // Act
        appService.LoadPokemonStats(pkm);

        // Assert — should be clamped to the recalculated max
        pkm.Stat_HPCurrent.Should().Be(pkm.Stat_HPMax,
            "current HP should be clamped to new max when it exceeds it");
    }

    [Fact]
    public void LoadPokemonStats_BoxPokemonWithoutPartyStats_SetsHpToMax()
    {
        // Arrange — use a Gen 5 save where box Pokémon don't have party stats
        var (saveFile, appState, refreshService, appService) =
            BunitTestHelpers.LoadSave("Black - Full Completion.sav");

        var pkm = saveFile.GetBoxSlotAtIndex(0, 0);
        pkm.PartyStatsPresent.Should().BeFalse("box Pokémon in Gen 5 should not have party stats");

        // Act
        appService.LoadPokemonStats(pkm);

        // Assert — HP should be set to max (normal recalculation)
        pkm.Stat_HPCurrent.Should().Be(pkm.Stat_HPMax,
            "box Pokémon HP should equal max after stat recalculation");
    }

    // ── PB7 (Let's Go) EditFormPokemon preservation ─────────────────────────

    [Fact]
    public void EditFormPokemon_PB7WithPartyStats_SkipsStatRecalculation()
    {
        // Arrange — load Let's Go save and get a party Pokémon
        var (saveFile, appState, refreshService, appService) =
            BunitTestHelpers.LoadSave("Lets-Go-Pikachu-All-Pokemon.bin");

        var pkm = saveFile.GetPartySlotAtIndex(0);
        pkm.Should().BeOfType<PB7>();
        pkm.PartyStatsPresent.Should().BeTrue();

        // Set custom HP and status
        pkm.Stat_HPCurrent = 1;
        pkm.Status_Condition = (int)StatusCondition.Burn;

        // Act — setting EditFormPokemon triggers clone + potential stat recalculation
        appService.EditFormPokemon = pkm;

        // Assert — the clone should preserve HP and status (not recalculated)
        appService.EditFormPokemon.Should().NotBeNull();
        appService.EditFormPokemon!.Stat_HPCurrent.Should().Be(1,
            "PB7 with party stats should preserve current HP");
        appService.EditFormPokemon.Status_Condition.Should().Be((int)StatusCondition.Burn,
            "PB7 with party stats should preserve status condition");
    }

    [Fact]
    public void EditFormPokemon_PB7WithoutPartyStats_ComputesStats()
    {
        // Arrange — create a fresh PB7 without party stats
        var (saveFile, appState, refreshService, appService) =
            BunitTestHelpers.LoadSave("Lets-Go-Pikachu-All-Pokemon.bin");

        var pkm = (PB7)saveFile.BlankPKM;
        pkm.Species = (ushort)Species.Bulbasaur;
        pkm.CurrentLevel = 50;
        // Stat_HPMax is 0 → PartyStatsPresent is false
        pkm.PartyStatsPresent.Should().BeFalse();

        // Act
        appService.EditFormPokemon = pkm;

        // Assert — stats should be computed since no prior party stats
        appService.EditFormPokemon.Should().NotBeNull();
        appService.EditFormPokemon!.Stat_HPMax.Should().BeGreaterThan(0,
            "stats should be computed for PB7 without prior party stats");
    }

    // ── Let's Go save/load roundtrip ────────────────────────────────────────

    [Fact]
    public void LetsGo_SaveAndReload_PreservesHpAndStatus()
    {
        // Arrange
        var (saveFile, appState, refreshService, appService) =
            BunitTestHelpers.LoadSave("Lets-Go-Pikachu-All-Pokemon.bin");

        // Select and modify a box Pokémon
        var slotNumber = 0;
        var originalPkm = saveFile.GetBoxSlotAtIndex(slotNumber);
        originalPkm.Should().BeOfType<PB7>();

        appService.SetSelectedLetsGoPokemon(originalPkm, slotNumber);
        var editPkm = appService.EditFormPokemon!;

        // Ensure stats are present, then set custom values
        if (!editPkm.PartyStatsPresent)
        {
            editPkm.ResetPartyStats();
        }

        editPkm.Stat_HPCurrent = 1;
        editPkm.Status_Condition = (int)StatusCondition.Burn;

        // Act — save, then re-select
        appService.SavePokemon(editPkm);
        var reloadedPkm = saveFile.GetBoxSlotAtIndex(slotNumber);

        // Assert — data should persist in the save file
        reloadedPkm.Stat_HPCurrent.Should().Be(1,
            "current HP should persist after saving to Let's Go box");
        reloadedPkm.Status_Condition.Should().Be((int)StatusCondition.Burn,
            "status condition should persist after saving to Let's Go box");
    }

    [Fact]
    public void LetsGo_SaveAndReselect_PreservesHpAndStatusInEditForm()
    {
        // Arrange
        var (saveFile, appState, refreshService, appService) =
            BunitTestHelpers.LoadSave("Lets-Go-Pikachu-All-Pokemon.bin");

        var slotNumber = 0;
        var originalPkm = saveFile.GetBoxSlotAtIndex(slotNumber);
        appService.SetSelectedLetsGoPokemon(originalPkm, slotNumber);
        var editPkm = appService.EditFormPokemon!;

        if (!editPkm.PartyStatsPresent)
        {
            editPkm.ResetPartyStats();
        }

        editPkm.Stat_HPCurrent = 1;
        editPkm.Status_Condition = (int)StatusCondition.Burn;

        // Act — save, then re-select (simulating user clicking the slot again)
        appService.SavePokemon(editPkm);
        var savedPkm = saveFile.GetBoxSlotAtIndex(slotNumber);
        appService.SetSelectedLetsGoPokemon(savedPkm, slotNumber);

        // Assert — edit form should show the preserved values
        appService.EditFormPokemon.Should().NotBeNull();
        appService.EditFormPokemon!.Stat_HPCurrent.Should().Be(1,
            "edit form should show preserved HP after re-selecting");
        appService.EditFormPokemon.Status_Condition.Should().Be((int)StatusCondition.Burn,
            "edit form should show preserved status after re-selecting");
    }
}
