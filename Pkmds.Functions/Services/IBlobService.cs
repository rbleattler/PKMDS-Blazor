namespace Pkmds.Functions.Services;

public interface IBlobService
{
    Task UploadAsync(
        int issueNumber,
        string fileName,
        Stream data,
        CancellationToken cancellationToken = default);

    Task DeleteIssueFilesAsync(
        int issueNumber,
        CancellationToken cancellationToken = default);
}
