namespace Pkmds.Rcl.Components.MainTabPages.Pokedex.Gen9Sv;

public partial class PokedexGen9SvSpeciesPanel : BasePkmdsComponent
{
    private static readonly (string Label, int LangId)[] LanguageLabels =
    [
        ("JPN", (int)LanguageID.Japanese), ("ENG", (int)LanguageID.English), ("FRE", (int)LanguageID.French),
        ("ITA", (int)LanguageID.Italian), ("GER", (int)LanguageID.German), ("SPA", (int)LanguageID.Spanish),
        ("KOR", (int)LanguageID.Korean), ("CHS", (int)LanguageID.ChineseS), ("CHT", (int)LanguageID.ChineseT)
    ];

    [Parameter]
    [EditorRequired]
    public ushort SpeciesId { get; set; }

    /// <summary>
    ///     Returns which regional dexes contain this species (checking all forms).
    /// </summary>
    private static (bool Paldea, bool Kitakami, bool Blueberry) GetDexMembership(SAV9SV sav, ushort species)
    {
        bool paldea = false, kitakami = false, blueberry = false;
        var fc = sav.Personal.GetFormEntry(species, 0).FormCount;
        for (byte f = 0; f < fc; f++)
        {
            var pi = sav.Personal.GetFormEntry(species, f);
            if (pi.DexPaldea != 0)
            {
                paldea = true;
            }

            if (pi.DexKitakami != 0)
            {
                kitakami = true;
            }

            if (pi.DexBlueberry != 0)
            {
                blueberry = true;
            }
        }

        return (paldea, kitakami, blueberry);
    }

    /// <summary>
    ///     Returns true when the given gender slot (0=male, 1=female, 2=genderless) is
    ///     possible for this species, based on the gender ratio in the personal table.
    ///     Mirrors the guarding logic used in the Gen7b and Gen8SWSH panels.
    /// </summary>
    private static bool IsGenderAvailable(SAV9SV sav, ushort species, byte gender)
    {
        var ratio = sav.Personal.GetFormEntry(species, 0).Gender;
        return gender switch
        {
            0 => ratio is not PersonalInfo.RatioMagicFemale and not PersonalInfo.RatioMagicGenderless,
            1 => ratio is not PersonalInfo.RatioMagicMale and not PersonalInfo.RatioMagicGenderless,
            2 => ratio == PersonalInfo.RatioMagicGenderless,
            _ => true
        };
    }

    /// <summary>Sets the Paldea display fields on the Kitakami unified entry.</summary>
    private void SetKitakamiDisplayPaldea(Zukan9Kitakami dex, ushort species, byte form, byte gender, byte shiny)
    {
        dex.Get(species).SetLocalPaldea(form, gender, shiny);
        StateHasChanged();
    }

    /// <summary>Sets the Kitakami display fields on the Kitakami unified entry.</summary>
    private void SetKitakamiDisplayKitakami(Zukan9Kitakami dex, ushort species, byte form, byte gender, byte shiny)
    {
        dex.Get(species).SetLocalKitakami(form, gender, shiny);
        StateHasChanged();
    }

    /// <summary>Sets the Blueberry display fields on the Kitakami unified entry.</summary>
    private void SetKitakamiDisplayBlueberry(Zukan9Kitakami dex, ushort species, byte form, byte gender, byte shiny)
    {
        dex.Get(species).SetLocalBlueberry(form, gender, shiny);
        StateHasChanged();
    }
}
