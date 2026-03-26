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

    private int boxesUnlocked;
    private byte[] boxFlags = [];
    private string[] boxNames = [];

    private int selectedBoxIndex;
    private bool showUnlocked;
    private bool supportsNameRead;
    private bool supportsNameWrite;
    private bool supportsWallpaper;
    private int[] wallpaperIds = [];
    private string[] wallpaperNameList = [];
    private string wallpaperSpriteUrl = string.Empty;

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

        supportsNameRead = sav is IBoxDetailNameRead;
        supportsNameWrite = sav is IBoxDetailName;
        supportsWallpaper = sav is IBoxDetailWallpaper;

        boxNames = new string[sav.BoxCount];
        if (sav is IBoxDetailNameRead r)
        {
            for (var i = 0; i < sav.BoxCount; i++)
            {
                boxNames[i] = r.GetBoxName(i);
            }
        }

        wallpaperIds = new int[sav.BoxCount];
        if (sav is IBoxDetailWallpaper w)
        {
            for (var i = 0; i < sav.BoxCount; i++)
            {
                wallpaperIds[i] = w.GetBoxWallpaper(i);
            }
        }

        wallpaperNameList = BuildWallpaperNames(sav);
        showUnlocked = sav.BoxesUnlocked > 0;
        boxesUnlocked = showUnlocked
            ? sav.BoxesUnlocked
            : 0;

        var flags = sav.BoxFlags;
        boxFlags = flags.Length > 0
            ? [.. flags]
            : [];

        selectedBoxIndex = 0;
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
        selectedBoxIndex = index;
        UpdateWallpaperPreview();
    }

    private void MoveBoxUp()
    {
        if (selectedBoxIndex <= 0 || AppState.SaveFile is not { } sav)
        {
            return;
        }

        var newIndex = selectedBoxIndex - 1;
        if (!sav.SwapBox(selectedBoxIndex, newIndex))
        {
            Snackbar.Add("Locked or team slots prevent moving this box.", Severity.Warning);
            return;
        }

        (boxNames[selectedBoxIndex], boxNames[newIndex]) =
            (boxNames[newIndex], boxNames[selectedBoxIndex]);
        (wallpaperIds[selectedBoxIndex], wallpaperIds[newIndex]) =
            (wallpaperIds[newIndex], wallpaperIds[selectedBoxIndex]);

        selectedBoxIndex = newIndex;
        RefreshService.RefreshBoxState();
        UpdateWallpaperPreview();
    }

    private void MoveBoxDown()
    {
        if (AppState.SaveFile is not { } sav || selectedBoxIndex >= sav.BoxCount - 1)
        {
            return;
        }

        var newIndex = selectedBoxIndex + 1;
        if (!sav.SwapBox(selectedBoxIndex, newIndex))
        {
            Snackbar.Add("Locked or team slots prevent moving this box.", Severity.Warning);
            return;
        }

        (boxNames[selectedBoxIndex], boxNames[newIndex]) =
            (boxNames[newIndex], boxNames[selectedBoxIndex]);
        (wallpaperIds[selectedBoxIndex], wallpaperIds[newIndex]) =
            (wallpaperIds[newIndex], wallpaperIds[selectedBoxIndex]);

        selectedBoxIndex = newIndex;
        RefreshService.RefreshBoxState();
        UpdateWallpaperPreview();
    }

    private void OnBoxNameChanged(string value)
    {
        if (AppState.SaveFile is not IBoxDetailName names)
        {
            return;
        }

        boxNames[selectedBoxIndex] = value;
        names.SetBoxName(selectedBoxIndex, value);
    }

    private void OnWallpaperChanged(int id)
    {
        if (AppState.SaveFile is not IBoxDetailWallpaper w)
        {
            return;
        }

        wallpaperIds[selectedBoxIndex] = id;
        w.SetBoxWallpaper(selectedBoxIndex, id);
        UpdateWallpaperPreview();
    }

    private void OnBoxesUnlockedChanged(int value)
    {
        if (AppState.SaveFile is not { } sav || !showUnlocked)
        {
            return;
        }

        boxesUnlocked = value;
        sav.BoxesUnlocked = value;
    }

    private void OnFlagChanged(int index, byte value)
    {
        if (AppState.SaveFile is not { } sav || index >= boxFlags.Length)
        {
            return;
        }

        boxFlags[index] = value;
        sav.BoxFlags = boxFlags;
    }

    private void UpdateWallpaperPreview()
    {
        if (AppState.SaveFile is { } sav && wallpaperIds.Length > selectedBoxIndex)
        {
            wallpaperSpriteUrl = ImageHelper.GetBoxWallpaperSpriteFileName(
                wallpaperIds[selectedBoxIndex], sav.Version);
        }
        else
        {
            wallpaperSpriteUrl = string.Empty;
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
