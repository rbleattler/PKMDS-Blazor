namespace Pkmds.Tests;

/// <summary>
/// Tests for PKHaX (Illegal Mode) wiring through PKHeX's <see cref="FilteredGameDataSource"/>
/// (the bool flag passed to <see cref="LocalizeUtil.InitializeStrings(string, SaveFile?, bool)"/>).
/// Verifies that Z-moves / Max moves / Torque moves and other normally-filtered entries
/// surface in the appropriate dropdown sources when HaX is on.
/// </summary>
public class HaXFilteredSourcesTests
{
    private const string TestFilesPath = "../../../TestFiles";

    // PKHeX move ID for the first Z-move (Move.BreakneckBlitzP). Z-moves occupy 622–658,
    // 695–703, 719, and 723–728 — all excluded by MoveInfo.IsMoveKnowable. 622 is sufficient
    // as a sentinel for the whole class.
    private const int BreakneckBlitzMoveId = 622;

    private static SaveFile LoadSave(string fileName)
    {
        var data = File.ReadAllBytes(Path.Combine(TestFilesPath, fileName));
        SaveUtil.TryGetSaveFile(data, out var saveFile, fileName).Should().BeTrue();
        return saveFile!;
    }

    [Fact]
    public void FilteredMoves_HaXFalse_ExcludesZMoves()
    {
        var sav = LoadSave("ultra sun.sav");

        LocalizeUtil.InitializeStrings(GameLanguage.DefaultLanguage, sav, hax: false);

        GameInfo.FilteredSources.Moves
            .Should().NotContain(m => m.Value == BreakneckBlitzMoveId,
                "Z-moves are excluded from LegalMoveDataSource when HaX is off");
    }

    [Fact]
    public void FilteredMoves_HaXTrue_IncludesZMoves()
    {
        var sav = LoadSave("ultra sun.sav");

        LocalizeUtil.InitializeStrings(GameLanguage.DefaultLanguage, sav, hax: true);

        GameInfo.FilteredSources.Moves
            .Should().Contain(m => m.Value == BreakneckBlitzMoveId,
                "Z-moves come from HaXMoveDataSource when HaX is on");
    }

    [Fact]
    public void FilteredMoves_HaXToggle_RefreshesList()
    {
        var sav = LoadSave("ultra sun.sav");

        LocalizeUtil.InitializeStrings(GameLanguage.DefaultLanguage, sav, hax: false);
        GameInfo.FilteredSources.Moves
            .Should().NotContain(m => m.Value == BreakneckBlitzMoveId);

        // Mid-session toggle: production code re-runs InitializeStrings when
        // AppState.IsHaXEnabled flips, so the dropdown picks up the change
        // without requiring a save-file reload.
        LocalizeUtil.InitializeStrings(GameLanguage.DefaultLanguage, sav, hax: true);
        GameInfo.FilteredSources.Moves
            .Should().Contain(m => m.Value == BreakneckBlitzMoveId);
    }
}
