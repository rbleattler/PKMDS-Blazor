namespace Pkmds.Rcl.Components.Dialogs;

public partial class PumpkabooSizeDialog
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    private byte selectedForm;

    protected override void OnParametersSet()
    {
        if (Pokemon is not null)
        {
            selectedForm = Pokemon.Form;
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
