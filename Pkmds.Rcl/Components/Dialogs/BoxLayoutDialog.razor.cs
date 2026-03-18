namespace Pkmds.Rcl.Components.Dialogs;

public partial class BoxLayoutDialog : BasePkmdsComponent
{
    // Gen 3 wallpaper names differ from Gen 4+ for indices 12-15.
    // PKHeX's wallpapernames text file uses Gen 4 names (POKÉ CENTER, MACHINE, CHECKS,
    // SIMPLE) for those slots, but Gen 3 has Polka-Dot, PokéCenter, Machine, Simple.
    private static readonly string[] Gen3WallpaperNames =
    [
        "Forest", "City", "Desert", "Savanna",
        "Crag", "Volcano", "Snow", "Cave",
        "Beach", "Seafloor", "River", "Sky",
        "Polka-Dot", "PokéCenter", "Machine", "Simple"
    ];

    private int _boxesUnlocked;
    private byte[] _boxFlags = [];
    private string[] _boxNames = [];

    private int _selectedBoxIndex;
    private bool _showUnlocked;
    private bool _supportsNameRead;
    private bool _supportsNameWrite;
    private bool _supportsWallpaper;
    private int[] _wallpaperIds = [];
    private string[] _wallpaperNameList = [];
    private string _wallpaperSpriteUrl = string.Empty;

    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        LoadFromSave();
    }

    private void LoadFromSave()
    {
        if (AppState.SaveFile is not { } sav)
        {
            return;
        }

        _supportsNameRead = sav is IBoxDetailNameRead;
        _supportsNameWrite = sav is IBoxDetailName;
        _supportsWallpaper = sav is IBoxDetailWallpaper;

        _boxNames = new string[sav.BoxCount];
        if (sav is IBoxDetailNameRead r)
        {
            for (var i = 0; i < sav.BoxCount; i++)
            {
                _boxNames[i] = r.GetBoxName(i);
            }
        }

        _wallpaperIds = new int[sav.BoxCount];
        if (sav is IBoxDetailWallpaper w)
        {
            for (var i = 0; i < sav.BoxCount; i++)
            {
                _wallpaperIds[i] = w.GetBoxWallpaper(i);
            }
        }

        _wallpaperNameList = BuildWallpaperNames(sav);
        _showUnlocked = sav.BoxesUnlocked > 0;
        _boxesUnlocked = _showUnlocked
            ? sav.BoxesUnlocked
            : 0;

        var flags = sav.BoxFlags;
        _boxFlags = flags.Length > 0
            ? [.. flags]
            : [];

        _selectedBoxIndex = 0;
        UpdateWallpaperPreview();
    }

    private static string[] BuildWallpaperNames(SaveFile sav)
    {
        if (sav is not IBoxDetailWallpaper)
        {
            return [];
        }

        if (sav.Generation == 3)
        {
            return Gen3WallpaperNames;
        }

        var names = GameInfo.Strings.wallpapernames;
        var namedCount = sav.Generation switch
        {
            4 or 5 or 6 => 24,
            7 => 16,
            8 when sav is SAV8BS => 32,
            _ => 0
        };

        if (namedCount > 0 && names.Length >= namedCount)
        {
            var result = new string[namedCount];
            Array.Copy(names, result, namedCount);
            return result;
        }

        var placeholderCount = sav.Generation switch
        {
            8 => 19,
            9 => 20,
            _ => 0
        };

        if (placeholderCount <= 0)
        {
            return [];
        }

        {
            var result = new string[placeholderCount];
            for (var i = 0; i < placeholderCount; i++)
            {
                result[i] = $"Wallpaper {i + 1}";
            }

            return result;
        }
    }

    private void SelectBox(int index)
    {
        _selectedBoxIndex = index;
        UpdateWallpaperPreview();
    }

    private void MoveBoxUp()
    {
        if (_selectedBoxIndex <= 0 || AppState.SaveFile is not { } sav)
        {
            return;
        }

        var newIndex = _selectedBoxIndex - 1;
        if (!sav.SwapBox(_selectedBoxIndex, newIndex))
        {
            Snackbar.Add("Locked or team slots prevent moving this box.", Severity.Warning);
            return;
        }

        (_boxNames[_selectedBoxIndex], _boxNames[newIndex]) =
            (_boxNames[newIndex], _boxNames[_selectedBoxIndex]);
        (_wallpaperIds[_selectedBoxIndex], _wallpaperIds[newIndex]) =
            (_wallpaperIds[newIndex], _wallpaperIds[_selectedBoxIndex]);

        _selectedBoxIndex = newIndex;
        RefreshService.RefreshBoxState();
        UpdateWallpaperPreview();
    }

    private void MoveBoxDown()
    {
        if (AppState.SaveFile is not { } sav || _selectedBoxIndex >= sav.BoxCount - 1)
        {
            return;
        }

        var newIndex = _selectedBoxIndex + 1;
        if (!sav.SwapBox(_selectedBoxIndex, newIndex))
        {
            Snackbar.Add("Locked or team slots prevent moving this box.", Severity.Warning);
            return;
        }

        (_boxNames[_selectedBoxIndex], _boxNames[newIndex]) =
            (_boxNames[newIndex], _boxNames[_selectedBoxIndex]);
        (_wallpaperIds[_selectedBoxIndex], _wallpaperIds[newIndex]) =
            (_wallpaperIds[newIndex], _wallpaperIds[_selectedBoxIndex]);

        _selectedBoxIndex = newIndex;
        RefreshService.RefreshBoxState();
        UpdateWallpaperPreview();
    }

    private void OnBoxNameChanged(string value)
    {
        if (AppState.SaveFile is not IBoxDetailName names)
        {
            return;
        }

        _boxNames[_selectedBoxIndex] = value;
        names.SetBoxName(_selectedBoxIndex, value);
    }

    private void OnWallpaperChanged(int id)
    {
        if (AppState.SaveFile is not IBoxDetailWallpaper w)
        {
            return;
        }

        _wallpaperIds[_selectedBoxIndex] = id;
        w.SetBoxWallpaper(_selectedBoxIndex, id);
        UpdateWallpaperPreview();
    }

    private void OnBoxesUnlockedChanged(int value)
    {
        if (AppState.SaveFile is not { } sav || !_showUnlocked)
        {
            return;
        }

        _boxesUnlocked = value;
        sav.BoxesUnlocked = value;
    }

    private void OnFlagChanged(int index, byte value)
    {
        if (AppState.SaveFile is not { } sav || index >= _boxFlags.Length)
        {
            return;
        }

        _boxFlags[index] = value;
        sav.BoxFlags = _boxFlags;
    }

    private void UpdateWallpaperPreview()
    {
        if (AppState.SaveFile is { } sav && _wallpaperIds.Length > _selectedBoxIndex)
        {
            _wallpaperSpriteUrl = ImageHelper.GetBoxWallpaperSpriteFileName(
                _wallpaperIds[_selectedBoxIndex], sav.Version);
        }
        else
        {
            _wallpaperSpriteUrl = string.Empty;
        }
    }

    private int GetBoxNameMaxLength() => AppState.SaveFile?.Generation switch
    {
        6 or 7 => 14,
        >= 8 => 16,
        _ => 8
    };

    private int GetBoxPokemonCount(int boxId)
    {
        if (AppState.SaveFile is not { } sav)
        {
            return 0;
        }

        var count = 0;
        for (var slot = 0; slot < sav.BoxSlotCount; slot++)
        {
            if (sav.GetBoxSlotAtIndex(boxId, slot).Species != 0)
            {
                count++;
            }
        }

        return count;
    }

    private void Close()
    {
        RefreshService.RefreshBoxState();
        MudDialog?.Close();
    }
}
