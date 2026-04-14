namespace Pkmds.Rcl.Components.MainTabPages;

public partial class TeamsTab : RefreshAwareComponent
{
    // Battle teams reference box slots, so refresh when box state changes too.
    protected override RefreshEvents SubscribeTo => RefreshEvents.AppState | RefreshEvents.BoxState;

    private async Task CopyTeamToClipboard(IReadOnlyList<PKM> team)
    {
        var text = AppService.ExportTeamAsShowdown(team);
        if (string.IsNullOrEmpty(text))
        {
            Snackbar.Add("No Pokémon to export.", Severity.Warning);
            return;
        }

        try
        {
            await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text);
            Snackbar.Add("Team copied to clipboard in Showdown format.", Severity.Success);
        }
        catch (JSException)
        {
            Snackbar.Add("Failed to copy to clipboard.", Severity.Error);
        }
    }
}
