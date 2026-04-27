namespace Pkmds.Rcl.Components.MainTabPages;

public partial class WonderCardsTab : RefreshAwareComponent
{
    // Recomputed on every render. Cheap (≤49 slots) and avoids stale data after a save reload
    // or a mystery-gift import — RefreshAwareComponent triggers StateHasChanged on AppState
    // events, which re-runs the render path but does not invoke OnParametersSet.
    private IReadOnlyList<WonderCardSlotInfo> Slots => AppService.HasWonderCardSlots()
        ? AppService.GetWonderCardSlots()
        : [];

    private string SpeciesLabel(WonderCardSlotInfo slot) =>
        slot.Species is { } species
            ? AppService.GetPokemonSpeciesName(species) ?? species.ToString(CultureInfo.InvariantCulture)
            : "—";
}
