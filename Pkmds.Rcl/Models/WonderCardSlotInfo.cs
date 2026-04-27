namespace Pkmds.Rcl.Models;

/// <summary>
/// Generation-agnostic snapshot of a single wonder card / mystery gift slot stored in the
/// loaded save file. Used by the Wonder Cards viewer to display Gen 3 (<see cref="WonderCard3" />)
/// and Gen 4–7 (<see cref="DataMysteryGift" />) slots in a unified table.
/// </summary>
public sealed record WonderCardSlotInfo
{
    /// <summary>0-based slot index. For Gen 3 this is always 0; for Gen 4 HG/SS, the Lock
    /// Capsule appears as the highest index.</summary>
    public required int Index { get; init; }

    /// <summary>Human-readable card title (e.g., the gift's name as shown in-game). For empty
    /// slots this is the localized "(empty)" placeholder.</summary>
    public required string Title { get; init; }

    /// <summary>Concrete card type label — e.g. "WC6", "PGF", "WonderCard3". Useful for
    /// distinguishing card formats inside a multi-format storage block.</summary>
    public required string CardType { get; init; }

    /// <summary>The card's in-game card ID, when the format exposes one. <see langword="null" />
    /// for formats without IDs (e.g. <see cref="WR7" />) or for empty slots.</summary>
    public int? CardId { get; init; }

    /// <summary>Pokémon species delivered by the card, when the card is an entity gift.
    /// <see langword="null" /> for item gifts, BP gifts, item-only WC3 cards, etc.</summary>
    public ushort? Species { get; init; }

    /// <summary>True when the slot has no card written. Empty rows still appear in the table
    /// so the user can see the storage's full slot count.</summary>
    public required bool IsEmpty { get; init; }

    /// <summary>Received-flag state when the storage exposes <see cref="IMysteryGiftFlags" />.
    /// <see langword="null" /> when the format has no received-flag bitmap (Gen 3).</summary>
    public bool? Received { get; init; }

    /// <summary>Optional one-line note (e.g. "Lock Capsule" for SAV4HGSS, "Mystery Event script
    /// present" for Gen 3, "Link card" for WC3 type 2).</summary>
    public string? ExtraInfo { get; init; }
}
