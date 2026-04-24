namespace Pkmds.Rcl.Components.Dialogs;

public partial class MiniorColorDialog
{
    // Forms 0–6 = Meteor (Red/Orange/Yellow/Green/Blue/Indigo/Violet)
    // Forms 7–13 = Core (same color order)
    internal const byte MiniorMeteorCount = 7;
    internal const byte MiniorCoreCount = 7;
    private readonly HashSet<int> failedFormSprites = [];
    private bool isPreviewShiny;

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
        Haptics.Confirm();
        MudDialog?.Close(DialogResult.Ok(true));
    }

    private void OnFormSpriteError(int formIdx)
    {
        if (failedFormSprites.Add(formIdx))
        {
            StateHasChanged();
        }
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
