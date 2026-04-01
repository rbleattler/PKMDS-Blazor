namespace Pkmds.Functions.Services;

public interface IBlobService
{
    Task UploadAsync(
        int issueNumber,
        string fileName,
        Stream data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a time-limited SAS URL for reading the specified blob, or <see langword="null"/>
    /// if the client was not created with a shared key credential (e.g. managed identity).
    /// </summary>
    Uri? GetSasUrl(int issueNumber, string fileName, TimeSpan expiry);

    Task DeleteIssueFilesAsync(
        int issueNumber,
        CancellationToken cancellationToken = default);
}
