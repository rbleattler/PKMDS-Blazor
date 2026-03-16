namespace Pkmds.Rcl.Components.Dialogs;

public partial class FurfrouEditorDialog
{
    private uint daysRemaining;

    private byte selectedForm;

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
        daysRemaining = Pokemon is IFormArgument fa
            ? fa.FormArgument
            : 0;
    }

    private void SelectForm(byte form)
    {
        selectedForm = form;
        // Clear days when reverting to Natural form
        if (form == 0)
        {
            daysRemaining = 0;
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
        if (Pokemon is IFormArgument fa)
        {
            fa.FormArgument = selectedForm == 0
                ? 0
                : daysRemaining;
        }

        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
