using System.ComponentModel.DataAnnotations;

namespace Pkmds.Rcl.Components.Dialogs;

public partial class BugReportDialog
{
    private const int MinDescriptionLength = 30;
    private static readonly EmailAddressAttribute EmailValidator = new();
    private bool attachSaveFile;

    private string description = string.Empty;
    private string email = string.Empty;
    private bool isSubmitting;
    private string name = string.Empty;

    private string? submitError;

    private bool IsEmailValid => !string.IsNullOrWhiteSpace(email) && EmailValidator.IsValid(email);

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public bool HasSaveFile { get; set; }

    [Parameter]
    public string AppVersion { get; set; } = string.Empty;

    [Parameter]
    public Exception? CapturedException { get; set; }

    [Inject]
    private IBugReportService BugReportService { get; set; } = null!;

    protected override void OnInitialized()
    {
        attachSaveFile = HasSaveFile;

        if (CapturedException is not null)
        {
            description =
                $"[Crash] {CapturedException.GetType().Name}: {CapturedException.Message}" +
                $"\n\nStack trace:\n{CapturedException.StackTrace}" +
                "\n\n--- Additional details ---\nPlease describe what you were doing when this crash occurred:\n\n";
        }
    }

    private void Cancel() => MudDialog.Close(DialogResult.Cancel());

    private async Task Submit()
    {
        if (CapturedException is null && description.Length < MinDescriptionLength)
        {
            submitError = $"Please provide a more detailed description (at least {MinDescriptionLength} characters).";
            return;
        }

        if (!IsEmailValid || string.IsNullOrWhiteSpace(name))
        {
            submitError = "Please provide a valid email address and name before submitting.";
            return;
        }

        isSubmitting = true;
        submitError = null;
        StateHasChanged();

        try
        {
            byte[]? saveBytes = null;
            string? saveFileName = null;
            string? saveGameName = null;
            string? saveRevision = null;
            if (AppState.SaveFile is { } sf)
            {
                saveGameName = SaveFileNameDisplay.FriendlyGameName(sf.Version);
                saveRevision = (sf as ISaveFileRevision)?.SaveRevisionString;
                if (attachSaveFile)
                {
                    var rawBytes = sf.Write().ToArray();
                    // If the current save was loaded from a Manic EMU .3ds.sav ZIP, rebuild the
                    // archive so the bug report preserves the wrapper. Without this the submitted
                    // bytes are the bare inner save and we can never diagnose ZIP round-trip
                    // issues from user reports (see issue #750). The attachment name must carry
                    // the compound extension so triagers can see at a glance the payload is a ZIP
                    // and not a bare .sav — a generic save.bin fallback would mask that.
                    //
                    // RebuildZip can throw (InvalidDataException on oversized non-save entries,
                    // corrupt archives, etc.). Getting the report through matters more than the
                    // wrapper, so on failure we fall back to the bare save — the submission
                    // itself must not be blocked by an attach-side issue.
                    if (AppState.ManicEmuSaveContext is { } ctx)
                    {
                        try
                        {
                            saveBytes = ManicEmuSaveHelper.RebuildZip(ctx, rawBytes);
                            saveFileName = ManicEmuSaveHelper.GetExportFileName(AppState.SaveFileName).ExportName;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "Failed to rebuild Manic EMU ZIP for bug report attachment; falling back to bare save");
                            saveBytes = rawBytes;
                            saveFileName = AppState.SaveFileName ?? "save.bin";
                        }
                    }
                    else
                    {
                        saveBytes = rawBytes;
                        saveFileName = AppState.SaveFileName ?? "save.bin";
                    }
                }
            }

            var userAgent = await JSRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
            var request = new BugReportRequest(name, email, description, AppVersion, userAgent,
                saveBytes, saveFileName, saveGameName, saveRevision, CapturedException);
            var result = await BugReportService.SubmitBugReportAsync(request);

            if (result.Success)
            {
                MudDialog.Close(DialogResult.Ok(result.IssueUrl));
            }
            else
            {
                submitError = result.ErrorMessage ?? "Submission failed. Please try again.";
            }
        }
        finally
        {
            isSubmitting = false;
            StateHasChanged();
        }
    }
}
