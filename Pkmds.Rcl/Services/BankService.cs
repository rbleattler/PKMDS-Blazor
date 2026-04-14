namespace Pkmds.Rcl.Services;

public class BankService(IJSRuntime js) : IBankService, IAsyncDisposable
{
    private IJSObjectReference? module;

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            await module.DisposeAsync();
        }
    }

    public async Task AddAsync(PKM pkm, string? tag = null, string? sourceSave = null)
    {
        await GetModuleAsync();
        var storedData = new byte[pkm.SIZE_STORED];
        pkm.WriteDecryptedDataStored(storedData);
        var b64 = Convert.ToBase64String(storedData);
        var meta = new
        {
            species = pkm.Species,
            isShiny = pkm.IsShiny,
            nickname = pkm.Nickname,
            ext = pkm.Extension,
            tag,
            sourceSave
        };

        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("addPokemon", b64, meta);
    }

    public async Task AddRangeAsync(IEnumerable<PKM> pokemon, string? tag = null, string? sourceSave = null)
    {
        await GetModuleAsync();
        // Collect all entries first and send in one JS call / one IDB transaction
        // rather than one round-trip per Pokémon.
        var entries = pokemon.Select(object (pkm) =>
        {
            var storedData = new byte[pkm.SIZE_STORED];
            pkm.WriteDecryptedDataStored(storedData);
            return new
            {
                bytesBase64 = Convert.ToBase64String(storedData),
                meta = new
                {
                    species = pkm.Species,
                    isShiny = pkm.IsShiny,
                    nickname = pkm.Nickname,
                    ext = pkm.Extension,
                    tag,
                    sourceSave
                }
            };
        }).ToArray();

        if (entries.Length > 0)
        {
            if (module is null)
            {
                return;
            }

            await module.InvokeVoidAsync("addRange", (object)entries);
        }
    }

    public async Task<IReadOnlyList<BankEntry>> GetAllAsync()
    {
        await GetModuleAsync();
        if (module is null)
        {
            return [];
        }

        var raw = await module.InvokeAsync<RawEntry[]>("getAllPokemon");

        if (raw.Length == 0)
        {
            return [];
        }

        var results = new List<BankEntry>(raw.Length);
        var invalidIds = new List<long>();

        foreach (var r in raw)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(r.BytesBase64);
            }
            catch (FormatException)
            {
                // Record contains invalid base64 data — schedule it for deletion so it
                // doesn't prevent the rest of the bank from loading.
                invalidIds.Add(r.Id);
                continue;
            }

            if (!FileUtil.TryGetPKM(bytes, out var pkm, r.Meta.Ext))
            {
                // Record is corrupt or uses an unsupported format — schedule it for
                // deletion so it doesn't accumulate as an undeletable ghost entry.
                invalidIds.Add(r.Id);
                continue;
            }

            var speciesName = GameInfo.Strings.Species.Count > pkm.Species
                ? GameInfo.Strings.Species[pkm.Species]
                : string.Empty;

            if (!DateTimeOffset.TryParse(r.AddedAt, out var addedAt))
            {
                addedAt = DateTimeOffset.UtcNow;
            }

            results.Add(new BankEntry
            {
                Id = r.Id,
                Pokemon = pkm,
                SpeciesName = string.IsNullOrEmpty(speciesName)
                    ? "Unknown"
                    : speciesName,
                Tag = r.Meta.Tag,
                SourceSave = r.Meta.SourceSave,
                AddedAt = addedAt
            });
        }

        // Proactively clean up any records that could not be rehydrated.
        foreach (var id in invalidIds)
        {
            await DeleteAsync(id);
        }

        return results;
    }

    public async Task DeleteAsync(long id)
    {
        await GetModuleAsync();
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("deletePokemon", id);
    }

    public async Task ClearAsync()
    {
        await GetModuleAsync();
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("clearAll");
    }

    public async Task<byte[]> ExportAsync()
    {
        await GetModuleAsync();
        // JS exportAll() returns a Uint8Array; Blazor marshals it directly to byte[].
        if (module is null)
        {
            return [];
        }

        return await module.InvokeAsync<byte[]>("exportAll");
    }

    public async Task ImportAsync(byte[] data)
    {
        await GetModuleAsync();
        if (module is null)
        {
            return;
        }

        await module.InvokeVoidAsync("importAll", data);
    }

    public async Task<bool> IsDuplicateAsync(PKM pkm)
    {
        var all = await GetAllAsync();
        var candidateBytes = new byte[pkm.SIZE_STORED];
        pkm.WriteDecryptedDataStored(candidateBytes);
        return all.Any(entry =>
        {
            var entryBytes = new byte[entry.Pokemon.SIZE_STORED];
            entry.Pokemon.WriteDecryptedDataStored(entryBytes);
            return entryBytes.AsSpan().SequenceEqual(candidateBytes);
        });
    }

    public async Task<(IReadOnlyList<PKM> Unique, IReadOnlyList<PKM> Duplicates)> PartitionDuplicatesAsync(
        IEnumerable<PKM> candidates)
    {
        var all = await GetAllAsync();

        // Build a hash set of existing entries' stored data (base64) for O(1) lookup.
        var existingHashes = all
            .Select(e =>
            {
                var data = new byte[e.Pokemon.SIZE_STORED];
                e.Pokemon.WriteDecryptedDataStored(data);
                return Convert.ToBase64String(data);
            })
            .ToHashSet();

        var unique = new List<PKM>();
        var duplicates = new List<PKM>();

        foreach (var pkm in candidates)
        {
            var storedBytes = new byte[pkm.SIZE_STORED];
            pkm.WriteDecryptedDataStored(storedBytes);
            if (existingHashes.Contains(Convert.ToBase64String(storedBytes)))
            {
                duplicates.Add(pkm);
            }
            else
            {
                unique.Add(pkm);
            }
        }

        return (unique, duplicates);
    }

    private async Task GetModuleAsync() =>
        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/bank.js");

    // ── Private DTOs for JS deserialization ───────────────────────────────
    // internal so that Pkmds.Tests can reference these types when setting up
    // JS interop mocks (via InternalsVisibleTo in Pkmds.Rcl.csproj).

    internal sealed class RawEntry
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("bytesBase64")]
        public string BytesBase64 { get; set; } = string.Empty;

        [JsonPropertyName("meta")]
        public RawMeta Meta { get; set; } = new();

        [JsonPropertyName("addedAt")]
        public string AddedAt { get; set; } = string.Empty;
    }

    internal sealed class RawMeta
    {
        [JsonPropertyName("species")]
        public ushort Species { get; set; }

        [JsonPropertyName("isShiny")]
        public bool IsShiny { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("ext")]
        public string Ext { get; set; } = string.Empty;

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("sourceSave")]
        public string? SourceSave { get; set; }
    }
}
