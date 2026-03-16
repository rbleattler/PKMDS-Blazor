namespace Pkmds.Rcl.Components.Dialogs;

public partial class PumpkabooSizeDialog
{
    private byte selectedForm;
    private bool isPreviewShiny;
    private readonly HashSet<int> _failedFormSprites = [];

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

        selectedForm = Pokemon.Form;
        isPreviewShiny = Pokemon.IsShiny;
    }

    private void Confirm()
    {
        if (Pokemon is null)
        {
            MudDialog?.Close(DialogResult.Cancel());
            return;
        }

        Pokemon.Form = selectedForm;
        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void OnFormSpriteError(int formIdx)
    {
        if (_failedFormSprites.Add(formIdx))
        {
            StateHasChanged();
        }
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
