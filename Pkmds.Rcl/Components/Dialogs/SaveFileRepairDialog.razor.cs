namespace Pkmds.Rcl.Components.Dialogs;

public partial class SaveFileRepairDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public SaveFile? SaveFile { get; set; }

    /// <summary>
    /// Gen 8-9 SCBlock saves have no checksums — SetChecksums() is a no-op.
    /// </summary>
    private bool HasChecksums => SaveFile is not null && SaveFile.Generation < 8;

    private void OnFixChecksums()
    {
        if (SaveFile is null)
        {
            return;
        }

        // Write() calls GetFinalData() which calls SetChecksums() internally.
        // For non-SCBlock saves, SetChecksums() writes directly into the Data span,
        // so the in-memory save's checksums are fixed as a side effect.
        _ = SaveFile.Write();
        SaveFile.State.Edited = true;
        StateHasChanged();

        Snackbar.Add(SaveFile.ChecksumsValid
            ? "Checksums recalculated — all valid."
            : "Checksums recalculated.", Severity.Success);
    }

    private void OnHealParty()
    {
        if (SaveFile is null)
        {
            return;
        }

        var count = 0;
        for (var i = 0; i < SaveFile.PartyCount; i++)
        {
            var pk = SaveFile.GetPartySlotAtIndex(i);
            if (pk.Species == 0)
            {
                continue;
            }

            pk.Heal();
            SaveFile.SetPartySlotAtIndex(pk, i);
            count++;
        }

        SaveFile.State.Edited = true;
        RefreshService.RefreshBoxAndPartyState();
        StateHasChanged();
        Snackbar.Add($"Healed {count} party Pokémon.", Severity.Success);
    }

    private void OnHealBoxPP()
    {
        if (SaveFile is null || !SaveFile.HasBox)
        {
            return;
        }

        var count = SaveFile.ModifyBoxes(pk => pk.HealPP());
        SaveFile.State.Edited = true;
        RefreshService.RefreshBoxAndPartyState();
        StateHasChanged();
        Snackbar.Add($"Restored PP for {count} box Pokémon.", Severity.Success);
    }

    private void OnCompactStorage()
    {
        if (SaveFile is not IStorageCleanup cleanup)
        {
            return;
        }

        var modified = cleanup.FixStoragePreWrite();
        SaveFile.State.Edited = true;
        RefreshService.RefreshBoxAndPartyState();
        StateHasChanged();
        Snackbar.Add(modified
            ? "Storage compacted — empty gaps removed."
            : "Storage is already compact.", Severity.Info);
    }

    private void Close() => MudDialog.Close(DialogResult.Cancel());
}
