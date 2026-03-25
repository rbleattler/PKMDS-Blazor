using System.ComponentModel.DataAnnotations;

namespace Pkmds.Rcl.Components.Dialogs;

public record BugReportData(string Description, string Email, string Name, bool AttachSaveFile);

public partial class BugReportDialog
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    private string description = string.Empty;
    private string email = string.Empty;
    private string name = string.Empty;
    private bool attachSaveFile;

    private bool IsEmailValid => EmailValidator.IsValid(email);

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
