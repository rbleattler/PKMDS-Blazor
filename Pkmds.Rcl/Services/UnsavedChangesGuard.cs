namespace Pkmds.Rcl.Services;

/// <summary>
/// Centralized prompt for actions that would discard or bypass unsaved Pokémon
/// edits — slot navigation, save-file export, etc. Mirrors the existing
/// "Add to Bank" pattern in <see cref="Components.EditForms.PokemonEditForm"/>.
/// </summary>
public static class UnsavedChangesGuard
{
    /// <summary>
    /// If the edit form has unsaved Pokémon changes, prompts the user to Save,
    /// Discard, or Cancel. Returns true when it is safe to proceed (no edits, or
    /// the user chose Save/Discard); false when the user chose Cancel.
    /// </summary>
    public static async Task<bool> ConfirmAsync(
        IAppService appService,
        IDialogService dialogService,
        string message,
        string saveText = "Save",
        string discardText = "Discard",
        string cancelText = "Cancel")
    {
        if (!appService.EditFormHasUnsavedChanges())
        {
            return true;
        }

        var result = await dialogService.ShowMessageBoxAsync(
            "Unsaved Changes",
            message,
            yesText: saveText,
            noText: discardText,
            cancelText: cancelText);

        if (result is null)
        {
            return false;
        }

        if (result is true)
        {
            appService.SavePokemon(appService.EditFormPokemon);
        }

        return true;
    }
}
