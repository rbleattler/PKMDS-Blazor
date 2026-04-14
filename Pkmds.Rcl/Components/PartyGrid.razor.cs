namespace Pkmds.Rcl.Components;

public partial class PartyGrid : RefreshAwareComponent
{
    private static readonly DialogOptions ImportExportDialogOptions = new() { CloseOnEscapeKey = true, MaxWidth = MaxWidth.Medium, FullWidth = true };

    protected override RefreshEvents SubscribeTo => RefreshEvents.AppState | RefreshEvents.PartyState;

    private void SetSelectedPokemon(PKM? pokemon, int slotNumber) =>
        AppService.SetSelectedPartyPokemon(pokemon, slotNumber);

    private string GetClass(int slotNumber) => AppState.SelectedPartySlotNumber == slotNumber
        ? Constants.SelectedSlotClass
        : string.Empty;

    private void ExportAsShowdown() =>
        DialogService.ShowAsync<ShowdownExportDialog>(
            "Showdown Export",
            new DialogOptions { CloseOnEscapeKey = true });

    private void ExportToPokePaste() =>
        DialogService.ShowAsync<PokePasteExportDialog>(
            "Export to PokePaste",
            new DialogParameters<PokePasteExportDialog>(),
            ImportExportDialogOptions);

    private void ImportFromShowdown() =>
        DialogService.ShowAsync<ShowdownImportDialog>(
            "Import from Showdown / PokePaste",
            new DialogParameters<ShowdownImportDialog>(),
            ImportExportDialogOptions);
}
