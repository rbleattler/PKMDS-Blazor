namespace Pkmds.Rcl;

/// <summary>
/// Identifies the environment hosting PKMDS. Standalone web app is the default;
/// any other value indicates the app is embedded inside a host (emulator,
/// companion app, etc.) that drives it via the JS interop bridge.
/// </summary>
public enum HostKind
{
    /// <summary>Standalone web app — the default when no host has been declared.</summary>
    None,

    /// <summary>Embedded inside an external host (any value of <c>?host=</c>).</summary>
    Generic,
}
