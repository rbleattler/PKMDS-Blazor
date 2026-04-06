namespace Pkmds.Rcl.Services;

public class BankService(IJSRuntime js) : IBankService, IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/bank.js");

    public async Task AddAsync(PKM pkm, string? tag = null)
    {
        var module = await GetModuleAsync();
        var b64 = Convert.ToBase64String(pkm.DecryptedBoxData);
        var meta = new
        {
            species = pkm.Species,
            isShiny = pkm.IsShiny,
            nickname = pkm.Nickname,
            ext = pkm.Extension,
            tag
        };
        await module.InvokeVoidAsync("addPokemon", b64, meta);
    }

    public async Task AddRangeAsync(IEnumerable<PKM> pokemon, string? tag = null)
    {
        foreach (var pkm in pokemon)
        {
            await AddAsync(pkm, tag);
        }
    }

    public async Task<IReadOnlyList<BankEntry>> GetAllAsync()
    {
        var module = await GetModuleAsync();
        var raw = await module.InvokeAsync<RawEntry[]>("getAllPokemon");

        var results = new List<BankEntry>(raw.Length);
        foreach (var r in raw)
        {
            var bytes = Convert.FromBase64String(r.BytesBase64);
            if (!FileUtil.TryGetPKM(bytes, out var pkm, r.Meta.Ext))
            {
                continue;
            }

            var speciesName = GameInfo.Strings.Species.Count > pkm.Species
                ? GameInfo.Strings.Species[pkm.Species]
                : string.Empty;

            _ = DateTimeOffset.TryParse(r.AddedAt, out var addedAt);

            results.Add(new BankEntry
            {
                Id = r.Id,
                Pokemon = pkm,
                SpeciesName = string.IsNullOrEmpty(speciesName) ? "Unknown" : speciesName,
                Tag = r.Meta.Tag,
                AddedAt = addedAt
            });
        }

        return results;
    }

    public async Task DeleteAsync(long id)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deletePokemon", id);
    }

    public async Task ClearAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearAll");
    }

    public async Task<byte[]> ExportAsync()
    {
        var module = await GetModuleAsync();
        var byteList = await module.InvokeAsync<int[]>("exportAll");
        return byteList.Select(b => (byte)b).ToArray();
    }

    public async Task ImportAsync(byte[] data)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("importAll", data);
    }

    public async Task<bool> IsDuplicateAsync(PKM pkm)
    {
        var all = await GetAllAsync();
        var candidateBytes = pkm.DecryptedBoxData;
        return all.Any(entry =>
            entry.Pokemon.DecryptedBoxData.AsSpan().SequenceEqual(candidateBytes));
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    // ── Private DTOs for JS deserialization ───────────────────────────────

    private sealed class RawEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("bytesBase64")]
        public string BytesBase64 { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("meta")]
        public RawMeta Meta { get; set; } = new();

        [System.Text.Json.Serialization.JsonPropertyName("addedAt")]
        public string AddedAt { get; set; } = string.Empty;
    }

    private sealed class RawMeta
    {
        [System.Text.Json.Serialization.JsonPropertyName("species")]
        public ushort Species { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("isShiny")]
        public bool IsShiny { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("ext")]
        public string Ext { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("tag")]
        public string? Tag { get; set; }
    }
}
