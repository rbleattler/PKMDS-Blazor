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

    private bool KeepNature { get; set; } = true;

    private bool KeepGender { get; set; } = true;

    private bool AvoidShiny { get; set; } = true;

    private int SaveGeneration => AppState.SaveFile?.Generation ?? 0;

    private bool IsGen345 => SaveGeneration is 3 or 4 or 5;

    private void GeneratePid()
    {
        if (Pokemon is null)
        {
            return;
        }

        var desiredNature = (byte)Pokemon.Nature;
        var genderRatio = Pokemon.PersonalInfo.Gender;
        var desiredGender = Pokemon.Gender;
        var isDualGender = Pokemon.PersonalInfo.IsDualGender;

        // For Gen 3–5, the ability slot is encoded in the PID (bit 0 for Gen 3/4, bit 16
        // for Gen 5). Preserve it so randomizing the PID does not silently change the
        // Pokémon's ability. See EntityPID.GetRandomPID in PKHeX.Core for reference.
        var abilityBitMask = SaveGeneration switch
        {
            3 or 4 => 0x0000_0001u,
            5 => 0x0001_0000u,
            _ => 0u
        };
        var desiredAbilityBit = Pokemon.PID & abilityBitMask;

        uint pid;
        do
        {
            pid = NextRandomUInt32();
            Pokemon.PID = pid; // needed to evaluate IsShiny
        } while (
            abilityBitMask != 0 && (pid & abilityBitMask) != desiredAbilityBit ||
            IsGen345 && KeepNature && pid % 25 != desiredNature ||
            IsGen345 && KeepGender && isDualGender && EntityGender.GetFromPIDAndRatio(pid, genderRatio) != desiredGender ||
            AvoidShiny && Pokemon.IsShiny
        );

        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void MakeShinyPid()
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.SetIsShiny(true);
        RefreshService.Refresh();
    }

    private void GenerateWithMethod()
    {
        if (Pokemon is null)
        {
            return;
        }

        var desiredNature = (byte)Pokemon.Nature;
        var genderRatio = Pokemon.PersonalInfo.Gender;
        var desiredGender = Pokemon.Gender;
        var isDualGender = Pokemon.PersonalInfo.IsDualGender;

        // Loop until we find a seed whose PID satisfies all checked constraints.
        // For Gen 3–5, both nature and gender are encoded in the PID, so a random
        // PID will only match by chance — this avoids the user having to retry manually.
        uint seed, pid;
        do
        {
            seed = NextRandomUInt32();
            pid = ClassicEraRNG.GetSequentialPID(ref seed);
            Pokemon.PID = pid; // needed to evaluate IsShiny
        } while (
            KeepNature && pid % 25 != desiredNature ||
            KeepGender && isDualGender && EntityGender.GetFromPIDAndRatio(pid, genderRatio) != desiredGender ||
            AvoidShiny && Pokemon.IsShiny
        );

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
                ivs = iv1 | iv2 << 15;
                break;
            default:
                return;
        }

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

        // Note: EC % 6 determines the Pokémon's characteristic (e.g. "Often dozes off").
        // If preserving the characteristic ever becomes a requirement, loop until the new
        // EC satisfies newEc % 6 == oldEc % 6 before calling SetRandomEC.
        Pokemon.SetRandomEC();
        AppService.LoadPokemonStats(Pokemon);
        RefreshService.Refresh();
    }

    private void ApplyShinyType()
    {
        if (Pokemon is null)
        {
            return;
        }

        Pokemon.SetShiny(SelectedShinyType);
        RefreshService.Refresh();
    }

    private static uint NextRandomUInt32() =>
        (uint)Random.Shared.NextInt64(uint.MaxValue + 1L);

    private void Close() => MudDialog?.Close();
}
