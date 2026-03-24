using System.Buffers.Binary;

namespace Pkmds.Rcl.Components.Dialogs;

public partial class SpindaPatternDialog
{
    private const string BaseUrl = "_content/Pkmds.Rcl/sprites/spinda/327-spotless.png";
    private const string BaseShinyUrl = "_content/Pkmds.Rcl/sprites/spinda/327-spotless-shiny.png";
    private const string HeadUrl = "_content/Pkmds.Rcl/sprites/spinda/327-head.png";
    private const string FaceUrl = "_content/Pkmds.Rcl/sprites/spinda/327-face.png";
    private const string MouthUrl = "_content/Pkmds.Rcl/sprites/spinda/327-mouth.png";

    private ElementReference _canvas;
    private uint _pattern;
    private bool isPreviewShiny;

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    protected override void OnParametersSet()
    {
        if (Pokemon is null)
        {
            return;
        }

        isPreviewShiny = Pokemon.IsShiny;
        _pattern = GetPatternValue(Pokemon);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RenderAsync();
        }
    }

    private async Task RenderAsync()
    {
        var baseUrl = isPreviewShiny ? BaseShinyUrl : BaseUrl;
        await JSRuntime.InvokeVoidAsync(
            "spindaRenderer.render",
            _canvas, _pattern, isPreviewShiny, baseUrl, HeadUrl, FaceUrl, MouthUrl);
    }

    private async Task Randomize()
    {
        _pattern = Util.Rand.Rand32();
        StateHasChanged();
        await RenderAsync();
    }

    private void Confirm()
    {
        if (Pokemon is null)
        {
            MudDialog?.Close(DialogResult.Cancel());
            return;
        }

        ApplyPattern(Pokemon, _pattern);
        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());

    /// <summary>
    /// Returns the 32-bit value that drives Spinda's spot layout.
    /// Gen 3–5: PID. Gen 6+: EncryptionConstant. BDSP (PB8): EC byte-swapped.
    /// </summary>
    private static uint GetPatternValue(PKM pk) => pk switch
    {
        PB8 pb8 => BinaryPrimitives.ReverseEndianness(pb8.EncryptionConstant),
        _ when pk.Format <= 5 => pk.PID,
        _ => pk.EncryptionConstant,
    };

    /// <summary>
    /// Writes the chosen pattern back to PID (Gen 3–5) or EC (Gen 6+, with BDSP byte-swap).
    /// </summary>
    private static void ApplyPattern(PKM pk, uint pattern)
    {
        if (pk is PB8 pb8)
        {
            pb8.EncryptionConstant = BinaryPrimitives.ReverseEndianness(pattern);
        }
        else if (pk.Format <= 5)
        {
            pk.PID = pattern;
        }
        else
        {
            pk.EncryptionConstant = pattern;
        }
    }
}
