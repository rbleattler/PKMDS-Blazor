using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace Pkmds.Core.Utilities;

/// <summary>
/// Helpers for importing and exporting 3DS save files in the Manic EMU <c>.3ds.sav</c> ZIP format.
/// </summary>
/// <remarks>
/// Manic EMU exports 3DS saves as a ZIP archive. The canonical extension is
/// <c>GameTitle.3ds.sav</c> on every platform — the iOS build uses the same suffix as
/// desktop (see <c>ManicEmu/Sources/Tools/Cores/ThreeDS.swift</c> and
/// <c>ManicEmu/Sources/Tools/Others/ShareManager.swift</c> in the upstream repo).
/// We still *accept* <c>.3ds.save</c> (with a trailing <c>e</c>) defensively — users
/// sometimes rename manually, and iOS Safari has been observed mangling compound
/// extensions on blob-URL downloads — but PKMDS should never produce it when round-tripping
/// a <c>.3ds.sav</c> upload. Manic EMU's own importer matches <c>url.path.contains(".3ds.sav")</c>
/// as a substring check, so <c>.3ds.save</c> happens to re-import successfully too.
/// <para>
/// The ZIP contains the full <c>sdmc/</c> directory tree from Citra's virtual SD card,
/// e.g. <c>sdmc/Nintendo 3DS/…/title/00040000/00055d00/data/00000001/main</c>. The
/// PKHeX-compatible save bytes are stored as a single binary file entry inside that
/// structure. To round-trip a save through PKMDS:
/// </para>
/// <list type="number">
/// <item>User exports <c>.3ds.sav</c> from Manic EMU.</item>
/// <item>PKMDS detects the ZIP, finds the save entry, and loads it (see <see cref="SaveFileLoader" />).</item>
/// <item>User edits the save in PKMDS.</item>
/// <item>
/// PKMDS rebuilds the ZIP with the edited save bytes and offers it for download
/// using the same compound extension as the original so Manic EMU can import it directly.
/// </item>
/// </list>
/// <para>
/// Load-path ordering matters: PKHeX.Core ships its own <c>ZipReader</c> that recognises
/// any ZIP with a <c>main</c> or <c>SaveData.bin</c> entry inside and unwraps it invisibly.
/// If the Manic EMU ZIP detection runs <em>after</em> <c>SaveUtil.TryGetSaveFile</c>, PKHeX
/// swallows the archive, we never see it as a ZIP, <see cref="ManicEmuSaveContext" /> is never
/// set, and export silently produces raw bytes that Manic EMU can't re-import (see issue #750
/// for the regression caused by missing this ordering). Always run ZIP detection first.
/// </para>
/// </remarks>
public static class ManicEmuSaveHelper
{
    private const string SdmcPrefix = "sdmc/";

    // 3DS save files are at most a few MB; cap at 8 MB to guard against ZIP bombs.
    private const long MaxUncompressedEntrySize = 8 * 1024 * 1024;

    // A real Manic EMU ZIP contains only a handful of entries total; cap at 500
    // to prevent DoS from iterating a crafted archive with many non-sdmc/ entries.
    private const int MaxTotalEntries = 500;

    // A real Manic EMU ZIP contains only a handful of sdmc/ entries; cap at 100
    // to prevent DoS from a crafted ZIP with many qualifying entries.
    private const int MaxSdmcEntriesToInspect = 100;

    /// <summary>
    /// Determines whether <paramref name="data" /> looks like a ZIP archive.
    /// </summary>
    public static bool IsZip(ReadOnlySpan<byte> data) =>
        data.Length >= 4 &&
        data[0] == 0x50 && data[1] == 0x4B &&
        data[2] == 0x03 && data[3] == 0x04;

    /// <summary>
    /// Tries to find a PKHeX-loadable save file inside a Manic EMU <c>.3ds.sav</c> ZIP.
    /// </summary>
    /// <param name="zipBytes">Raw bytes of the <c>.3ds.sav</c> ZIP archive.</param>
    /// <param name="fileName">
    /// Original filename of the ZIP (forwarded to <see cref="SaveUtil.TryGetSaveFile" /> for
    /// format detection); may be <see langword="null" />.
    /// </param>
    /// <param name="saveFile">
    /// The parsed <see cref="SaveFile" /> instance, ready to use directly.
    /// </param>
    /// <param name="context">
    /// Metadata needed to rebuild the ZIP on export.  Pass this back to
    /// <see cref="RebuildZip" /> once the user has finished editing.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if a recognisable save was found inside the ZIP;
    /// <see langword="false" /> otherwise.
    /// </returns>
    public static bool TryExtractSaveFromZip(
        byte[] zipBytes,
        string? fileName,
        [NotNullWhen(true)] out SaveFile? saveFile,
        [NotNullWhen(true)] out ManicEmuSaveContext? context)
    {
        saveFile = null;
        context = null;

        if (!IsZip(zipBytes))
        {
            return false;
        }

        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            if (archive.Entries.Count > MaxTotalEntries)
            {
                return false;
            }

            var inspected = 0;
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(SdmcPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (++inspected > MaxSdmcEntriesToInspect)
                {
                    break;
                }

                if (entry.Length is 0 or > MaxUncompressedEntrySize)
                {
                    continue;
                }

                // Copy with a hard byte limit to guard against ZIP bombs where
                // entry.Length metadata is falsified.
                using var entryStream = new MemoryStream((int)entry.Length);
                var tooLarge = false;
                using (var src = entry.Open())
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalRead += read;
                        if (totalRead > MaxUncompressedEntrySize)
                        {
                            tooLarge = true;
                            break;
                        }

                        entryStream.Write(buffer, 0, read);
                    }
                }

