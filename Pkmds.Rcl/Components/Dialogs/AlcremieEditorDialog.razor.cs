namespace Pkmds.Rcl.Components.Dialogs;

public partial class AlcremieEditorDialog
{
    // Cream form names in PKHeX form-index order (0–8)
    internal static readonly string[] CreamNames =
    [
        "Vanilla Cream", "Ruby Cream", "Matcha Cream", "Mint Cream",
        "Lemon Cream", "Salted Cream", "Ruby Swirl", "Caramel Swirl", "Rainbow Swirl"
    ];

    // Decoration names in AlcremieDecoration enum order (0–6)
    internal static readonly string[] DecorationNames =
    [
        "Strawberry Sweet", "Berry Sweet", "Love Sweet", "Star Sweet",
        "Clover Sweet", "Flower Sweet", "Ribbon Sweet"
    ];

    private byte selectedCream;
    private uint selectedDeco;
    private bool isPreviewShiny;
    private readonly HashSet<int> _failedCreamSprites = [];
    private readonly HashSet<int> _failedDecoSprites = [];
    private bool _previewFailed;

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

        selectedCream = Pokemon.Form < CreamNames.Length
            ? Pokemon.Form
            : (byte)0;
        var arg = Pokemon.GetFormArgument(0) ?? 0;
        selectedDeco = arg < (uint)DecorationNames.Length
            ? arg
            : 0;
        isPreviewShiny = Pokemon.IsShiny;
    }

    private void SelectCream(byte cream) => selectedCream = cream;

    private void SelectDeco(uint deco) => selectedDeco = deco;

    private void Confirm()
    {
        if (Pokemon is null)
        {
            MudDialog?.Close(DialogResult.Cancel());
            return;
        }

        Pokemon.Form = selectedCream;
        if (Pokemon is IFormArgument fa)
        {
            fa.FormArgument = selectedDeco;
        }

        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void OnCreamSpriteError(int creamIdx)
    {
        if (_failedCreamSprites.Add(creamIdx))
        {
            StateHasChanged();
        }
    }

    private void OnDecoSpriteError(int decoIdx)
    {
        if (_failedDecoSprites.Add(decoIdx))
        {
            StateHasChanged();
        }
    }

    private void OnPreviewSpriteError()
    {
        if (!_previewFailed)
        {
            _previewFailed = true;
            StateHasChanged();
        }
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
