namespace Pkmds.Web.Services;

// Bug reporting is currently disabled — Sentry trial ended.
// To re-enable: restore Sentry SDK, update this implementation, add @inject IBugReportService
// back to Pkmds.Rcl/_Imports.razor, and re-add the "Report a Bug" button in MainLayout.razor.
public class BugReportService : IBugReportService
{
    public IDisposable AttachRawFileToScope(byte[] data, string fileName) => NullDisposable.Instance;

    public Task SubmitBugReportAsync(string description, string email, string name, bool attachSaveFile = false) =>
        Task.CompletedTask;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
