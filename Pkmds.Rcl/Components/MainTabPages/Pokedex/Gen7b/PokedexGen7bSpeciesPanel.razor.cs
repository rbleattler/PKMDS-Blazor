namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen7b;

// ReSharper disable once InconsistentNaming
public partial class PokedexGen7bSpeciesPanel
{
    private static readonly (string Label, int Region)[] SeenRegions =
    [
        ("Male", 0), ("Female", 1), ("Male (Shiny)", 2), ("Female (Shiny)", 3)
    ];

    private static readonly string[] LanguageLabels =
        ["JPN", "ENG", "FRE", "ITA", "GER", "SPA", "KOR", "CHS", "CHT"];

    private static readonly (string Label, DexSizeType Group)[] SizeGroups =
    [
        ("Min Height", DexSizeType.MinHeight), ("Max Height", DexSizeType.MaxHeight),
        ("Min Weight", DexSizeType.MinWeight), ("Max Weight", DexSizeType.MaxWeight)
    ];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    private void OnDisplayedChanged(Zukan7b dex, int bit, int region, bool value)
    {
        if (value)
        {
            for (var i = 0; i < 4; i++)
            {
                dex.SetDisplayed(bit, i, false);
            }

            dex.SetDisplayed(bit, region, true);
        }
        else
        {
            dex.SetDisplayed(bit, region, false);
        }

        StateHasChanged();
    }

    /// <summary>
    /// Toggles a size record's "used" state. Unchecking resets it to PKHeX defaults
    /// (0xFE height, 0x7F weight); checking seeds it with a sensible initial scalar
    /// (0 for min records, 255 for max records) matching PKHeX's GiveAll behaviour.
    /// </summary>
    private void OnSizeUsedChanged(Zukan7b dex, DexSizeType group, bool used, int sizeIndex)
    {
        if (used)
        {
            var seed = group is DexSizeType.MinHeight or DexSizeType.MinWeight
                ? (byte)0
                : (byte)255;
            dex.SetSizeData(group, sizeIndex, seed, seed);
        }
        else
        {
            dex.SetSizeData(group, sizeIndex, Zukan7b.DefaultEntryValueH, Zukan7b.DefaultEntryValueW);
        }

        StateHasChanged();
    }

    /// <summary>Writes an updated height, weight, or flag to an existing size record.</summary>
    private void OnSizeValueChanged(Zukan7b dex, DexSizeType group, int sizeIndex, byte h, byte w, bool flag)
    {
        dex.SetSizeData(group, sizeIndex, h, w, flag);
        StateHasChanged();
    }

    /// <summary>
    /// Returns true when the gender region is valid for this species.
    /// Region 0/2 = Male / Male Shiny  — invalid for female-only species (Gender == 254).
    /// Region 1/3 = Female / Female Shiny — invalid for male-only (0) or genderless (255) species.
    /// </summary>
    private bool IsRegionValid(int region)
    {
        var gender = AppState.SaveFile?.Personal[SpeciesId].Gender ?? 255;
        return region switch
        {
            0 or 2 => gender != 254,
            1 or 3 => gender is not 0 and not 255,
            _ => true
        };
    }
}
