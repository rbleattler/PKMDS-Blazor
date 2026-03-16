namespace Pkmds.Rcl.Components.Dialogs;

public partial class MiniorColorDialog
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    // Forms 0–6 = Meteor (Red/Orange/Yellow/Green/Blue/Indigo/Violet)
    // Forms 7–13 = Core (same color order)
    internal const byte MiniorMeteorCount = 7;
    internal const byte MiniorCoreCount = 7;

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
