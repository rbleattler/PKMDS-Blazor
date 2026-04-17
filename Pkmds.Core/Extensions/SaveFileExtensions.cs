namespace Pkmds.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="SaveFile"/> that deal with storage-slot layout invariants.
/// </summary>
public static class SaveFileExtensions
{
    private const int PartySize = 6;

    /// <summary>
    /// Rewrites the party so all non-blank members are contiguous at indices 0..N-1, with the
    /// remaining slots blanked. Use after any party write that could have left a gap (e.g. writing
    /// to an index past <see cref="SaveFile.PartyCount"/> or deleting a middle slot).
    /// </summary>
    /// <remarks>
    /// Mirrors the invariant PKHeX's <c>SlotInfoParty.WriteTo</c> enforces by realigning writes to
    /// <c>Math.Min(slot, PartyCount)</c> — but applied post-hoc so callers don't need to compute
    /// the realigned slot themselves.
    /// </remarks>
    public static void CompactParty(this SaveFile sav)
    {
        var nonBlank = new List<PKM>(PartySize);
        for (var i = 0; i < PartySize; i++)
        {
            var pkm = sav.GetPartySlotAtIndex(i);
            if (pkm.Species != 0)
            {
                nonBlank.Add(pkm);
            }
        }

        for (var i = 0; i < nonBlank.Count; i++)
        {
            sav.SetPartySlotAtIndex(nonBlank[i], i);
        }

        for (var i = nonBlank.Count; i < PartySize; i++)
        {
            sav.SetPartySlotAtIndex(sav.BlankPKM, i);
        }
    }

    /// <summary>
    /// For Gen 1/2 saves — whose box storage is a packed list, not a grid — collects non-blank
    /// slots in <paramref name="box"/> and rewrites them contiguously starting at slot 0. No-op
    /// for other generations, where gaps between box slots are valid.
    /// </summary>
    public static void CompactBoxIfGen12(this SaveFile sav, int box)
    {
        if (sav.Context is not (EntityContext.Gen1 or EntityContext.Gen2))
        {
            return;
        }

        var slotCount = sav.BoxSlotCount;
        var nonBlank = new List<PKM>(slotCount);
        for (var i = 0; i < slotCount; i++)
        {
            var pkm = sav.GetBoxSlotAtIndex(box, i);
            if (pkm.Species != 0)
            {
                nonBlank.Add(pkm);
            }
        }

        for (var i = 0; i < nonBlank.Count; i++)
        {
            sav.SetBoxSlotAtIndex(nonBlank[i], box, i);
        }

        for (var i = nonBlank.Count; i < slotCount; i++)
        {
            sav.SetBoxSlotAtIndex(sav.BlankPKM, box, i);
        }
    }
}
