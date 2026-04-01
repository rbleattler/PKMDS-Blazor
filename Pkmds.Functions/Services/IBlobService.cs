namespace Pkmds.Functions.Services;

public interface IBlobService
{
    /// <summary>
    /// Azure Portal URL to the blob container. When set via <c>AzureBlobPortalContainerUrl</c>
    /// configuration, issue comments include a clickable link that requires Azure Portal
    /// authentication to access (safe for public GitHub repositories).
    /// </summary>
    string? PortalContainerUrl { get; }

    Task UploadAsync(
        int issueNumber,
        string fileName,
        Stream data,
        CancellationToken cancellationToken = default);

    Task DeleteIssueFilesAsync(
        int issueNumber,
        CancellationToken cancellationToken = default);
}
