using System.Text.Json.Serialization;

namespace Pkmds.Functions.Models;

public sealed record GitHubIssuePayload(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("issue")] GitHubIssueData Issue);

public sealed record GitHubIssueData(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("state")] string State);
