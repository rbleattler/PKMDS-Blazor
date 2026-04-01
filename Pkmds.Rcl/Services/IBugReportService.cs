namespace Pkmds.Rcl.Services;

public sealed record BugReportRequest(
    string Name,
    string Email,
    string Description,
    string AppVersion,
    string UserAgent,
    byte[]? SaveFileBytes = null,
    string? SaveFileName = null,
    Exception? CapturedException = null);

public sealed record BugReportResult(bool Success, string? IssueUrl = null, string? ErrorMessage = null);

public interface IBugReportService
{
    Task<BugReportResult> SubmitBugReportAsync(BugReportRequest request, CancellationToken cancellationToken = default);
}
