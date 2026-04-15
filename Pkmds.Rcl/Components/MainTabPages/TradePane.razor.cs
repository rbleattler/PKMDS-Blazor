namespace Pkmds.Rcl.Components.MainTabPages;

public partial class TradePane : RefreshAwareComponent
{
    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public SaveFile? SaveFile { get; set; }

    [Parameter]
    public int? SelectedBox { get; set; }

    [Parameter]
    public EventCallback<int?> SelectedBoxChanged { get; set; }

    [Parameter]
    public int? SelectedBoxSlot { get; set; }

    [Parameter]
    public EventCallback<int?> SelectedBoxSlotChanged { get; set; }

    [Parameter]
    public int? SelectedPartySlot { get; set; }

    [Parameter]
    public EventCallback<int?> SelectedPartySlotChanged { get; set; }

    private async Task SelectBox(int boxNum)
    {
        SelectedBox = boxNum;
        SelectedBoxSlot = null;
        await SelectedBoxChanged.InvokeAsync(boxNum);
        await SelectedBoxSlotChanged.InvokeAsync(null);
    }

    private async Task SelectBoxSlot(int boxNum, int slotNum)
    {
        SelectedBox = boxNum;
        SelectedBoxSlot = slotNum;
        SelectedPartySlot = null;
        await SelectedBoxChanged.InvokeAsync(boxNum);
        await SelectedBoxSlotChanged.InvokeAsync(slotNum);
        await SelectedPartySlotChanged.InvokeAsync(null);
    }

    private async Task SelectParty(int slotNum)
    {
        SelectedPartySlot = slotNum;
        SelectedBoxSlot = null;
        await SelectedPartySlotChanged.InvokeAsync(slotNum);
        await SelectedBoxSlotChanged.InvokeAsync(null);
    }

    private static string BoxGridClass(SaveFile saveFile) =>
        saveFile.BoxSlotCount == 20
            ? "grid grid-cols-4 grid-rows-5 gap-1 w-full max-w-80 mx-auto"
            : "grid grid-cols-6 gap-1 w-full aspect-[6/5] mx-auto";

    // SaveFile.Language is -1 for Gen 1/2 (no language field); fall back to the current
    // app language so name lookups still succeed for those saves.
    private static GameStrings GetStringsForSave(SaveFile saveFile) =>
        GameInfo.GetStrings(saveFile.Language >= 0
            ? GameLanguage.LanguageCode(saveFile.Language)
            : GameInfo.CurrentLanguage);

    internal PKM? SelectedPokemon
    {
        get
        {
            if (SaveFile is not { } sav)
            {
                return null;
            }

            if (SelectedPartySlot is { } partySlot && partySlot < sav.PartyCount)
            {
                return sav.GetPartySlotAtIndex(partySlot);
            }

            if (SelectedBox is { } boxNum && SelectedBoxSlot is { } boxSlot)
            {
                return sav.GetBoxSlotAtIndex(boxNum, boxSlot);
            }

            return null;
        }
    }
}
