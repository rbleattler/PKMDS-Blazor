using System.IO.Compression;

namespace Pkmds.Rcl.Components.Dialogs;

public partial class BulkExportDialog
{
    public enum BulkExportScope
    {
        Party,
        CurrentBox,
        AllBoxes,
        Everything
    }

    private int currentBoxCount;
    private bool isExporting;
    private int partyCount;
    private BulkExportScope scope = BulkExportScope.CurrentBox;
    private int totalBoxCount;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    protected override void OnInitialized()
    {
        if (AppState.SaveFile is not { } sav)
        {
            return;
        }

        var currentBox = AppState.BoxEdit?.CurrentBox ?? sav.CurrentBox;
        currentBoxCount = CountBox(sav, currentBox);

        var total = 0;
        for (var box = 0; box < sav.BoxCount; box++)
        {
            total += CountBox(sav, box);
        }

        totalBoxCount = total;
        partyCount = CountParty(sav);

        // Default scope: prefer current box, then any box, then party.
        if (currentBoxCount > 0)
        {
            scope = BulkExportScope.CurrentBox;
        }
        else if (totalBoxCount > 0)
        {
            scope = BulkExportScope.AllBoxes;
        }
        else if (partyCount > 0)
        {
            scope = BulkExportScope.Party;
        }
    }

    private static int CountBox(SaveFile sav, int box)
    {
        var count = 0;
        for (var slot = 0; slot < sav.BoxSlotCount; slot++)
        {
            if (sav.GetBoxSlotAtIndex(box, slot).Species != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountParty(SaveFile sav)
    {
        var count = 0;
        for (var i = 0; i < sav.PartyCount; i++)
        {
            if (sav.GetPartySlotAtIndex(i).Species != 0)
            {
                count++;
            }
        }

        return count;
    }

    private int GetScopedCount() => scope switch
    {
        BulkExportScope.Party => partyCount,
        BulkExportScope.CurrentBox => currentBoxCount,
        BulkExportScope.AllBoxes => totalBoxCount,
        BulkExportScope.Everything => partyCount + totalBoxCount,
        _ => 0
    };

    private async Task ExportAsync()
    {
        if (AppState.SaveFile is not { } sav)
        {
            return;
        }

        var pokemonToExport = CollectPokemon(sav);
        if (pokemonToExport.Count == 0)
        {
            Snackbar.Add("No Pokémon to export.", Severity.Warning);
            return;
        }

        isExporting = true;
        StateHasChanged();

        try
        {
            var zipBytes = BuildZip(pokemonToExport);
            var fileName = scope switch
            {
                BulkExportScope.Party => "pokemon_export_party.zip",
                BulkExportScope.CurrentBox =>
                    $"pokemon_export_box{(AppState.BoxEdit?.CurrentBox ?? sav.CurrentBox) + 1}.zip",
                BulkExportScope.AllBoxes => "pokemon_export_boxes.zip",
                BulkExportScope.Everything => "pokemon_export.zip",
                _ => "pokemon_export.zip"
            };

            await WriteZipAsync(zipBytes, fileName);

            Snackbar.Add($"Exported {pokemonToExport.Count} Pokémon.", Severity.Success);
            MudDialog?.Close();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Export failed: {ex.Message}", Severity.Error);
        }
        finally
        {
            isExporting = false;
            StateHasChanged();
        }
    }

    private List<PKM> CollectPokemon(SaveFile sav)
    {
        var result = new List<PKM>();

        if (scope is BulkExportScope.Party or BulkExportScope.Everything)
        {
            for (var i = 0; i < sav.PartyCount; i++)
            {
                var pkm = sav.GetPartySlotAtIndex(i);
                if (pkm.Species != 0)
                {
                    result.Add(pkm);
                }
            }
        }

        var boxes = scope switch
        {
            BulkExportScope.CurrentBox => new[] { AppState.BoxEdit?.CurrentBox ?? sav.CurrentBox },
            BulkExportScope.AllBoxes or BulkExportScope.Everything => Enumerable.Range(0, sav.BoxCount).ToArray(),
            _ => []
        };

        foreach (var box in boxes)
        {
            for (var slot = 0; slot < sav.BoxSlotCount; slot++)
            {
                var pkm = sav.GetBoxSlotAtIndex(box, slot);
                if (pkm.Species != 0)
                {
                    result.Add(pkm);
                }
            }
        }

        return result;
    }

    private byte[] BuildZip(IReadOnlyList<PKM> pokemon)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pkm in pokemon)
            {
                pkm.RefreshChecksum();
                var bytes = new byte[pkm.SIZE_PARTY];
                pkm.WriteDecryptedDataParty(bytes);

                var entryName = GetUniqueEntryName(usedNames, AppService.GetCleanFileName(pkm));
                var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                using var es = entry.Open();
                es.Write(bytes);
            }
        }

        return ms.ToArray();
    }

    private static string GetUniqueEntryName(HashSet<string> used, string desired)
    {
        if (used.Add(desired))
        {
            return desired;
        }

        var ext = Path.GetExtension(desired);
        var stem = Path.GetFileNameWithoutExtension(desired);
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem}_{i}{ext}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private async Task WriteZipAsync(byte[] data, string fileName)
    {
        // Mirror MainLayout.WriteFile: prefer File System Access API, fall back to anchor.
        if (await FileSystemAccessService.IsSupportedAsync())
        {
            try
            {
                await JSRuntime.InvokeVoidAsync(
                    "showFilePickerAndWrite",
                    fileName,
                    data,
                    ".zip",
                    "Pokémon Export");
                return;
            }
            catch (JSException ex) when (ex.Message.Contains("AbortError", StringComparison.OrdinalIgnoreCase) ||
                                         ex.Message.Contains("aborted a request", StringComparison.OrdinalIgnoreCase))
            {
                // User dismissed the picker — not an error.
                return;
            }
        }

        // Legacy fallback: base64 data-URI anchor click.
        var base64 = Convert.ToBase64String(data);
        var anchor = await JSRuntime.InvokeAsync<IJSObjectReference>("eval", "document.createElement('a')");
        await anchor.InvokeVoidAsync("setAttribute", "href", $"data:application/zip;base64,{base64}");
        await anchor.InvokeVoidAsync("setAttribute", "download", fileName);
        await anchor.InvokeVoidAsync("click");
    }

    private void Cancel() => MudDialog?.Close(DialogResult.Cancel());
}
