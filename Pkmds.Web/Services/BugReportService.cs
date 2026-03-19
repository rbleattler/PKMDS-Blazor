namespace Pkmds.Web.Services;

file sealed class NoOpDisposable : IDisposable
{
    public static readonly NoOpDisposable Instance = new();
    public void Dispose() { }
}

public class BugReportService(IAppState appState) : IBugReportService
{
    private const int MaxAttachmentBytes = 10 * 1024 * 1024; // 10 MiB, matches Program.cs

    public IDisposable AttachRawFileToScope(byte[] data, string fileName)
    {
        if (data.Length == 0 || data.Length > MaxAttachmentBytes)
        {
            return NoOpDisposable.Instance;
        }

        // Sanitize filename: keep only the last segment and replace problematic characters.
        var sanitized = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "unknown-save-file";
        }

        var scope = SentrySdk.PushScope();
        SentrySdk.ConfigureScope(s => s.AddAttachment(data, sanitized));
        return scope;
    }

    public async Task SubmitBugReportAsync(string description, string? email = null, string? name = null, bool attachSaveFile = false)
    {
        using var _ = SentrySdk.PushScope();

        SentrySdk.ConfigureScope(scope =>
        {
            scope.SetTag("report_type", "user_bug_report");
            scope.SetTag("app_version", appState.AppVersion ?? "unknown");
            scope.SetTag("pkhex_version", IAppState.PkhexVersion ?? "unknown");
            scope.SetTag("hax_enabled", appState.IsHaXEnabled.ToString());
            scope.SetTag("app_language", appState.CurrentLanguage);

            if (appState.SaveFile is { } saveFile)
            {
                scope.SetTag("save_type", saveFile.GetType().Name);
                scope.SetTag("game_version", saveFile.Version.ToString());
            }

            if (!attachSaveFile || appState.SaveFile is not { } saveFileToAttach)
            {
                return;
            }

            var saveData = saveFileToAttach.Write().ToArray();
            var fileName = $"savefile.{saveFileToAttach.Extension}";
            scope.AddAttachment(saveData, fileName);
        });

        var sentryEvent = new SentryEvent { Message = description };
        var eventId = SentrySdk.CaptureEvent(sentryEvent);
        SentrySdk.CaptureFeedback(description, email, name, associatedEventId: eventId);

        // Flush events to ensure they're sent before returning
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));
    }
}
