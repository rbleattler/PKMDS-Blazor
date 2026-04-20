using System.IO.Compression;

namespace Pkmds.Tests;

/// <summary>
/// Regression tests for <see cref="SaveFileLoader" /> covering the ordering bug that shipped
/// in production: PKHeX.Core's <c>ZipReader</c> runs inside <c>SaveUtil.TryGetSaveFile</c> and
/// would silently unwrap Manic EMU archives if we didn't check for them first (see issue #750).
/// </summary>
public class SaveFileLoaderTests
{
    private const string TestFilesPath = "../../../TestFiles";

    private const string ManicEmuSavePath = "sdmc/Nintendo 3DS/00000000000000000000000000000000/00000000000000000000000000000000/title/00040000/00175e00/data/00000001/main";

    /// <summary>
    /// Builds a ZIP matching what Manic EMU's <c>ShareManager.create3DSGameSave</c> produces
    /// on device: a single store-method (uncompressed) entry at the Citra sdmc/ save path.
    /// Matching the real compression method is load-bearing — a deflate rebuild of a Pokémon
    /// save compresses to &lt;2% of the original size (heavy 0xFF / 0x00 padding), and Manic EMU /
    /// iOS ZIPFoundation rejects the structurally-valid deflate archive on re-import.
    /// </summary>
    private static byte[] BuildManicEmuZip(byte[] saveBytes)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(ManicEmuSavePath, CompressionLevel.NoCompression);
            using var s = entry.Open();
            s.Write(saveBytes, 0, saveBytes.Length);
        }

        return ms.ToArray();
    }

    [Fact]
    public void TryLoad_ManicEmuZip_ReturnsContextAndSaveFile()
    {
        // This is the scenario that was broken pre-fix: before SaveFileLoader existed, the code
        // called SaveUtil.TryGetSaveFile directly, which accepts ZIPs via PKHeX's ZipReader and
        // returned a valid SaveFile but with no Manic EMU context — causing the UI to export raw
        // bytes that Manic EMU rejects on re-import.
        var rawSave = File.ReadAllBytes(Path.Combine(TestFilesPath, "moon.sav"));
        var zipBytes = BuildManicEmuZip(rawSave);

        var ok = SaveFileLoader.TryLoad(zipBytes, "moon.3ds.sav", out var saveFile, out var manicContext);

        ok.Should().BeTrue();
        saveFile.Should().NotBeNull();
        saveFile.Should().BeOfType<SAV7SM>();
        manicContext.Should().NotBeNull();
        manicContext!.SaveEntryPath.Should().Be(ManicEmuSavePath);
    }

    [Fact]
    public void TryLoad_RawSave_ReturnsNullContext()
    {
        var rawSave = File.ReadAllBytes(Path.Combine(TestFilesPath, "moon.sav"));

        var ok = SaveFileLoader.TryLoad(rawSave, "moon.sav", out var saveFile, out var manicContext);

        ok.Should().BeTrue();
        saveFile.Should().NotBeNull();
        manicContext.Should().BeNull();
    }

    [Fact]
    public void TryLoad_ZipWithoutSdmcEntries_FallsThroughToPkhexZipReader()
    {
        // A plain ZIP whose inner file is named `main` — recognised by PKHeX's ZipReader but
        // not by our Manic EMU helper (no sdmc/ prefix). TryLoad should still return true, with
        // a null manic context, so non-Manic ZIPs (e.g. PKHeX-produced ones) still work.
        var rawSave = File.ReadAllBytes(Path.Combine(TestFilesPath, "moon.sav"));

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("main", CompressionLevel.Optimal);
            using var s = entry.Open();
            s.Write(rawSave, 0, rawSave.Length);
        }

        var ok = SaveFileLoader.TryLoad(ms.ToArray(), "moon.zip", out var saveFile, out var manicContext);

        ok.Should().BeTrue();
        saveFile.Should().NotBeNull();
        manicContext.Should().BeNull();
    }

    [Fact]
    public void TryLoad_NonZipNonSaveGarbage_ReturnsFalse()
    {
        var garbage = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };

        var ok = SaveFileLoader.TryLoad(garbage, "garbage.bin", out var saveFile, out var manicContext);

        ok.Should().BeFalse();
        saveFile.Should().BeNull();
        manicContext.Should().BeNull();
    }

    [Fact]
    public void TryLoad_ManicEmuZip_RoundTripsThroughRebuild()
    {
        // End-to-end: ZIP → load → write edited bytes → rebuild ZIP via returned context →
        // load again → inner save still parses and has the expected size. This is the flow
        // the MainLayout upload → export path now follows.
        var rawSave = File.ReadAllBytes(Path.Combine(TestFilesPath, "moon.sav"));
        var zipBytes = BuildManicEmuZip(rawSave);

        SaveFileLoader.TryLoad(zipBytes, "moon.3ds.sav", out var saveFile, out var ctx).Should().BeTrue();

        var exportedBytes = saveFile!.Write().ToArray();
        var rebuilt = ManicEmuSaveHelper.RebuildZip(ctx!, exportedBytes);

        SaveFileLoader.TryLoad(rebuilt, "moon.3ds.sav", out var reloaded, out var reloadedCtx).Should().BeTrue();
        reloaded.Should().NotBeNull();
        reloaded.Should().BeOfType<SAV7SM>();
        reloaded!.Write().Length.Should().Be(rawSave.Length);
        reloadedCtx.Should().NotBeNull();
        reloadedCtx!.SaveEntryPath.Should().Be(ManicEmuSavePath);
    }

    [Fact]
    public void RebuildZip_SetsUtf8PathEncodingFlagOnAllEntries()
    {
        // Regression guard for PR #751 follow-up: .NET's ZipArchive only sets general-purpose
        // bit 11 (UTF-8 path encoding) when a filename contains non-ASCII characters, but
        // ZIPFoundation on iOS sets it unconditionally when writing and has been observed
        // rejecting archives that omit it — even for pure-ASCII paths like Manic EMU's sdmc/
        // tree. Our rebuild has to match ZIPFoundation's behaviour.
        var rawSave = File.ReadAllBytes(Path.Combine(TestFilesPath, "moon.sav"));
        var original = BuildManicEmuZip(rawSave);

        SaveFileLoader.TryLoad(original, "moon.3ds.sav", out var saveFile, out var ctx).Should().BeTrue();
        var rebuilt = ManicEmuSaveHelper.RebuildZip(ctx!, saveFile!.Write().ToArray());

        // Walk the central directory and confirm bit 11 is set on every CDFH.
        var eocdOffset = FindEndOfCentralDirectory(rebuilt);
        eocdOffset.Should().BeGreaterThanOrEqualTo(0);
        var cdOffset = BitConverter.ToInt32(rebuilt, eocdOffset + 16);
        var entryCount = BitConverter.ToUInt16(rebuilt, eocdOffset + 10);

        var pos = cdOffset;
        for (var i = 0; i < entryCount; i++)
        {
            var flags = BitConverter.ToUInt16(rebuilt, pos + 8);
            (flags & 0x0800).Should().NotBe(0, $"CDFH entry {i} must have UTF-8 path-encoding flag (bit 11) set");
            var nameLen = BitConverter.ToUInt16(rebuilt, pos + 28);
            var extraLen = BitConverter.ToUInt16(rebuilt, pos + 30);
            var commentLen = BitConverter.ToUInt16(rebuilt, pos + 32);
            pos += 46 + nameLen + extraLen + commentLen;
        }

        // Also walk local file headers — bit 11 has to match between LFH and CDFH per spec.
        pos = 0;
        for (var i = 0; i < entryCount && pos < cdOffset; i++)
        {
            BitConverter.ToInt32(rebuilt, pos).Should().Be(0x04034B50, $"LFH {i} signature");
            var flags = BitConverter.ToUInt16(rebuilt, pos + 6);
            (flags & 0x0800).Should().NotBe(0, $"LFH entry {i} must have UTF-8 path-encoding flag (bit 11) set");
            var nameLen = BitConverter.ToUInt16(rebuilt, pos + 26);
            var extraLen = BitConverter.ToUInt16(rebuilt, pos + 28);
            var compSize = BitConverter.ToUInt32(rebuilt, pos + 18);
            pos += 30 + nameLen + extraLen + (int)compSize;
        }
    }

    private static int FindEndOfCentralDirectory(byte[] zipBytes)
    {
        const int eocdSignature = 0x06054B50;
        const int eocdMinSize = 22;
        const int eocdMaxCommentLen = 65535;
        var searchStart = Math.Max(0, zipBytes.Length - eocdMinSize - eocdMaxCommentLen);
        for (var i = zipBytes.Length - eocdMinSize; i >= searchStart; i--)
        {
            if (BitConverter.ToInt32(zipBytes, i) == eocdSignature)
            {
                return i;
            }
        }
        return -1;
    }

    [Fact]
    public void RebuildZip_PreservesStoreCompressionMatchingManicEmuOutput()
    {
        // Regression guard for the deflate-shrinkage bug observed on PR #751: a 483 kB ORAS
        // save compressed to a 9 kB deflate entry and Manic EMU rejected the re-import.
        // The rebuild must use store (method 0) to match what Manic EMU itself produces, so the
        // rebuilt archive stays in the same size ballpark as the original upload.
        var rawSave = File.ReadAllBytes(Path.Combine(TestFilesPath, "moon.sav"));
        var original = BuildManicEmuZip(rawSave);

        SaveFileLoader.TryLoad(original, "moon.3ds.sav", out var saveFile, out var ctx).Should().BeTrue();
        var rebuilt = ManicEmuSaveHelper.RebuildZip(ctx!, saveFile!.Write().ToArray());

        // Store-method rebuild must be close to the input size (allow modest drift from
        // timestamp/header differences). A deflate-compressed rebuild would be a fraction
        // of the input size due to the save's heavy padding.
        rebuilt.Length.Should().BeGreaterThan((int)(original.Length * 0.95));
        rebuilt.Length.Should().BeLessThan((int)(original.Length * 1.05));

        // And confirm every entry in the rebuilt archive is store-method (compression method 0).
        using var ms = new MemoryStream(rebuilt);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            // CompressedLength == Length is the observable signal that no compression was applied.
            entry.CompressedLength.Should().Be(entry.Length,
                $"entry '{entry.FullName}' should be store-method (uncompressed) to match Manic EMU's own output");
        }
    }
}
