namespace Pkmds.Rcl.Components.Dialogs;

public partial class PidEcDialog
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    private PIDType SelectedMethod { get; set; } = PIDType.Method_1;

    private Shiny SelectedShinyType { get; set; } = Shiny.AlwaysStar;

    private int SaveGeneration => AppState.SaveFile?.Generation ?? 0;

    private void RerollPid()
    {
        if (Pokemon is null)
        {
            return;
        }

        var pid = EntityPID.GetRandomPID(
            Random.Shared,
            Pokemon.Species,
            (byte)Pokemon.Gender,
            Pokemon.Version,
            Pokemon.Nature,
            Pokemon.Form,
            Pokemon.PID);

        Pokemon.PID = pid;
        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void MakeShinyPid()
    {
        if (Pokemon is null)
        {
            return;
        }

        CommonEdits.SetIsShiny(Pokemon, true);
        RefreshService.Refresh();
    }

    private void SetGenderLockedPid()
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.SetPIDGender((byte)Pokemon.Gender);
        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void SetNatureLockedPid()
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.SetPIDNature(Pokemon.Nature);
        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void GenerateWithMethod()
    {
        if (Pokemon is null)
        {
            return;
        }

        var seed = (uint)Random.Shared.Next();
        var pid = ClassicEraRNG.GetSequentialPID(ref seed);

        uint ivs;
        switch (SelectedMethod)
        {
            case PIDType.Method_1:
                ivs = ClassicEraRNG.GetSequentialIVs(ref seed);
                break;
            case PIDType.Method_2:
                LCRNG.Next16(ref seed); // skip one frame between PID and IVs
                ivs = ClassicEraRNG.GetSequentialIVs(ref seed);
                break;
            case PIDType.Method_4:
                var iv1 = LCRNG.Next15(ref seed);
                LCRNG.Next16(ref seed); // skip one frame between IV halves
                var iv2 = LCRNG.Next15(ref seed);
                ivs = iv1 | (iv2 << 15);
                break;
            default:
                return;
        }

        Pokemon.PID = pid;
        Pokemon.SetIVs(ivs);
        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void RandomizeEc()
    {
        if (Pokemon is null)
        {
            return;
        }

        CommonEdits.SetRandomEC(Pokemon);
        RefreshService.Refresh();
    }

    private void ApplyShinyType()
    {
        if (Pokemon is null)
        {
            return;
        }

        CommonEdits.SetShiny(Pokemon, SelectedShinyType);
        RefreshService.Refresh();
    }

    private void Close() => MudDialog?.Close();
}
