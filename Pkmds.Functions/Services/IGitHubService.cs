namespace Pkmds.Functions.Services;

public interface IGitHubService
{
    Task<(int IssueNumber, string IssueUrl)> CreateIssueAsync(
        string title,
        string body,
        CancellationToken cancellationToken = default);

    Task AddCommentAsync(
        int issueNumber,
        string comment,
        CancellationToken cancellationToken = default);
}
