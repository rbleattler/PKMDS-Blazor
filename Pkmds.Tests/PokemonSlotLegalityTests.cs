using Bunit;
using Microsoft.AspNetCore.Components;

namespace Pkmds.Tests;

/// <summary>
/// bUnit component tests for the legality overlay rendered by <see cref="PokemonSlotComponent" />.
/// </summary>
public class PokemonSlotLegalityTests
{
    [Fact]
    public void PokemonSlotComponent_LegalPokemon_RendersValidOverlay()
    {
        var (saveFile, appState, refreshService, appService) = BunitTestHelpers.LoadSave("Black - Full Completion.sav");
        using var ctx = BunitTestHelpers.CreateBunitContext(appState, refreshService, appService);

        var pkm = saveFile.GetPartySlotAtIndex(0);
        pkm.Species.Should().BeGreaterThan((ushort)0, "need a real Pokémon for the legality overlay to appear");

        var cut = ctx.Render<PokemonSlotComponent>(p => p
            .Add(c => c.SlotNumber, 0)
            .Add(c => c.Pokemon, pkm)
            .Add(c => c.OnSlotClick, EventCallback.Empty)
            .Add(c => c.GetClassFunction, () => string.Empty));

        cut.Markup.Should().Contain("valid.png",
            "a legal Pokémon must render the valid.png legality indicator");
    }

    [Fact]
    public void PokemonSlotComponent_IllegalPokemon_RendersWarnOverlay()
    {
        var (saveFile, appState, refreshService, appService) = BunitTestHelpers.LoadSave("Black - Full Completion.sav");
        using var ctx = BunitTestHelpers.CreateBunitContext(appState, refreshService, appService);

        var pkm = saveFile.GetPartySlotAtIndex(0);
        pkm.AbilityNumber = 4; // force ability violation
        pkm.RefreshChecksum();

        var cut = ctx.Render<PokemonSlotComponent>(p => p
            .Add(c => c.SlotNumber, 0)
            .Add(c => c.Pokemon, pkm)
            .Add(c => c.OnSlotClick, EventCallback.Empty)
            .Add(c => c.GetClassFunction, () => string.Empty));

        cut.Markup.Should().Contain("warn.png",
            "an illegal Pokémon must render the warn.png legality indicator");
    }

    [Fact]
    public void PokemonSlotComponent_EmptySlot_RendersNoLegalityOverlay()
    {
        var (saveFile, appState, refreshService, appService) = BunitTestHelpers.LoadSave("Black - Full Completion.sav");
        using var ctx = BunitTestHelpers.CreateBunitContext(appState, refreshService, appService);

        var blank = saveFile.BlankPKM;
        blank.Species.Should().Be(0, "BlankPKM must have Species 0");

        var cut = ctx.Render<PokemonSlotComponent>(p => p
            .Add(c => c.SlotNumber, 0)
            .Add(c => c.Pokemon, blank)
            .Add(c => c.OnSlotClick, EventCallback.Empty)
            .Add(c => c.GetClassFunction, () => string.Empty));

        cut.Markup.Should().NotContain("valid.png",
            "an empty slot should not show the valid legality overlay");
        cut.Markup.Should().NotContain("warn.png",
            "an empty slot should not show the warn legality overlay");
    }
}
