using Microsoft.Extensions.Configuration;
using Octokit;

namespace Pkmds.Functions.Services;

public class GitHubService(IConfiguration configuration) : IGitHubService
{
    private readonly string _owner = configuration["GitHubOwner"]
        ?? throw new InvalidOperationException("GitHubOwner configuration is required.");
    private readonly string _repo = configuration["GitHubRepo"]
        ?? throw new InvalidOperationException("GitHubRepo configuration is required.");
    private readonly GitHubClient _client = new GitHubClient(new ProductHeaderValue("pkmds-bug-reporter"))
    {
        Credentials = new Credentials(
            configuration["GitHubPat"] ?? throw new InvalidOperationException("GitHubPat configuration is required.")),
    };

    public async Task<(int IssueNumber, string IssueUrl)> CreateIssueAsync(
        string title,
        string body,
        CancellationToken cancellationToken = default)
    {
        var newIssue = new NewIssue(title) { Body = body };
        newIssue.Labels.Add("bug");

        var issue = await _client.Issue.Create(_owner, _repo, newIssue);
        return (issue.Number, issue.HtmlUrl);
    }

    public async Task AddCommentAsync(
        int issueNumber,
        string comment,
        CancellationToken cancellationToken = default)
    {
        await _client.Issue.Comment.Create(_owner, _repo, issueNumber, comment);
    }
}
