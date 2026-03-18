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

    /// <summary>
    /// Handles a form-displayed checkbox change, enforcing radio-group behaviour:
    /// at most one form×shiny combination may be displayed at a time.
    /// </summary>
    private void OnFormDisplayedChanged(Zukan6 dex, int formStart, int formCount, int formIndex, int region, bool value)
    {
        if (value)
        {
            // Clear every region-2 and region-3 flag for all forms of this species first.
            for (var f = 0; f < formCount; f++)
            {
                dex.SetFormFlag(formStart + f, 2, false);
                dex.SetFormFlag(formStart + f, 3, false);
            }

            dex.SetFormFlag(formIndex, region, true);
        }
        else
        {
            dex.SetFormFlag(formIndex, region, false);
        }

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
