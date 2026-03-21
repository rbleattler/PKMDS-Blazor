using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace Pkmds.Core.Utilities;

/// <summary>
/// Helpers for importing and exporting 3DS save files in the Manic EMU
/// <c>.3ds.sav</c> ZIP format.
/// </summary>
/// <remarks>
/// Manic EMU exports 3DS saves as a ZIP archive named <c>GameTitle.3ds.sav</c>.
/// The ZIP contains the full <c>sdmc/</c> directory tree from Citra's virtual SD card,
/// e.g. <c>sdmc/Nintendo 3DS/…/title/00040000/00055d00/data/00000001/&lt;savefile&gt;</c>.
/// The actual PKHeX-compatible save bytes are stored as a single binary file entry
/// inside that directory structure.
///
/// To round-trip a save through PKMDS:
/// <list type="number">
///   <item>User exports <c>.3ds.sav</c> from Manic EMU.</item>
///   <item>PKMDS detects the ZIP, finds the save entry, and loads it.</item>
///   <item>User edits the save in PKMDS.</item>
///   <item>PKMDS rebuilds the ZIP with the edited save bytes and offers it for download
///         as <c>.3ds.sav</c> so Manic EMU can import it directly.</item>
/// </list>
/// </remarks>
public static class ManicEmuSaveHelper
{
    private const string SdmcPrefix = "sdmc/";

    // 3DS save files are at most a few MB; cap at 8 MB to guard against ZIP bombs.
    private const long MaxUncompressedEntrySize = 8 * 1024 * 1024;

    /// <summary>
    /// Metadata required to rebuild a <c>.3ds.sav</c> ZIP after the save has been edited.
    /// </summary>
    public sealed record ManicEmuSaveContext(byte[] OriginalZipBytes, string SaveEntryPath);

    /// <summary>
    /// Determines whether <paramref name="data"/> looks like a ZIP archive.
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
    ///   Original filename of the ZIP (forwarded to <see cref="SaveUtil.TryGetSaveFile"/> for
    ///   format detection); may be <see langword="null"/>.
    /// </param>
    /// <param name="saveFile">
    ///   The parsed <see cref="SaveFile"/> instance, ready to use directly.
    /// </param>
    /// <param name="context">
    ///   Metadata needed to rebuild the ZIP on export.  Pass this back to
    ///   <see cref="RebuildZip"/> once the user has finished editing.
    /// </param>
    /// <returns>
    ///   <see langword="true"/> if a recognisable save was found inside the ZIP;
    ///   <see langword="false"/> otherwise.
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
            return false;

        try
        {
            using var zipStream = new MemoryStream(zipBytes);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(SdmcPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.Length == 0 || entry.Length > MaxUncompressedEntrySize)
                    continue;

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
                    continue;

                var entryBytes = entryStream.ToArray();

                if (!SaveUtil.TryGetSaveFile(entryBytes, out var sf, fileName))
                    continue;

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
    /// with <paramref name="newSaveBytes"/>.  All other entries are re-compressed at
    /// <see cref="CompressionLevel.Optimal"/>; timestamps are preserved but other per-entry
    /// metadata (compression method, extra fields) may differ from the original.
    /// </summary>
    /// <param name="context">The context returned by <see cref="TryExtractSaveFromZip"/>.</param>
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
                var newEntry = newArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                newEntry.LastWriteTime = entry.LastWriteTime;

                using var dest = newEntry.Open();

                if (string.Equals(entry.FullName, context.SaveEntryPath, StringComparison.OrdinalIgnoreCase))
                {
                    dest.Write(newSaveBytes, 0, newSaveBytes.Length);
                }
                else
                {
                    using var src = entry.Open();
                    src.CopyTo(dest);
                }
            }
        }

        return resultStream.ToArray();
    }
}
