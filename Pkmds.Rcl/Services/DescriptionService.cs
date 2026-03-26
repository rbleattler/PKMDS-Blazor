using System.Text.Json;
using System.Text.Json.Serialization;
using Pkmds.Rcl.Models;

namespace Pkmds.Rcl.Services;

public sealed class DescriptionService(HttpClient http) : IDescriptionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Lazily loaded caches — store the Task so all concurrent callers share one HTTP request
    // rather than each firing their own (which causes silent failures under load in WASM).
    private Task<Dictionary<string, JsonAbilityEntry>>? _abilitiesTask;
    private Task<Dictionary<string, JsonMoveEntry>>? _movesTask;
    private Task<Dictionary<string, JsonItemEntry>>? _itemsTask;
    private Task<Dictionary<string, Dictionary<string, string>>>? _tmDataTask;
    private Task<Dictionary<string, Dictionary<string, string>>>? _hmDataTask;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public async Task<MoveSummary?> GetMoveInfoAsync(int moveId, GameVersion version)
    {
        var moves = await GetMovesAsync().ConfigureAwait(false);
        if (!moves.TryGetValue(moveId.ToString(), out var entry))
            return null;

        var targetVg = ToVersionGroup(version);
        var epoch = ResolveEpoch(entry.Stats, targetVg);
        var description = ResolveFlavor(entry.Flavor, targetVg) is { Length: > 0 } flavor
            ? flavor
            : entry.Description;

        return new MoveSummary(
            entry.Name,
            epoch?.Type ?? string.Empty,
            epoch?.Category ?? string.Empty,
            epoch?.Power,
            epoch?.Pp,
            epoch?.Accuracy,
            description,
            entry.Target ?? string.Empty,
            entry.Flags ?? []);
    }

    public async Task<AbilitySummary?> GetAbilityInfoAsync(int abilityId, GameVersion version)
    {
        var abilities = await GetAbilitiesAsync().ConfigureAwait(false);
        if (!abilities.TryGetValue(abilityId.ToString(), out var entry))
            return null;

        var description = ResolveFlavor(entry.Flavor, ToVersionGroup(version)) is { Length: > 0 } flavor
            ? flavor
            : entry.Description;

        return new AbilitySummary(entry.Name, description);
    }

    public async Task<string?> GetTMMoveNameAsync(string tmNumber, GameVersion version)
    {
        var tmData = await GetTmDataAsync().ConfigureAwait(false);
        var key = ToTmDataKey(version);
        if (key is null || !tmData.TryGetValue(key, out var versionData))
            return null;
        return versionData.TryGetValue(tmNumber, out var moveName) ? moveName : null;
    }

    public async Task<string?> GetHMMoveNameAsync(string hmKey, GameVersion version)
    {
        var hmData = await GetHmDataAsync().ConfigureAwait(false);
        var key = ToHmDataKey(version);
        if (key is null || !hmData.TryGetValue(key, out var versionData))
            return null;
        return versionData.TryGetValue(hmKey, out var moveName) ? moveName : null;
    }

    public async Task<ItemSummary?> GetItemInfoAsync(string itemName, GameVersion version)
    {
        var items = await GetItemsAsync().ConfigureAwait(false);
        var key = itemName.Trim().ToLowerInvariant();
        if (!items.TryGetValue(key, out var entry))
            return null;

        var description = ResolveFlavor(entry.Flavor, ToVersionGroup(version)) is { Length: > 0 } flavor
            ? flavor
            : entry.Description;

        return new ItemSummary(entry.Name, description);
    }

    // -------------------------------------------------------------------------
    // Lazy loaders
    // -------------------------------------------------------------------------

    private const string DataRoot = "_content/Pkmds.Rcl/data/";

    private Task<Dictionary<string, JsonAbilityEntry>> GetAbilitiesAsync() =>
        _abilitiesTask ??= LoadAsync<Dictionary<string, JsonAbilityEntry>>(DataRoot + "ability-info.json");

    private Task<Dictionary<string, JsonMoveEntry>> GetMovesAsync() =>
        _movesTask ??= LoadAsync<Dictionary<string, JsonMoveEntry>>(DataRoot + "move-info.json");

    private Task<Dictionary<string, JsonItemEntry>> GetItemsAsync() =>
        _itemsTask ??= LoadAsync<Dictionary<string, JsonItemEntry>>(DataRoot + "item-info.json");

    private Task<Dictionary<string, Dictionary<string, string>>> GetTmDataAsync() =>
        _tmDataTask ??= LoadAsync<Dictionary<string, Dictionary<string, string>>>(DataRoot + "tm-data.json");

    private Task<Dictionary<string, Dictionary<string, string>>> GetHmDataAsync() =>
        _hmDataTask ??= LoadAsync<Dictionary<string, Dictionary<string, string>>>(DataRoot + "hm-data.json");

    private async Task<T> LoadAsync<T>(string path) where T : new()
    {
        try
        {
            var stream = await http.GetStreamAsync(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions) ?? new T();
        }
        catch
        {
            return new T();
        }
    }

    // -------------------------------------------------------------------------
    // Version-group resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps a PKHeX <see cref="GameVersion"/> to the hm-data.json key for that game's HM list.
    /// Returns null for games without HMs (Gen 7+, PLA, GCN games).
    /// </summary>
    private static string? ToHmDataKey(GameVersion version) => version switch
    {
        GameVersion.RD or GameVersion.GN or GameVersion.BU
            or GameVersion.RB or GameVersion.RBY or GameVersion.YW
            or GameVersion.Gen1                                          => "gen1",
        GameVersion.GD or GameVersion.SI or GameVersion.C
            or GameVersion.GS or GameVersion.GSC or GameVersion.Gen2    => "gen2",
        GameVersion.R or GameVersion.S or GameVersion.E
            or GameVersion.RS or GameVersion.RSE                        => "gen3rse",
        GameVersion.FR or GameVersion.LG or GameVersion.FRLG            => "gen3frlg",
        GameVersion.D or GameVersion.P or GameVersion.Pt or GameVersion.DP => "gen4dpp",
        GameVersion.HG or GameVersion.SS or GameVersion.HGSS            => "gen4hgss",
        GameVersion.B or GameVersion.W or GameVersion.B2 or GameVersion.W2
            or GameVersion.BW or GameVersion.B2W2 or GameVersion.Gen5   => "gen5",
        GameVersion.X or GameVersion.Y or GameVersion.XY                => "gen6xy",
        GameVersion.OR or GameVersion.AS or GameVersion.ORAS            => "gen6oras",
        _                                                                => null, // Gen 7+, PLA, GCN — no HMs
    };

    /// <summary>
    /// Maps a PKHeX <see cref="GameVersion"/> to the tm-data.json key for that game's TM list.
    /// Returns null for games without a standard TM system (e.g. PLA).
    /// </summary>
    private static string? ToTmDataKey(GameVersion version) => version switch
    {
        GameVersion.RD or GameVersion.GN or GameVersion.BU
            or GameVersion.RB or GameVersion.RBY or GameVersion.YW
            or GameVersion.Gen1                                          => "gen1",
        GameVersion.GD or GameVersion.SI or GameVersion.C
            or GameVersion.GS or GameVersion.GSC or GameVersion.Gen2    => "gen2",
        GameVersion.R or GameVersion.S or GameVersion.E
            or GameVersion.FR or GameVersion.LG
            or GameVersion.RS or GameVersion.RSE or GameVersion.FRLG
            or GameVersion.CXD or GameVersion.COLO or GameVersion.XD    => "gen3",
        GameVersion.D or GameVersion.P or GameVersion.Pt
            or GameVersion.HG or GameVersion.SS
            or GameVersion.DP or GameVersion.HGSS or GameVersion.Gen4   => "gen4",
        GameVersion.B or GameVersion.W or GameVersion.B2 or GameVersion.W2
            or GameVersion.BW or GameVersion.B2W2 or GameVersion.Gen5   => "gen5",
        GameVersion.X or GameVersion.Y or GameVersion.OR or GameVersion.AS
            or GameVersion.XY or GameVersion.ORAS or GameVersion.Gen6   => "gen6",
        GameVersion.SN or GameVersion.MN or GameVersion.US or GameVersion.UM
            or GameVersion.SM or GameVersion.USUM or GameVersion.Gen7   => "gen7sm",
        GameVersion.GP or GameVersion.GE or GameVersion.GG
            or GameVersion.Gen7b                                         => "gen7lgpe",
        GameVersion.SW or GameVersion.SH or GameVersion.SWSH            => "gen8swsh",
        GameVersion.BD or GameVersion.SP or GameVersion.BDSP            => "gen8bdsp",
        GameVersion.PLA or GameVersion.Gen8                             => null, // no standard TM system
        GameVersion.SL or GameVersion.VL or GameVersion.SV
            or GameVersion.Gen9                                          => "gen9sv",
        GameVersion.ZA                                                   => "gen9za",
        _                                                                => null,
    };

    /// <summary>
    /// Maps a PKHeX <see cref="GameVersion"/> to the corresponding PokeAPI version-group ID.
    /// The version-group IDs match those in the PokeAPI CSV data used to generate the JSON files.
    /// </summary>
    private static int ToVersionGroup(GameVersion version) => version switch
    {
        GameVersion.RD or GameVersion.GN or GameVersion.BU
            or GameVersion.RB or GameVersion.RBY or GameVersion.Gen1   => 1,  // red-blue
        GameVersion.YW                                                   => 2,  // yellow
        GameVersion.GD or GameVersion.SI or GameVersion.GS              => 3,  // gold-silver
        GameVersion.C or GameVersion.GSC or GameVersion.Gen2            => 4,  // crystal
        GameVersion.R or GameVersion.S or GameVersion.RS or GameVersion.RSE => 5,  // ruby-sapphire
        GameVersion.E                                                    => 6,  // emerald
        GameVersion.FR or GameVersion.LG or GameVersion.FRLG            => 7,  // firered-leafgreen
        GameVersion.CXD or GameVersion.COLO or GameVersion.XD          => 13, // xd (closest GCN match)
        GameVersion.D or GameVersion.P or GameVersion.DP                => 8,  // diamond-pearl
        GameVersion.Pt                                                   => 9,  // platinum
        GameVersion.HG or GameVersion.SS or GameVersion.HGSS or GameVersion.Gen4 => 10, // heartgold-soulsilver
        GameVersion.B or GameVersion.W or GameVersion.BW                => 11, // black-white
        GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2 or GameVersion.Gen5 => 14, // black-2-white-2
        GameVersion.X or GameVersion.Y or GameVersion.XY                => 15, // x-y
        GameVersion.OR or GameVersion.AS or GameVersion.ORAS or GameVersion.Gen6 => 16, // omega-ruby-alpha-sapphire
        GameVersion.SN or GameVersion.MN or GameVersion.SM              => 17, // sun-moon
        GameVersion.US or GameVersion.UM or GameVersion.USUM or GameVersion.Gen7 => 18, // ultra-sun-ultra-moon
        GameVersion.GP or GameVersion.GE or GameVersion.GG or GameVersion.Gen7b  => 19, // lets-go
        GameVersion.SW or GameVersion.SH or GameVersion.SWSH            => 20, // sword-shield
        GameVersion.BD or GameVersion.SP or GameVersion.BDSP            => 23, // brilliant-diamond-shining-pearl
        GameVersion.PLA                                                  => 24, // legends-arceus
        GameVersion.Gen8                                                 => 24, // latest gen 8 (PLA)
        GameVersion.SL or GameVersion.VL or GameVersion.SV or GameVersion.Gen9 => 25, // scarlet-violet
        GameVersion.ZA                                                   => 30, // legends-za
        GameVersion.CP                                                   => 31, // mega-dimension
        _                                                                => 25, // default to latest known
    };

    /// <summary>
    /// Returns the flavor text entry with the largest version-group ID that is ≤
    /// <paramref name="targetVg"/>. Falls back to the smallest available entry if
    /// none qualifies (e.g., the target game predates all entries).
    /// </summary>
    private static string? ResolveFlavor(Dictionary<string, string>? flavor, int targetVg)
    {
        if (flavor is null or { Count: 0 })
            return null;

        string? best = null;
        int bestVg = -1;
        int smallestVg = int.MaxValue;
        string? smallestEntry = null;

        foreach (var (key, text) in flavor)
        {
            if (!int.TryParse(key, out var vg))
                continue;
            if (vg <= targetVg && vg > bestVg)
            {
                bestVg = vg;
                best = text;
            }
            if (vg < smallestVg)
            {
                smallestVg = vg;
                smallestEntry = text;
            }
        }

        return best ?? smallestEntry;
    }

    /// <summary>
    /// From the stat epochs array (sorted ascending by fromVersionGroup), returns the epoch
    /// whose <c>fromVersionGroup</c> is the largest value ≤ <paramref name="targetVg"/>.
    /// Falls back to the first epoch if the target predates all epochs.
    /// </summary>
    private static JsonMoveStatEpoch? ResolveEpoch(List<JsonMoveStatEpoch>? epochs, int targetVg)
    {
        if (epochs is null or { Count: 0 })
            return null;

        JsonMoveStatEpoch? best = null;
        foreach (var epoch in epochs)
        {
            if (epoch.FromVersionGroup <= targetVg)
                best = epoch;
        }
        return best ?? epochs[0];
    }

    // -------------------------------------------------------------------------
    // JSON deserialization models (internal)
    // -------------------------------------------------------------------------

    private sealed record JsonAbilityEntry(
        string Name,
        string Description,
        Dictionary<string, string>? Flavor);

    private sealed record JsonMoveEntry(
        string Name,
        string Description,
        string? Target,
        List<string>? Flags,
        List<JsonMoveStatEpoch> Stats,
        Dictionary<string, string>? Flavor);

    private sealed record JsonMoveStatEpoch(
        int FromVersionGroup,
        string Type,
        string Category,
        int? Power,
        int? Pp,
        int? Accuracy);

    private sealed record JsonItemEntry(
        string Name,
        string Description,
        Dictionary<string, string>? Flavor);
}
