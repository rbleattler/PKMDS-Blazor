using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Pkmds.Tests;

/// <summary>
/// Unit tests for <see cref="BankService"/> using bUnit's JS-interop mocks.
/// <see cref="BankService.RawEntry"/> and <see cref="BankService.RawMeta"/> are
/// <c>internal</c> and accessible here via <c>InternalsVisibleTo</c> in Pkmds.Rcl.csproj.
/// </summary>
public class BankServiceTests
{
    private const string TestFilesPath = "../../../TestFiles";

    /// <summary>
    /// Creates a <see cref="BankService"/> backed by bUnit's JS-interop mock with
    /// <paramref name="bankEntries"/> pre-loaded as the <c>getAllPokemon</c> result.
    /// </summary>
    private static (BankService Service, BunitContext Ctx) CreateService(
        BankService.RawEntry[]? bankEntries = null)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;

        ctx.JSInterop
            .SetupModule("./js/bank.js")
            .Setup<BankService.RawEntry[]>("getAllPokemon")
            .SetResult(bankEntries ?? []);

        var jsRuntime = ctx.Services.GetRequiredService<IJSRuntime>();
        var service = new BankService(jsRuntime);
        return (service, ctx);
    }

    [Fact]
    public async Task GetAllAsync_EmptyBank_ReturnsEmptyList()
    {
        var (service, ctx) = CreateService();

        var result = await service.GetAllAsync();

        result.Should().BeEmpty();

        await service.DisposeAsync();
        ctx.Dispose();
    }

    [Fact]
    public async Task IsDuplicateAsync_EmptyBank_ReturnsFalse()
    {
        var (service, ctx) = CreateService();

        var data = File.ReadAllBytes(Path.Combine(TestFilesPath, "Lucario_B06DDFAD.pk5"));
        FileUtil.TryGetPKM(data, out var pkm, ".pk5").Should().BeTrue();

        var result = await service.IsDuplicateAsync(pkm!);

        result.Should().BeFalse();

        await service.DisposeAsync();
        ctx.Dispose();
    }

    [Fact]
    public async Task PartitionDuplicatesAsync_EmptyBank_AllReturnedAsUnique()
    {
        var (service, ctx) = CreateService();

        var data = File.ReadAllBytes(Path.Combine(TestFilesPath, "Lucario_B06DDFAD.pk5"));
        FileUtil.TryGetPKM(data, out var pkm, ".pk5").Should().BeTrue();

        var (unique, duplicates) = await service.PartitionDuplicatesAsync([pkm!]);

        unique.Should().ContainSingle();
        duplicates.Should().BeEmpty();

        await service.DisposeAsync();
        ctx.Dispose();
    }

    [Fact]
    public async Task PartitionDuplicatesAsync_BankContainsPkm_DetectedAsDuplicate()
    {
        var data = File.ReadAllBytes(Path.Combine(TestFilesPath, "Lucario_B06DDFAD.pk5"));
        FileUtil.TryGetPKM(data, out var pkm, ".pk5").Should().BeTrue();

        // Seed the bank with a RawEntry whose bytesBase64 matches the test PKM.
        var rawEntry = new BankService.RawEntry
        {
            Id = 1,
            BytesBase64 = Convert.ToBase64String(pkm!.DecryptedBoxData),
            Meta = new BankService.RawMeta
            {
                Species = pkm.Species,
                IsShiny = pkm.IsShiny,
                Nickname = pkm.Nickname,
                Ext = pkm.Extension,
                Tag = null,
            },
            AddedAt = DateTimeOffset.UtcNow.ToString("O"),
        };

        var (service, ctx) = CreateService([rawEntry]);

        var (unique, duplicates) = await service.PartitionDuplicatesAsync([pkm]);

        unique.Should().BeEmpty();
        duplicates.Should().ContainSingle();

        await service.DisposeAsync();
        ctx.Dispose();
    }

    [Fact]
    public async Task ExportAsync_ReturnsExpectedBytes()
    {
        var expectedBytes = "test json"u8.ToArray();

        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop
            .SetupModule("./js/bank.js")
            .Setup<byte[]>("exportAll")
            .SetResult(expectedBytes);

        var service = new BankService(ctx.Services.GetRequiredService<IJSRuntime>());

        var result = await service.ExportAsync();

        result.Should().BeEquivalentTo(expectedBytes);

        await service.DisposeAsync();
        ctx.Dispose();
    }
}
