namespace Pkmds.Rcl.Services;

public interface IBugReportService
{
    Task SubmitBugReportAsync(string description, string? email = null, string? name = null, bool attachSaveFile = false);

    /// <summary>
    /// Attaches raw file bytes to the current Sentry scope so they are included
    /// with any error events captured during save file loading failures.
    /// </summary>
    void AttachRawFileToScope(byte[] data, string fileName);
}
