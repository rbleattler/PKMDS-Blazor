using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Pkmds.Functions.Services;

namespace Pkmds.Functions.Functions;

public class SubmitBugReport(IGitHubService gitHubService, IBlobService blobService, ILogger<SubmitBugReport> logger)
{
    private const long MaxSaveFileSizeBytes = 8 * 1024 * 1024; // 8 MB

    [Function("SubmitBugReport")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "SubmitBugReport")]
        HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!req.HasFormContentType)
        {
            return new BadRequestObjectResult(new { error = "Expected multipart/form-data." });
        }

        IFormCollection form;
        try
        {
            form = await req.ReadFormAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read form data");
            return new BadRequestObjectResult(new { error = "Failed to read form data." });
        }

        var name = form["name"].ToString().Trim();
        var email = form["email"].ToString().Trim();
        var description = form["description"].ToString().Trim();
        var appVersion = form["appVersion"].ToString().Trim();
        var userAgent = form["userAgent"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(description))
        {
            return new BadRequestObjectResult(new { error = "name, email, and description are required." });
        }

        var shortTitle = description.Length > 72
            ? $"{description[..72]}…"
            : description;
        var issueTitle = $"[Bug] {shortTitle}";

        var issueBody =
            $"**Reporter:** {name} ({email})\n" +
            $"**App version:** {appVersion}\n" +
            $"**User agent:** {userAgent}\n\n" +
            $"## Description\n\n{description}";

        int issueNumber;
        string issueUrl;
        try
        {
            (issueNumber, issueUrl) = await gitHubService.CreateIssueAsync(issueTitle, issueBody, cancellationToken);
            logger.LogInformation("Created GitHub issue #{IssueNumber} for bug report from {Email}", issueNumber, email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create GitHub issue");
            return new ObjectResult(new { error = "Failed to create GitHub issue. Please try again later." })
            {
                StatusCode = StatusCodes.Status502BadGateway,
            };
        }

        var saveFile = form.Files["saveFile"];
        if (saveFile is { Length: > 0 })
        {
            if (saveFile.Length > MaxSaveFileSizeBytes)
            {
                logger.LogWarning("Save file for issue #{IssueNumber} exceeds 8 MB limit ({Size} bytes) — skipping upload",
                    issueNumber, saveFile.Length);
            }
            else
            {
                var safeFileName = SanitizeFileName(saveFile.FileName);
                try
                {
                    await using var stream = saveFile.OpenReadStream();
                    await blobService.UploadAsync(issueNumber, safeFileName, stream, cancellationToken);
                    var blobPath = $"{issueNumber}/{safeFileName}";
                    var portalUrl = blobService.PortalContainerUrl is { } baseUrl
                        ? $"{baseUrl}/path/{issueNumber}"
                        : null;
                    var comment = portalUrl is not null
                        ? $"📎 Save file attached: [View in Azure Portal]({portalUrl}) — blob path: `{blobPath}`"
                        : $"📎 Save file attached at blob path: `{blobPath}`";
                    await gitHubService.AddCommentAsync(issueNumber, comment, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to upload save file for issue #{IssueNumber}", issueNumber);
                    // Non-fatal: issue was already created; log and continue.
                }
            }
        }

        return new ObjectResult(new { issueNumber, issueUrl }) { StatusCode = StatusCodes.Status201Created };
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "save.bin" : name;
    }
}
