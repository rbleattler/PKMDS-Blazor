using Pkmds.Core.Feebas;

namespace Pkmds.Rcl.Components.MainTabPages;

public partial class FeebasLocatorTab
{
    private const string Route119Url = "_content/Pkmds.Rcl/maps/feebas/route119.png";
    private const string MtCoronetUrl = "_content/Pkmds.Rcl/maps/feebas/mtcoronet.png";

    private const int Route119Width = 641;
    private const int Route119Height = 1553;
    private const int MtCoronetWidth = 512;
    private const int MtCoronetHeight = 518;

    private SaveFile? saveFile;
    private ushort[]? tiles;
    private uint seed;
    private string seedHex = string.Empty;
    private string seedFormat = "X4";
    private string locationLabel = string.Empty;
    private string mapUrl = string.Empty;
    private int mapWidth;
    private int mapHeight;
    private List<TileMarker> markers = [];
    private bool fitToView = true;

    private void ToggleFitToView() => fitToView = !fitToView;

    private const string ContainerStyle =
        "border: 1px solid var(--mud-palette-lines-default); " +
        "border-radius: 4px; " +
        "background: var(--mud-palette-background-grey); " +
        "padding: 4px; " +
        "text-align: center;";

    private string WrapperStyle => fitToView
        ? "position: relative; display: inline-block;"
        : $"position: relative; display: inline-block; width: {mapWidth}px; height: {mapHeight}px;";

    private string ImageStyle => fitToView
        ? "display: block; max-width: 100%; max-height: 50vh; width: auto; height: auto; image-rendering: pixelated;"
        : "display: block; width: 100%; height: 100%; image-rendering: pixelated;";

    private string MarkerStyle(TileMarker marker)
    {
        var leftPct = (double)marker.X / mapWidth * 100;
        var topPct = (double)marker.Y / mapHeight * 100;
        var widthPct = (double)marker.Width / mapWidth * 100;
        var heightPct = (double)marker.Height / mapHeight * 100;
        return string.Create(CultureInfo.InvariantCulture,
            $"position: absolute; left: {leftPct:F4}%; top: {topPct:F4}%; width: {widthPct:F4}%; height: {heightPct:F4}%; background: rgba(244, 67, 54, 0.7); border: 1px solid rgba(244, 67, 54, 1); pointer-events: none; box-sizing: border-box;");
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        LoadFromAppState();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        LoadFromAppState();
    }

    private void LoadFromAppState()
    {
        saveFile = AppState?.SaveFile;
        if (saveFile is null || !FeebasSeedAccessor.IsSupported(saveFile))
        {
            tiles = null;
            markers = [];
            return;
        }

        var seedValue = FeebasSeedAccessor.TryReadSeed(saveFile);
        if (seedValue is null)
        {
            tiles = null;
            markers = [];
            return;
        }

        seed = seedValue.Value;
        tiles = FeebasSeedAccessor.GetTiles(saveFile);

        if (saveFile.Generation == 3)
        {
            seedFormat = "X4";
            locationLabel = "Route 119 — Hoenn";
            mapUrl = Route119Url;
            mapWidth = Route119Width;
            mapHeight = Route119Height;
        }
        else
        {
            seedFormat = "X8";
            locationLabel = "Mt. Coronet B1F";
            mapUrl = MtCoronetUrl;
            mapWidth = MtCoronetWidth;
            mapHeight = MtCoronetHeight;
        }

        seedHex = seed.ToString(seedFormat);
        markers = BuildMarkers(saveFile, tiles);
    }

    private static List<TileMarker> BuildMarkers(SaveFile sav, ushort[]? tiles)
    {
        var result = new List<TileMarker>();
        if (tiles is null)
        {
            return result;
        }

        if (sav.Generation == 3)
        {
            foreach (var tile in tiles)
            {
                if (Feebas3.IsUnderBridge(tile))
                {
                    var underBridgeRows = FeebasTileCoordinates.Route119TilesUnderBridge.GetLength(0);
                    for (var i = 0; i < underBridgeRows; i++)
                    {
                        result.Add(new TileMarker(
                            FeebasTileCoordinates.Route119TilesUnderBridge[i, 0],
                            FeebasTileCoordinates.Route119TilesUnderBridge[i, 1],
                            FeebasTileCoordinates.Gen3MarkerSize,
                            FeebasTileCoordinates.Gen3MarkerSize));
                    }
                    continue;
                }

                if (Feebas3.IsAccessible(tile))
                {
                    result.Add(new TileMarker(
                        FeebasTileCoordinates.Route119Tiles[tile - 4, 0],
                        FeebasTileCoordinates.Route119Tiles[tile - 4, 1],
                        FeebasTileCoordinates.Gen3MarkerSize,
                        FeebasTileCoordinates.Gen3MarkerSize));
                }
            }
        }
        else
        {
            foreach (var tile in tiles)
            {
                if (tile >= FeebasTileCoordinates.MtCoronetTiles.GetLength(0))
                {
                    continue;
                }

                result.Add(new TileMarker(
                    FeebasTileCoordinates.MtCoronetTiles[tile, 0],
                    FeebasTileCoordinates.MtCoronetTiles[tile, 1],
                    FeebasTileCoordinates.Gen4MarkerWidth,
                    FeebasTileCoordinates.Gen4MarkerHeight));
            }
        }

        return result;
    }

    private readonly record struct TileMarker(int X, int Y, int Width, int Height);
}
