namespace Pkmds.Rcl.Services;

/// <inheritdoc cref="IHapticService" />
public sealed class HapticService(IJSRuntime jsRuntime, IAppState appState) : IHapticService
{
    public void Tap() => Vibrate(10);

    public void Confirm() => Vibrate(20);

    public void Success() => Vibrate([10, 40, 15]);

    public void Error() => Vibrate([30, 30, 30, 30, 30]);

    public void Vibrate(int milliseconds)
    {
        if (!appState.HapticsEnabled || milliseconds <= 0)
        {
            return;
        }

        Invoke(milliseconds);
    }

    public void Vibrate(int[] pattern)
    {
        if (!appState.HapticsEnabled || pattern.Length == 0)
        {
            return;
        }

        Invoke(pattern);
    }

    // Prefer IJSInProcessRuntime so dragstart handlers can fire haptics synchronously
    // without awaiting — same pattern as setSlotDragMarker. Fire-and-forget on the async
    // path so callers never have to await a haptic.
    private void Invoke(object pattern)
    {
        try
        {
            if (jsRuntime is IJSInProcessRuntime inProcess)
            {
                inProcess.InvokeVoid("pkmdsHaptic", pattern);
                return;
            }

            _ = jsRuntime.InvokeVoidAsync("pkmdsHaptic", pattern);
        }
        catch (JSException)
        {
            // Vibration failures are not user-visible and not worth surfacing.
        }
    }
}
