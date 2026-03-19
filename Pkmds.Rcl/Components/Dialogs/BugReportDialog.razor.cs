namespace Pkmds.Rcl.Components.Dialogs;

public record BugReportData(string Description, string? Email, string? Name, bool AttachSaveFile);

public partial class BugReportDialog
{
    private string description = string.Empty;
    private string? email;
    private string? name;
    private bool attachSaveFile;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public bool HasSaveFile { get; set; }

    protected override void OnInitialized() =>
        attachSaveFile = HasSaveFile;

    private void Cancel() => MudDialog.Close(DialogResult.Cancel());

    private void Submit() =>
        MudDialog.Close(DialogResult.Ok(new BugReportData(description, email, name, attachSaveFile)));
}
