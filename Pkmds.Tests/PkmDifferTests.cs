namespace Pkmds.Tests;

public class PkmDifferTests
{
    [Fact]
    public void Diff_IdenticalPkms_ReturnsEmpty()
    {
        var before = new PK9 { Species = (ushort)Species.Pikachu };
        var after = before.Clone();

        var result = PkmDiffer.Diff(before, after);

        result.IsEmpty.Should().BeTrue();
        result.Count.Should().Be(0);
    }

    [Fact]
    public void Diff_NullArguments_ReturnsEmpty()
    {
        PkmDiffer.Diff(null, null).IsEmpty.Should().BeTrue();
        PkmDiffer.Diff(new PK9(), null).IsEmpty.Should().BeTrue();
        PkmDiffer.Diff(null, new PK9()).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Diff_DifferentMoves_ReportsBattleCategory()
    {
        var before = new PK9 { Species = (ushort)Species.Pikachu, Move1 = (ushort)Move.Tackle };
        var after = before.Clone();
        after.Move1 = (ushort)Move.Thunderbolt;

        var result = PkmDiffer.Diff(before, after);

        result.Count.Should().Be(1);
        var change = result.Changes[0];
        change.Category.Should().Be(LegalizationChangeCategory.Battle);
        change.FieldLabel.Should().Be("Move 1");
        change.OldValue.Should().NotBeNullOrEmpty();
        change.NewValue.Should().NotBeNullOrEmpty();
        change.OldValue.Should().NotBe(change.NewValue);
    }

    [Fact]
    public void Diff_DifferentIVs_ReportsStatsCategory()
    {
        var before = new PK9 { Species = (ushort)Species.Pikachu, IV_HP = 0, IV_ATK = 31 };
        var after = before.Clone();
        after.IV_HP = 31;

        var result = PkmDiffer.Diff(before, after);

        result.Count.Should().Be(1);
        var change = result.Changes[0];
        change.Category.Should().Be(LegalizationChangeCategory.Stats);
        change.FieldLabel.Should().Be("HP IV");
        change.OldValue.Should().Be("0");
        change.NewValue.Should().Be("31");
    }

    [Fact]
    public void Diff_HyperTrainingFlag_ReportedOnly_WhenInterfaceMatches()
    {
        // PK9 implements IHyperTrain — flag flip should surface.
        var before = new PK9();
        var after = before.Clone();
        after.HT_HP = true;

        var result = PkmDiffer.Diff(before, after);

        result.Changes.Should().ContainSingle(c => c.FieldLabel == "Hyper Train HP");

        // PK3 does not implement IHyperTrain — comparing two PK3s must not throw or
        // attempt to read HT_* properties off a non-IHyperTrain entity.
        var pk3Before = new PK3 { Species = (ushort)Species.Pikachu };
        var pk3After = pk3Before.Clone();
        var pk3Result = PkmDiffer.Diff(pk3Before, pk3After);
        pk3Result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Diff_DifferentBall_ReportsOriginCategory_WithBallNames()
    {
        var before = new PK9 { Species = (ushort)Species.Pikachu, Ball = (byte)Ball.Poke };
        var after = before.Clone();
        after.Ball = (byte)Ball.Master;

        var result = PkmDiffer.Diff(before, after);

        var ballChange = result.Changes.Should().ContainSingle(c => c.FieldLabel == "Ball").Subject;
        ballChange.Category.Should().Be(LegalizationChangeCategory.Origin);
        ballChange.OldValue.Should().NotBe(ballChange.NewValue);
    }

    [Fact]
    public void Diff_RibbonCountDelta_CollapsesToSingleEntry()
    {
        // Flip many ribbons in both directions — the differ should still emit just one
        // "Ribbons" line so the dialog isn't dominated by ribbon noise.
        var before = new PK9 { RibbonChampionKalos = true, RibbonChampionG3 = true };
        var after = before.Clone();
        after.RibbonChampionKalos = false; // removed
        after.RibbonChampionG3 = false; // removed
        after.RibbonChampionAlola = true; // added
        after.RibbonChampionGalar = true; // added
        after.RibbonChampionPaldea = true; // added

        var result = PkmDiffer.Diff(before, after);

        var ribbonRows = result.Changes.Where(c => c.FieldLabel == "Ribbons").ToList();
        ribbonRows.Should().ContainSingle();
        ribbonRows[0].Category.Should().Be(LegalizationChangeCategory.Cosmetic);
        ribbonRows[0].NewValue.Should().Contain("+3");
        ribbonRows[0].NewValue.Should().Contain("-2");
    }

    [Fact]
    public void Diff_Markings_DecodedPerSlot_ForGen7Plus()
    {
        // Gen 7+ stores 2-bit MarkingColor per slot. The diff should report each slot
        // by name (Circle, Triangle, …) with human-readable colour, not packed hex.
        var before = new PK9 { MarkingTriangle = MarkingColor.Blue };
        var after = before.Clone();
        after.MarkingCircle = MarkingColor.Pink;
        after.MarkingDiamond = MarkingColor.Blue;

        var result = PkmDiffer.Diff(before, after);

        var markingChanges = result.Changes
            .Where(c => c.FieldLabel.StartsWith("Marking ", StringComparison.Ordinal))
            .ToList();
        markingChanges.Should().HaveCount(2);
        markingChanges.Should().Contain(c => c.FieldLabel == "Marking (Circle)" && c.OldValue == "None" && c.NewValue == "Pink");
        markingChanges.Should().Contain(c => c.FieldLabel == "Marking (Diamond)" && c.OldValue == "None" && c.NewValue == "Blue");
    }

    [Fact]
    public void Diff_Markings_DecodedPerSlot_ForGen3Through6()
    {
        // Gen 3-6 stores a bool per slot. Format should be Set/Unset, not raw bools.
        var before = new PK4();
        var after = before.Clone();
        after.MarkingHeart = true;

        var result = PkmDiffer.Diff(before, after);

        result.Changes.Should().ContainSingle(c => c.FieldLabel == "Marking (Heart)")
            .Which.Should().Match<LegalizationChange>(c => c.OldValue == "Unset" && c.NewValue == "Set");
    }

    [Fact]
    public void Diff_PidAndEncryptionConstant_ReportedAsInternal()
    {
        var before = new PK9 { PID = 0x11111111, EncryptionConstant = 0x22222222 };
        var after = before.Clone();
        after.PID = 0xAAAAAAAA;
        after.EncryptionConstant = 0xBBBBBBBB;

        var result = PkmDiffer.Diff(before, after);

        result.Changes.Should().Contain(c => c.FieldLabel == "PID" && c.Category == LegalizationChangeCategory.Internal);
        result.Changes.Should().Contain(c => c.FieldLabel == "Encryption Constant" && c.Category == LegalizationChangeCategory.Internal);
    }

    [Fact]
    public void ByCategory_GroupsChangesAcrossMultipleCategories()
    {
        var before = new PK9
        {
            Species = (ushort)Species.Pikachu,
            Move1 = (ushort)Move.Tackle,
            IV_HP = 0,
            Ball = (byte)Ball.Poke
        };
        var after = before.Clone();
        after.Move1 = (ushort)Move.Thunderbolt;
        after.IV_HP = 31;
        after.Ball = (byte)Ball.Master;

        var result = PkmDiffer.Diff(before, after);

        var grouped = result.ByCategory().ToDictionary(g => g.Key, g => g.Count());
        grouped.Should().ContainKey(LegalizationChangeCategory.Battle);
        grouped.Should().ContainKey(LegalizationChangeCategory.Stats);
        grouped.Should().ContainKey(LegalizationChangeCategory.Origin);
    }
}
