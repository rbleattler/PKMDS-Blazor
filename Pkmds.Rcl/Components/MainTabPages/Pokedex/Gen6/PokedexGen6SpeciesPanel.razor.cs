namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen6;

public partial class PokedexGen6SpeciesPanel
{
    private static readonly (string Label, int Region)[] SeenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male (Shiny)", 2), ("Female (Shiny)", 3)
    ];

    private static readonly (string Label, LanguageID LangId)[] LanguageLabels =
    [
        ("JPN", LanguageID.Japanese), ("ENG", LanguageID.English), ("FRE", LanguageID.French),
        ("ITA", LanguageID.Italian), ("GER", LanguageID.German), ("SPA", LanguageID.Spanish),
        ("KOR", LanguageID.Korean)
    ];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan6 dex, int region, bool value)
    {
        if (value)
        {
            dex.ClearDisplayed(SpeciesId);
            dex.SetDisplayed(SpeciesId, region);
        }
        else
        {
            dex.SetDisplayed(SpeciesId, region, false);
        }

        StateHasChanged();
    }

    private static void OnSpindaChanged(Zukan6 dex, string hex)
    {
        if (uint.TryParse(hex, NumberStyles.HexNumber, null, out var pid))
        {
            dex.Spinda = pid;
        }
    }
}
