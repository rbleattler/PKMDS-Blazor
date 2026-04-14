namespace Pkmds.Rcl.Models;

/// <summary>
/// Represents the JSON response from the PokePaste <c>/json</c> endpoint.
/// </summary>
public sealed record PokePasteResponse(
    [property: JsonPropertyName("paste")] string Paste,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("notes")] string? Notes);