                if (tooLarge)
                {
                    continue;
                }

                var entryBytes = entryStream.ToArray();

                if (!SaveUtil.TryGetSaveFile(entryBytes, out var sf, fileName))
                {
                    continue;
                }

                saveFile = sf;
                context = new ManicEmuSaveContext(zipBytes, entry.FullName);
                return true;
            }
        }
        catch (InvalidDataException)
        {
            // Malformed ZIP data — fall through.
        }
        catch (IOException)
        {
            // ZIP read error — fall through.
        }
        catch (NotSupportedException)
        {
            // Unsupported ZIP feature — fall through.
        }

        return false;
    }

    /// <summary>
    /// Rebuilds the original <c>.3ds.sav</c> ZIP archive, replacing the save file entry
    /// with <paramref name="newSaveBytes" />. Entries are written with
    /// <see cref="CompressionLevel.NoCompression" /> (store method) to match what Manic EMU's
    /// own <c>ShareManager.create3DSGameSave</c> produces. Deflate-method rebuilds have been
    /// observed to be rejected by Manic EMU on iOS even though the archive is structurally
    /// valid (see issue #751 follow-up: a 483 kB ORAS save deflated to 9 kB failed to import,
    /// while the same content store-method at ~483 kB worked). Timestamps are preserved.
    /// </summary>
    /// <param name="context">The context returned by <see cref="TryExtractSaveFromZip" />.</param>
    /// <param name="newSaveBytes">The edited save data to embed.</param>
    /// <returns>Raw bytes of the rebuilt <c>.3ds.sav</c> ZIP.</returns>
    public static byte[] RebuildZip(ManicEmuSaveContext context, byte[] newSaveBytes)
    {
        using var resultStream = new MemoryStream();

        using (var newArchive = new ZipArchive(resultStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var origStream = new MemoryStream(context.OriginalZipBytes);
            using var origArchive = new ZipArchive(origStream, ZipArchiveMode.Read);

            foreach (var entry in origArchive.Entries)
            {
                var newEntry = newArchive.CreateEntry(entry.FullName, CompressionLevel.NoCompression);
                newEntry.LastWriteTime = entry.LastWriteTime;

                using var dest = newEntry.Open();

                if (string.Equals(entry.FullName, context.SaveEntryPath, StringComparison.OrdinalIgnoreCase))
                {
                    dest.Write(newSaveBytes, 0, newSaveBytes.Length);
                }
                else
                {
                    // Guarded copy — same limit as TryExtractSaveFromZip to prevent OOM
                    // from a crafted ZIP whose non-save entries are unexpectedly large.
                    using var src = entry.Open();
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalRead += read;
                        if (totalRead > MaxUncompressedEntrySize)
                        {
                            throw new InvalidDataException(
                                $"Non-save entry '{entry.FullName}' exceeds the {MaxUncompressedEntrySize / (1024 * 1024)} MB size limit.");
                        }

                        dest.Write(buffer, 0, read);
                    }
                }
            }
        }

        return resultStream.ToArray();
    }

    /// <summary>
    /// Computes the export filename and compound extension for a Manic EMU save archive,
    /// preserving the original compound extension for round-trip compatibility.
    /// </summary>
    /// <param name="originalName">
    /// Original filename of the loaded archive (may be <see langword="null" />).
    /// </param>
    /// <returns>
    /// A tuple of the full export filename and its compound extension. Normally the canonical
    /// <c>.3ds.sav</c> suffix, e.g. <c>("AlphaSapphire.3ds.sav", ".3ds.sav")</c>. If the user
    /// uploaded a file whose name already ends in <c>.3ds.save</c> (manual rename or iOS-side
    /// extension mangling), we echo that back so the round-trip is bit-for-bit transparent.
    /// </returns>
    public static (string ExportName, string CompoundExtension) GetExportFileName(string? originalName)
    {
        const string savExt = ".3ds.sav";
        const string saveExt = ".3ds.save";

        if (originalName is null)
        {
            return ("save" + savExt, savExt);
        }

        // Check .3ds.save first — it's the more specific suffix (ends in .3ds.sav also matches it).
        if (originalName.EndsWith(saveExt, StringComparison.OrdinalIgnoreCase))
        {
            var stem = originalName[..^saveExt.Length];
            return ((stem.Length > 0
                ? stem
                : "save") + saveExt, saveExt);
        }

        if (originalName.EndsWith(savExt, StringComparison.OrdinalIgnoreCase))
        {
            var stem = originalName[..^savExt.Length];
            return ((stem.Length > 0
                ? stem
                : "save") + savExt, savExt);
        }

        // Unknown/no compound extension — strip the last extension and default to .3ds.sav.
        var fallbackStem = Path.GetFileNameWithoutExtension(originalName);
        return ((fallbackStem.Length > 0
            ? fallbackStem
            : "save") + savExt, savExt);
    }

    /// <summary>
    /// Metadata required to rebuild a <c>.3ds.sav</c> / <c>.3ds.save</c> ZIP after the save has been edited.
    /// </summary>
    public sealed record ManicEmuSaveContext(byte[] OriginalZipBytes, string SaveEntryPath);
}
