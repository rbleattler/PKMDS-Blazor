namespace Pkmds.Rcl.Components.Dialogs;

public partial class LegalizationChangesDialog
{
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter]
    [EditorRequired]
    public LegalizationChanges Changes { get; set; } = LegalizationChanges.Empty;

    /// <summary>
    /// Optional human-readable label identifying the Pokémon (e.g. "Pikachu (Box 3, Slot 7)").
    /// Shown as a subtitle in the dialog header.
    /// </summary>
    [Parameter]
    public string? PokemonLabel { get; set; }

    private int CategoryCount => Changes.ByCategory().Count();

    private static string GetCategoryLabel(LegalizationChangeCategory category) => category switch
    {
        LegalizationChangeCategory.Identity => "Identity",
        LegalizationChangeCategory.Origin => "Origin",
        LegalizationChangeCategory.Battle => "Battle",
        LegalizationChangeCategory.Stats => "Stats",
        LegalizationChangeCategory.Cosmetic => "Cosmetic",
        LegalizationChangeCategory.Internal => "Internal",
        _ => category.ToString()
    };

    private void Close() => MudDialog?.Close(DialogResult.Cancel());
}
