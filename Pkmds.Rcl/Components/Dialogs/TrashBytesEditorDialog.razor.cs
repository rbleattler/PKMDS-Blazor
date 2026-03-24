namespace Pkmds.Rcl.Components.Dialogs;

public partial class TrashBytesEditorDialog
{
    [Parameter]
    [EditorRequired]
    public PKM? Pokemon { get; set; }

    [Parameter]
    [EditorRequired]
    public StringSource Field { get; set; }

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    private byte[] _rawBytes = [];
    private int _terminatorByteOffset;
    private ushort _underlaySpecies = 1;
    private int _underlayLanguage = 2; // LanguageID.English

    private string FieldLabel => Field switch
    {
        StringSource.Nickname => "Nickname",
        StringSource.OriginalTrainer => "OT",
        StringSource.HandlingTrainer => "HT",
        _ => Field.ToString(),
    };

    private string CurrentString
    {
        get
        {
            if (Pokemon is null || _rawBytes.Length == 0)
                return string.Empty;
            return Pokemon.GetString(_rawBytes);
        }
    }

    protected override void OnInitialized()
    {
        if (Pokemon is null)
            return;

        var trash = GetFieldTrash();
        if (trash.Length == 0)
            return;

        _rawBytes = trash.ToArray();
        _underlayLanguage = Pokemon.Language;
        _underlaySpecies = Pokemon.Species;
        UpdateTerminatorOffset();
    }

    private Span<byte> GetFieldTrash() => Field switch
    {
        StringSource.Nickname => Pokemon!.NicknameTrash,
        StringSource.OriginalTrainer => Pokemon!.OriginalTrainerTrash,
        StringSource.HandlingTrainer => Pokemon!.HandlingTrainerTrash,
        _ => [],
    };

    private void UpdateTerminatorOffset()
    {
        if (Pokemon is null || _rawBytes.Length == 0)
        {
            _terminatorByteOffset = 0;
            return;
        }

        var termCharIdx = Pokemon.GetStringTerminatorIndex(_rawBytes);
        if (termCharIdx < 0)
        {
            // No terminator found — entire buffer is string content, no trash.
            _terminatorByteOffset = _rawBytes.Length;
            return;
        }

        _terminatorByteOffset = (termCharIdx + 1) * Pokemon.GetBytesPerChar();
    }

    private void SetByte(int index, string hexValue)
    {
        if (index < 0 || index >= _rawBytes.Length)
            return;
        if (byte.TryParse(hexValue, System.Globalization.NumberStyles.HexNumber, null, out var b))
            _rawBytes[index] = b;
    }

    private void ApplyUnderlayer()
    {
        if (Pokemon is null || _rawBytes.Length == 0)
            return;

        var name = SpeciesName.GetSpeciesNameGeneration(_underlaySpecies, _underlayLanguage, Pokemon.Format);
        if (string.IsNullOrEmpty(name))
        {
            Snackbar.Add("No species name found for the selected species and language.", Severity.Warning);
            return;
        }

        Span<byte> encoded = stackalloc byte[_rawBytes.Length];
        var written = Pokemon.SetString(encoded, name.AsSpan(), name.Length, StringConverterOption.None);

        if (written <= _terminatorByteOffset)
        {
            Snackbar.Add("Underlayer is hidden by the current string — it would not appear as trash bytes.", Severity.Warning);
            return;
        }

        if (written > _rawBytes.Length)
        {
            Snackbar.Add("Underlayer name is too long to fit in the trash region.", Severity.Warning);
            return;
        }

        for (var i = _terminatorByteOffset; i < written; i++)
            _rawBytes[i] = encoded[i];

        StateHasChanged();
        Snackbar.Add($"Underlayer applied: \"{name}\"", Severity.Info);
    }

    private void ClearTrash()
    {
        for (var i = _terminatorByteOffset; i < _rawBytes.Length; i++)
            _rawBytes[i] = 0;

        StateHasChanged();
    }

    private void Save()
    {
        if (Pokemon is null)
        {
            MudDialog?.Close();
            return;
        }

        _rawBytes.CopyTo(GetFieldTrash());
        Pokemon.RefreshChecksum();
        RefreshService.Refresh();
        Snackbar.Add($"{FieldLabel} trash bytes saved. Click Save to apply changes.", Severity.Success);
        MudDialog?.Close();
    }

    private void Cancel() => MudDialog?.Close();

    private Task<IEnumerable<ComboItem>> SearchPokemonNames(string? value, CancellationToken token) =>
        Task.FromResult(AppService.SearchPokemonNames(value ?? string.Empty));
}
