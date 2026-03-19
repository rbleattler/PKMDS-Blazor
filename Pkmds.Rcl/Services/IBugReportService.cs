namespace Pkmds.Rcl.Services;

public interface IBugReportService
{
    Task SubmitBugReportAsync(string description, string? email = null, string? name = null, bool attachSaveFile = false);
}
