namespace Pkmds.Functions.Services;

public class BlobService(IConfiguration configuration, ILogger<BlobService> logger) : IBlobService
{
    private readonly BlobContainerClient containerClient = CreateContainerClient(configuration);

    public async Task UploadAsync(
        int issueNumber,
        string fileName,
        Stream data,
        CancellationToken cancellationToken = default)
    {
        var blobName = $"{issueNumber}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(data, overwrite: true, cancellationToken: cancellationToken);
        logger.LogInformation("Uploaded blob {BlobName} for issue #{IssueNumber}", blobName, issueNumber);
    }

    public string? GetSasUrl(int issueNumber, string fileName, TimeSpan expiry)
    {
        var blobName = $"{issueNumber}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);
        if (!blobClient.CanGenerateSasUri)
        {
            return null;
        }

        var sasBuilder = new BlobSasBuilder { BlobContainerName = containerClient.Name, BlobName = blobName, Resource = "b", ExpiresOn = DateTimeOffset.UtcNow.Add(expiry) };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        // Use AbsoluteUri (percent-encoded) — ToString() leaves spaces unencoded, breaking Markdown links
        return blobClient.GenerateSasUri(sasBuilder).AbsoluteUri;
    }

    public async Task DeleteIssueFilesAsync(
        int issueNumber,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"{issueNumber}/";
        var deleted = 0;

        await foreach (var blob in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            var blobClient = containerClient.GetBlobClient(blob.Name);
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
