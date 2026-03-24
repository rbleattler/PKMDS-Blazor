namespace Pkmds.Rcl.Services;

public interface IBugReportService
{
    Task SubmitBugReportAsync(string description, string email, string name, bool attachSaveFile = false);

    /// <summary>
    /// Pushes a new Sentry scope with the raw file bytes attached. The attachment is
    /// automatically removed when the returned <see cref="IDisposable"/> is disposed,
    /// preventing it from leaking into unrelated events.
    /// </summary>
    /// <returns>A disposable scope that should wrap the error logging call.</returns>
    IDisposable AttachRawFileToScope(byte[] data, string fileName);
}
