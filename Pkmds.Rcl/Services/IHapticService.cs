namespace Pkmds.Rcl.Services;

/// <summary>
/// Plays short haptic pulses via <c>navigator.vibrate</c> for key interactions
/// (drag/drop, save export, batch legalize completion, settings toggles).
/// Silently no-ops when the user has haptics disabled or when the platform doesn't
/// implement the Vibration API (notably iOS Safari).
/// </summary>
public interface IHapticService
{
    /// <summary>Light tap — drag lift, slot tap, switch toggle.</summary>
    void Tap();

    /// <summary>Slightly longer tap — confirms a successful action (drop, button press).</summary>
    void Confirm();

    /// <summary>Double-pulse — successful completion of a multi-step action (export, legalize).</summary>
    void Success();

    /// <summary>Triple low pulse — a destructive or error condition.</summary>
    void Error();

    /// <summary>Plays an arbitrary pattern (single duration in ms or a pattern array).</summary>
    void Vibrate(int milliseconds);

    /// <summary>Plays an arbitrary pattern (alternating vibrate/pause durations in ms).</summary>
    void Vibrate(int[] pattern);
}
