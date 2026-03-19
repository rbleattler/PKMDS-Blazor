namespace Pkmds.Web.Services;

public class BugReportService(IAppState appState) : IBugReportService
{
    public async Task SubmitBugReportAsync(string description, string? email = null, string? name = null, bool attachSaveFile = false)
    {
        SentrySdk.ConfigureScope(scope =>
        {
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
        sentryEvent.SetFingerprint(["bug-report", description]);
        var eventId = SentrySdk.CaptureEvent(sentryEvent);
        SentrySdk.CaptureFeedback(description, email, name, associatedEventId: eventId);

        // Flush events to ensure they're sent before returning
        await SentrySdk.FlushAsync(TimeSpan.FromSeconds(5));

        SentrySdk.ConfigureScope(scope => scope.ClearAttachments());
    }
}
