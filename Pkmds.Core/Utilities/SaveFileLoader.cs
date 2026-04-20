using System.Diagnostics.CodeAnalysis;

namespace Pkmds.Core.Utilities;

/// <summary>
/// Unified entry point for loading user-supplied save data. Detects Manic EMU
/// <c>.3ds.sav</c> ZIP archives before delegating to <see cref="SaveUtil.TryGetSaveFile(Memory{byte}, out SaveFile?, string?)" />
/// for raw saves.
/// </summary>
/// <remarks>
/// The ordering here is load-bearing. PKHeX.Core's built-in <see cref="ZipReader" />
/// (registered in <see cref="SaveUtil.CustomSaveReaders" />) recognises any ZIP with a
/// <c>main</c> or <c>SaveData.bin</c> entry and unwraps it invisibly — including Manic EMU
/// archives. If we call <see cref="SaveUtil.TryGetSaveFile(Memory{byte}, out SaveFile?, string?)" />
/// first on a Manic EMU ZIP we get back a valid <see cref="SaveFile" />, but the surrounding
/// <see cref="ManicEmuSaveHelper.ManicEmuSaveContext" /> (needed to rebuild the archive on export)
/// is silently lost, and the user ends up exporting raw bytes that Manic EMU rejects
/// on re-import (issue #750).
/// </remarks>
public static class SaveFileLoader
{
    /// <summary>
    /// Attempts to load <paramref name="data" /> as either a Manic EMU ZIP archive or a raw save.
    /// </summary>
    /// <param name="data">Raw upload bytes.</param>
    /// <param name="fileName">Original filename of the upload (may be <see langword="null" />).</param>
    /// <param name="saveFile">
    /// The parsed <see cref="SaveFile" /> instance on success.
    /// </param>
    /// <param name="manicEmuContext">
    /// Non-<see langword="null" /> only when the upload was a Manic EMU ZIP. Pass this back to
    /// <see cref="ManicEmuSaveHelper.RebuildZip" /> on export to round-trip correctly.
    /// </param>
    /// <returns><see langword="true" /> on successful load; <see langword="false" /> otherwise.</returns>
    public static bool TryLoad(
        byte[] data,
        string? fileName,
        [NotNullWhen(true)] out SaveFile? saveFile,
        out ManicEmuSaveHelper.ManicEmuSaveContext? manicEmuContext)
    {
        manicEmuContext = null;

        // Manic EMU detection must run before SaveUtil.TryGetSaveFile because PKHeX's ZipReader
        // would otherwise unwrap the archive invisibly, stripping the context we need for re-export.
        if (ManicEmuSaveHelper.IsZip(data) &&
            ManicEmuSaveHelper.TryExtractSaveFromZip(data, fileName, out saveFile, out var ctx))
        {
            manicEmuContext = ctx;
            return true;
        }

        return SaveUtil.TryGetSaveFile(data, out saveFile, fileName);
    }
}
