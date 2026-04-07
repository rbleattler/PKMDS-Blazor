namespace Pkmds.Rcl.Services;

public class BackupService(IJSRuntime js) : IBackupService, IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> GetModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/backup.js");

    public async Task<long> CreateBackupAsync(byte[] saveBytes, SaveFile saveFile, string? fileName, bool isManicEmu, string source)
    {
        var module = await GetModuleAsync();
        var b64 = Convert.ToBase64String(saveBytes);
        var meta = new
        {
            fileName = fileName ?? string.Empty,
            saveType = saveFile.GetType().Name,
            generation = (int)saveFile.Generation,
            gameVersion = saveFile.Version.ToString(),
            trainerName = saveFile.OT,
            sizeBytes = (long)saveBytes.Length,
            isManicEmu
        };
        return await module.InvokeAsync<long>("addBackup", b64, meta, source);
    }

    public async Task<IReadOnlyList<BackupEntry>> GetAllMetadataAsync()
    {
        var module = await GetModuleAsync();
        var raw = await module.InvokeAsync<RawBackupEntry[]>("getBackupMetadata");

        if (raw is null || raw.Length == 0)
        {
            return [];
        }

        var results = new List<BackupEntry>(raw.Length);
        foreach (var r in raw)
        {
            if (!DateTimeOffset.TryParse(r.CreatedAt, out var createdAt))
            {
                createdAt = DateTimeOffset.UtcNow;
            }

            results.Add(new BackupEntry
            {
                Id = r.Id,
                FileName = r.Meta?.FileName ?? string.Empty,
                SaveType = r.Meta?.SaveType ?? string.Empty,
                Generation = r.Meta?.Generation ?? 0,
                GameVersion = r.Meta?.GameVersion ?? string.Empty,
                TrainerName = r.Meta?.TrainerName ?? string.Empty,
                SizeBytes = r.Meta?.SizeBytes ?? 0,
                IsManicEmu = r.Meta?.IsManicEmu ?? false,
                CreatedAt = createdAt,
                Source = r.Source ?? string.Empty
            });
        }

        return results;
    }

    public async Task<byte[]?> GetBackupBytesAsync(long id)
    {
        var module = await GetModuleAsync();
        var raw = await module.InvokeAsync<RawBackupEntry?>("getBackup", id);

        if (raw?.BytesBase64 is null)
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(raw.BytesBase64);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task DeleteAsync(long id)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("deleteBackup", id);
    }

    public async Task ClearAsync()
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("clearAll");
    }

    public async Task EnforceRetentionAsync(int maxBackups)
    {
        var module = await GetModuleAsync();
        var count = await module.InvokeAsync<int>("getCount");
        if (count <= maxBackups)
        {
            return;
        }

        var excess = count - maxBackups;
        var oldestIds = await module.InvokeAsync<long[]>("getOldestIds", excess);
        if (oldestIds.Length > 0)
        {
            await module.InvokeVoidAsync("deleteMultiple", (object)oldestIds);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    // ── Internal DTOs for JS deserialization (internal for test/mocking support) ──

    internal sealed class RawBackupEntry
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("bytesBase64")]
        public string? BytesBase64 { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("meta")]
        public RawBackupMeta? Meta { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    internal sealed class RawBackupMeta
    {
        [System.Text.Json.Serialization.JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("saveType")]
        public string? SaveType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("generation")]
        public int? Generation { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("gameVersion")]
        public string? GameVersion { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("trainerName")]
        public string? TrainerName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("sizeBytes")]
        public long? SizeBytes { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("isManicEmu")]
        public bool? IsManicEmu { get; set; }
    }
}
