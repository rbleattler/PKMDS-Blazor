using System.ComponentModel.DataAnnotations;

namespace Pkmds.Rcl.Components.Dialogs;

public record BugReportData(string Description, string Email, string Name, bool AttachSaveFile);

public partial class BugReportDialog
{
    private const int MinDescriptionLength = 30;
    private static readonly EmailAddressAttribute EmailValidator = new();
    private bool attachSaveFile;

    private string description = string.Empty;
    private string email = string.Empty;
    private string name = string.Empty;

    private bool IsEmailValid => !string.IsNullOrWhiteSpace(email) && EmailValidator.IsValid(email);

    private string? submitError;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public bool HasSaveFile { get; set; }

    protected override void OnInitialized() =>
        attachSaveFile = HasSaveFile;

    private void Cancel() => MudDialog.Close(DialogResult.Cancel());

    private void Submit()
    {
        if (description.Length < MinDescriptionLength || !IsEmailValid || string.IsNullOrWhiteSpace(name))
        {
            submitError = "Please provide a valid email address and name before submitting.";
            return;
        }

        MudDialog.Close(DialogResult.Ok(new BugReportData(description, email, name, attachSaveFile)));
    }
}
