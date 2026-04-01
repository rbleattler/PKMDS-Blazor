using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Pkmds.Functions.Services;

public class BlobService : IBlobService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobService> _logger;

    public BlobService(IConfiguration configuration, ILogger<BlobService> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureStorageConnectionString"]
            ?? throw new InvalidOperationException("AzureStorageConnectionString configuration is required.");
        var containerName = configuration["BlobContainerName"] ?? "bug-reports";

        var serviceClient = new BlobServiceClient(connectionString);
        _containerClient = serviceClient.GetBlobContainerClient(containerName);
        _containerClient.CreateIfNotExists();
    }

    public async Task UploadAsync(
        int issueNumber,
        string fileName,
        Stream data,
        CancellationToken cancellationToken = default)
    {
        var blobName = $"{issueNumber}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(data, overwrite: true, cancellationToken: cancellationToken);
        _logger.LogInformation("Uploaded blob {BlobName} for issue #{IssueNumber}", blobName, issueNumber);
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

        _logger.LogInformation("Deleted {Count} blob(s) for issue #{IssueNumber}", deleted, issueNumber);
    }
}
