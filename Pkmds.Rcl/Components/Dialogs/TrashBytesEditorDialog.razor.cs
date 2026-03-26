namespace Pkmds.Rcl.Components.Dialogs;

public partial class TrashBytesEditorDialog
{
    private byte[] rawBytes = [];
    private int terminatorByteOffset;
    private int underlayLanguage = 2; // LanguageID.English
    private ushort underlaySpecies = 1;

    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [Parameter]
    [EditorRequired]
    public StringSource Field { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    private string FieldLabel => Field switch
    {
        StringSource.Nickname => "Nickname",
        StringSource.OriginalTrainer => "OT",
        StringSource.HandlingTrainer => "HT",
        _ => Field.ToString()
    };

    private string CurrentString
    {
        get
        {
            if (Pokemon is null || rawBytes.Length == 0)
            {
                return string.Empty;
            }

            return Pokemon.GetString(rawBytes);
        }
    }

    protected override void OnInitialized()
    {
        if (Pokemon is null)
        {
            return;
        }

        var trash = GetFieldTrash();
        if (trash.Length == 0)
        {
            return;
        }

        rawBytes = trash.ToArray();
        underlayLanguage = Pokemon.Language;
        underlaySpecies = Pokemon.Species;
        UpdateTerminatorOffset();
    }

    private Span<byte> GetFieldTrash() => Field switch
    {
        StringSource.Nickname => Pokemon!.NicknameTrash,
        StringSource.OriginalTrainer => Pokemon!.OriginalTrainerTrash,
        StringSource.HandlingTrainer => Pokemon!.HandlingTrainerTrash,
        _ => []
    };

    private void UpdateTerminatorOffset()
    {
        if (Pokemon is null || rawBytes.Length == 0)
        {
            terminatorByteOffset = 0;
            return;
        }

        var termCharIdx = Pokemon.GetStringTerminatorIndex(rawBytes);
        if (termCharIdx < 0)
        {
            // No terminator found — entire buffer is string content, no trash.
            terminatorByteOffset = rawBytes.Length;
            return;
        }

        terminatorByteOffset = (termCharIdx + 1) * Pokemon.GetBytesPerChar();
    }

    private void SetByte(int index, string hexValue)
    {
        if (index < 0 || index >= rawBytes.Length)
        {
            return;
        }

        if (!byte.TryParse(hexValue, NumberStyles.HexNumber, null, out var b))
        {
            return;
        }

        rawBytes[index] = b;
        UpdateTerminatorOffset();
    }

    private void ApplyUnderlayer()
    {
        if (Pokemon is null || rawBytes.Length == 0)
        {
            return;
        }

        var name = SpeciesName.GetSpeciesNameGeneration(underlaySpecies, underlayLanguage, Pokemon.Format);
        if (string.IsNullOrEmpty(name))
        {
            Snackbar.Add("No species name found for the selected species and language.", Severity.Warning);
            return;
        }

        Span<byte> encoded = stackalloc byte[rawBytes.Length];
        var written = Pokemon.SetString(encoded, name.AsSpan(), name.Length, StringConverterOption.None);

        if (written <= terminatorByteOffset)
        {
            Snackbar.Add("Underlayer is hidden by the current string — it would not appear as trash bytes.", Severity.Warning);
            return;
        }

        if (written > rawBytes.Length)
        {
            Snackbar.Add("Underlayer name is too long to fit in the trash region.", Severity.Warning);
            return;
        }

        for (var i = terminatorByteOffset; i < written; i++)
        {
            rawBytes[i] = encoded[i];
        }

        StateHasChanged();
        Snackbar.Add($"Underlayer applied: \"{name}\"", Severity.Info);
    }

    private void ClearTrash()
    {
        for (var i = terminatorByteOffset; i < rawBytes.Length; i++)
        {
            rawBytes[i] = 0;
        }

        StateHasChanged();
    }

    private void Save()
    {
        if (Pokemon is null)
        {
            MudDialog?.Close();
            return;
        }

        rawBytes.CopyTo(GetFieldTrash());
        Pokemon.RefreshChecksum();
        RefreshService.Refresh();
        Snackbar.Add($"{FieldLabel} trash bytes saved. Click Save to apply changes.", Severity.Success);
        MudDialog?.Close();
    }

    private void Cancel() => MudDialog?.Close();

    private Task<IEnumerable<ComboItem>> SearchPokemonNames(string? value, CancellationToken token) =>
        Task.FromResult(AppService.SearchPokemonNames(value ?? string.Empty));
}
