namespace Pkmds.Rcl.Services;

/// <summary>
/// Exposes information about the environment hosting PKMDS. Components and
/// services inject this to gate UI and behaviour that only makes sense in the
/// standalone web app (file picker, drawer, service worker, PokeAPI sprite
/// overlay, etc.) versus an embedded host that drives PKMDS via the JS interop
/// bridge.
/// </summary>
public interface IHostService
{
    /// <summary>True when PKMDS is embedded in an external host.</summary>
    bool IsEmbedded { get; }

    /// <summary>The category of host. <see cref="HostKind.None" /> for the standalone web app.</summary>
    HostKind HostKind { get; }

    /// <summary>
    /// The raw value of the <c>?host=</c> query string parameter, or <c>null</c>
    /// when absent. Useful for diagnostics and future host-specific branching.
    /// </summary>
    string? HostName { get; }
}
