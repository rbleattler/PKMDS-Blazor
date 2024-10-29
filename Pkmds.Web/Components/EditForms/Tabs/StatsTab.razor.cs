namespace Pkmds.Web.Components.EditForms.Tabs;

public partial class StatsTab : IDisposable
{
    [Parameter, EditorRequired]
    public PKM? Pokemon { get; set; }

    protected override void OnInitialized() =>
        RefreshService.OnAppStateChanged += StateHasChanged;

    public void Dispose() =>
        RefreshService.OnAppStateChanged -= StateHasChanged;

    public static string GetCharacteristic(PKM? pokemon) =>
        pokemon?.Characteristic is int characteristicIndex &&
        characteristicIndex > -1 &&
        GameInfo.Strings.characteristics is { Length: > 0 } characteristics &&
        characteristicIndex < characteristics.Length
            ? characteristics[characteristicIndex]
            : string.Empty;

    private void OnNatureSet(Nature nature)
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.SetNature(nature);
        AppService.LoadPokemonStats(Pokemon);
    }

    private static int GetEvMax(int generation) => generation switch
    {
        1 or 2 => EffortValues.Max12,
        3 or 4 or 5 => EffortValues.Max255,
        _ => EffortValues.Max252
    };

    private string GetStatClass(Stats stat)
    {
        if (Pokemon is null)
        {
            return string.Empty;
        }

        var pkm = Pokemon.Nickname;

        var (up, dn) = NatureAmp.GetNatureModification(Pokemon.Nature);

        return up == (int)stat
            ? "plus-nature"
            : dn == (int)stat
                ? "minus-nature"
                : string.Empty;
    }

    private enum Stats
    {
        Attack,
        Defense,
        Speed,
        SpecialAttack,
        SpecialDefense
    }
}
