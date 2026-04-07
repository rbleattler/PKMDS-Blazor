namespace Pkmds.Rcl.Components.Dialogs;

public partial class SaveFileInfoDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public SaveFile? SaveFile { get; set; }

    private string? RevisionString =>
        SaveFile is ISaveFileRevision rev && !string.IsNullOrEmpty(rev.SaveRevisionString)
            ? rev.SaveRevisionString
            : null;

    // SaveFile.Data.Length is 0 for SCBlock-based saves (Gen 8-9) because their
    // base constructor receives Memory<byte>.Empty — data lives in blocks, not a
    // contiguous buffer.  Fall back to Write().Length which always produces the
    // correct serialised size.
    private int FileSize =>
        SaveFile is null ? 0
        : SaveFile.Data.Length > 0 ? SaveFile.Data.Length
        : SaveFile.Write().Length;

    private void Close() => MudDialog.Close(DialogResult.Cancel());

    private string GetEncryptionDescription() => SaveFile?.Generation switch
    {
        1 or 2 => "None",
        3 => "Block shuffle",
        4 or 5 => "Block shuffle + checksum",
        6 or 7 => "PKM slot encryption",
        8 or 9 => "SCBlock encryption (SwishCrypto)",
        _ => "Unknown"
    };

    private static string FormatSize(int bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB ({bytes:N0} bytes)",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB ({bytes:N0} bytes)"
    };
}
