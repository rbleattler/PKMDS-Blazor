namespace Pkmds.Rcl.Components.Dialogs;

public partial class PokePasteExportDialog
{
    private string author = string.Empty;
    private string notes = string.Empty;

    private string title = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    /// <summary>
    /// The Pokémon to export. When <see langword="null" />, the full party is exported.
    /// </summary>
    [Parameter]
    public PKM? Pokemon { get; set; }

    private string ShowdownText => Pokemon is not null
        ? AppService.ExportPokemonAsShowdown(Pokemon)
        : AppService.ExportPartyAsShowdown();

    private async Task CopyToClipboardAsync()
    {
        if (string.IsNullOrWhiteSpace(ShowdownText))
        {
            return;
        }

        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", ShowdownText);
            Snackbar.Add("Copied to clipboard.", Severity.Success);
        }
        catch
        {
            Snackbar.Add("Clipboard unavailable — check browser permissions.", Severity.Warning);
        }
    }

    private async Task UploadToPokePasteAsync()
    {
        if (string.IsNullOrWhiteSpace(ShowdownText))
        {
            return;
        }

        try
        {
            // The PokePaste /create endpoint does not set CORS headers, so a direct
            // XHR/fetch POST would be blocked. submitPokePasteForm builds a hidden
            // form and submits it with target="_blank" to open the result in a new tab.
            await JSRuntime.InvokeVoidAsync("submitPokePasteForm", ShowdownText, title, author, notes);
            Snackbar.Add("Opening PokePaste in a new tab…", Severity.Success);
            MudDialog?.Close();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to upload to PokePaste: {ex.Message}", Severity.Error);
        }
    }

    private void Close() => MudDialog?.Close(DialogResult.Cancel());
}
