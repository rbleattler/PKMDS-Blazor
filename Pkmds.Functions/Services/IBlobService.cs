namespace Pkmds.Functions.Services;

public interface IBlobService
{
    Task UploadAsync(
        int issueNumber,
        string fileName,
        Stream data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a time-limited SAS URL string for reading the specified blob, or <see langword="null"/>
    /// if the client was not created with a shared key credential (e.g. managed identity).
    /// The returned string is fully percent-encoded and safe to embed in Markdown links.
    /// </summary>
    string? GetSasUrl(int issueNumber, string fileName, TimeSpan expiry);

    Task DeleteIssueFilesAsync(
        int issueNumber,
        CancellationToken cancellationToken = default);
}
