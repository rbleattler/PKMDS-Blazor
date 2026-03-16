namespace Pkmds.Rcl.Components.Dialogs;

public partial class FurfrouEditorDialog
{
    private uint daysRemaining;

    private byte selectedForm;
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

        selectedForm = Pokemon.Form;
        daysRemaining = Pokemon is IFormArgument fa
            ? (uint)fa.FormArgumentRemain
            : 0;
        isPreviewShiny = Pokemon.IsShiny;
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
        Pokemon.ChangeFormArgument(selectedForm == 0 ? 0 : daysRemaining);

        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
