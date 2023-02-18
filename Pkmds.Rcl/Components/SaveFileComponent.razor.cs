namespace Pkmds.Rcl.Components;

public partial class SaveFileComponent : IDisposable
{
    private string FileDisplayName { get; set; } = string.Empty;

    private const long MaxFileSize = 4_000_000L; // bytes

    private IBrowserFile? browserFile;

    protected override void OnInitialized() => AppState.OnAppStateChanged += StateHasChanged;

    private void HandleFile(InputFileChangeEventArgs e) => browserFile = e.File;

    private async Task LoadSaveFileAsync()
    {
        if (browserFile is null)
        {
            return;
        }

        AppState.SaveFile = null;

        await using var fileStream = browserFile.OpenReadStream(MaxFileSize);
        using var memoryStream = new MemoryStream();
        await fileStream.CopyToAsync(memoryStream);
        var data = memoryStream.ToArray();
        AppState.SaveFile = SaveUtil.GetVariantSAV(data);
        if (AppState.SaveFile is null)
        {
            return;
        }

        FileDisplayName = $"{AppState.SaveFile.OT} ({AppState.SaveFile.DisplayTID}, {AppState.SaveFile.Version})";
    }

    public void Dispose() => AppState.OnAppStateChanged -= StateHasChanged;

    private void NavigateRight()
    {
        if (AppState.SaveFile is null)
        {
            return;
        }

        if (AppState.SaveFile.CurrentBox == AppState.SaveFile.BoxCount - 1)
        {
            AppState.SaveFile.CurrentBox = 0;
        }
        else
        {
            AppState.SaveFile.CurrentBox++;
        }

        AppState.SelectedBoxSlot = null;
        AppState.SelectedPokemon = null;
    }

    private void NavigateLeft()
    {
        if (AppState.SaveFile is null)
        {
            return;
        }

        if (AppState.SaveFile.CurrentBox == 0)
        {
            AppState.SaveFile.CurrentBox = AppState.SaveFile.BoxCount - 1;
        }
        else
        {
            AppState.SaveFile.CurrentBox--;
        }

        AppState.SelectedBoxSlot = null;
        AppState.SelectedPokemon = null;
    }
}