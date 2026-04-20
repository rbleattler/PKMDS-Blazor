using System.Net.Http.Json;
using System.Text.Json;

namespace Pkmds.Web.Services;

public class BugReportService(IConfiguration configuration, HttpClient httpClient) : IBugReportService
{
    private readonly string? _functionUrl = configuration["BugReportService:FunctionUrl"];

    public async Task<BugReportResult> SubmitBugReportAsync(BugReportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_functionUrl))
        {
            return new BugReportResult(false, ErrorMessage: "Bug reporting is not configured.");
        }

        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(request.Name), "name");
            content.Add(new StringContent(request.Email), "email");
            content.Add(new StringContent(request.Description), "description");
            content.Add(new StringContent(request.AppVersion), "appVersion");
            content.Add(new StringContent(request.UserAgent), "userAgent");

            if (request.SaveGameName is not null)
            {
                content.Add(new StringContent(request.SaveGameName), "saveGameName");
            }

            if (request.SaveRevision is not null)
            {
                content.Add(new StringContent(request.SaveRevision), "saveRevision");
            }

            if (request.SaveFileSource is not null)
            {
                content.Add(new StringContent(request.SaveFileSource), "saveFileSource");
            }

            if (request.SaveFileType is not null)
            {
                content.Add(new StringContent(request.SaveFileType), "saveFileType");
            }

            if (request.SaveFileBytes is { Length: > 0 } saveBytes && request.SaveFileName is not null)
            {
                content.Add(new ByteArrayContent(saveBytes), "saveFile", request.SaveFileName);
            }

            var response = await httpClient.PostAsync($"{_functionUrl}/api/SubmitBugReport", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
                var issueUrl = json.TryGetProperty("issueUrl", out var urlElement)
                    ? urlElement.GetString()
                    : null;
                return new BugReportResult(true, IssueUrl: issueUrl);
            }

            var errorJson = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var errorMessage = errorJson.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString()
                : $"Submission failed with status {(int)response.StatusCode}.";
            return new BugReportResult(false, ErrorMessage: errorMessage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BugReportResult(false, ErrorMessage: "Failed to submit report. Please check your connection and try again.");
        }
    }
}
