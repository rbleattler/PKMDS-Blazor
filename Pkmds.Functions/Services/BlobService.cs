using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Pkmds.Functions.Services;

public class BlobService(IConfiguration configuration, ILogger<BlobService> logger) : IBlobService
{
    private readonly BlobContainerClient _containerClient = CreateContainerClient(configuration);

    public string? PortalContainerUrl { get; } = configuration["AzureBlobPortalContainerUrl"];

    public async Task UploadAsync(
        int issueNumber,
        string fileName,
        Stream data,
        CancellationToken cancellationToken = default)
    {
        var blobName = $"{issueNumber}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(data, overwrite: true, cancellationToken: cancellationToken);
        logger.LogInformation("Uploaded blob {BlobName} for issue #{IssueNumber}", blobName, issueNumber);
    }

    public async Task DeleteIssueFilesAsync(
        int issueNumber,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{issueNumber}/";
        var deleted = 0;

        await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var blobClient = _containerClient.GetBlobClient(blob.Name);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            deleted++;
        }

        logger.LogInformation("Deleted {Count} blob(s) for issue #{IssueNumber}", deleted, issueNumber);
    }

    private static BlobContainerClient CreateContainerClient(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorageConnectionString"]
            ?? throw new InvalidOperationException("AzureStorageConnectionString configuration is required.");
        var containerName = configuration["BlobContainerName"] ?? "bug-reports";
        var containerClient = new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);
        containerClient.CreateIfNotExists();
        return containerClient;
    }
}
