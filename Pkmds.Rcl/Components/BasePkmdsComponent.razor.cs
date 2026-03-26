namespace Pkmds.Rcl.Components;

public partial class BasePkmdsComponent
{
    private static readonly DialogOptions TrashBytesEditorDialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        CloseOnEscapeKey = true,
    };

    protected Task OpenTrashBytesEditorAsync(PKM? pokemon, StringSource field)
    {
        var parameters = new DialogParameters<TrashBytesEditorDialog>
        {
            { x => x.Pokemon, pokemon },
            { x => x.Field, field },
        };
        return DialogService.ShowAsync<TrashBytesEditorDialog>("Trash Bytes Editor", parameters, TrashBytesEditorDialogOptions);
    }
    /// <summary>
    /// Returns the nature modifier for a given stat, taking into account Gen 8+
    /// StatNature vs Nature.
    /// </summary>
    protected static NatureModifier GetNatureModifier(Nature nature, Stats stat)
    {
        var (up, dn) = nature.GetNatureModification();
        if (up == dn)
        {
            return NatureModifier.Neutral;
        }

        if (up == (int)stat)
        {
            return NatureModifier.Boosted;
        }

        return dn == (int)stat
            ? NatureModifier.Hindered
            : NatureModifier.Neutral;
    }

    protected static string GetNatureAdornmentIcon(NatureModifier modifier) => modifier switch
    {
        NatureModifier.Boosted => Icons.Material.Filled.ArrowUpward,
        NatureModifier.Hindered => Icons.Material.Filled.ArrowDownward,
        _ => string.Empty
    };

    protected static Color GetNatureAdornmentColor(NatureModifier modifier) => modifier switch
    {
        NatureModifier.Boosted => Color.Error,
        NatureModifier.Hindered => Color.Info,
        _ => Color.Default
    };

    protected static Adornment GetNatureAdornment(NatureModifier modifier) =>
        modifier is NatureModifier.Neutral
            ? Adornment.None
            : Adornment.End;

    protected static string GetNatureTooltip(NatureModifier modifier) => modifier switch
    {
        NatureModifier.Boosted => "Boosted by nature (+10%)",
        NatureModifier.Hindered => "Hindered by nature (-10%)",
        _ => string.Empty
    };

    protected static readonly IReadOnlyDictionary<string, string> FlagLabels =
        new Dictionary<string, string>
        {
            ["contact"]        = "Makes Contact",
            ["charge"]         = "Has Charge Turn",
            ["recharge"]       = "Must Recharge",
            ["protect"]        = "Blocked by Protect",
            ["reflectable"]    = "Reflectable",
            ["snatch"]         = "Snatchable",
            ["mirror"]         = "Copied by Mirror Move",
            ["punch"]          = "Punch-based",
            ["sound"]          = "Sound-based",
            ["gravity"]        = "Unusable during Gravity",
            ["defrost"]        = "Defrosts User",
            ["distance"]       = "Hits Non-adjacent Targets",
            ["heal"]           = "Heals",
            ["authentic"]      = "Bypasses Substitute",
            ["powder"]         = "Powder-based",
            ["bite"]           = "Jaw-based",
            ["pulse"]          = "Pulse-based",
            ["ballistics"]     = "Ballistics-based",
            ["non-sky-battle"] = "Unusable in Sky Battles",
            ["dance"]          = "Dance",
            ["wind"]           = "Wind-based",
            ["slicing"]        = "Slicing-based",
        };
}
