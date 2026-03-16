namespace Pkmds.Rcl.Components.Dialogs;

public partial class VivillonEditorDialog
{
    private byte selectedForm;
    private bool isPreviewShiny;

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    protected override void OnParametersSet()
    {
        if (Pokemon is not null)
        {
            selectedForm = Pokemon.Form;
            isPreviewShiny = Pokemon.IsShiny;
        }
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

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
