using System.Collections.Generic;

namespace NordSharp.Platform;

/// <summary>
/// Platform-specific adapter for NordVPN CLI operations.
/// </summary>
internal interface IPlatformAdapter
{
    /// <summary>
    /// Gets the platform this adapter supports.
    /// </summary>
    VpnPlatform Platform { get; }

    /// <summary>
    /// Checks if NordVPN is installed and returns the installation path (if applicable).
    /// </summary>
    (bool installed, string? path) CheckInstallation(string? customPath = null);

    /// <summary>
    /// Checks if the NordVPN service is running.
    /// </summary>
    bool IsServiceRunning();

    /// <summary>
    /// Checks if the user is logged in to NordVPN.
    /// </summary>
    bool IsLoggedIn();

    /// <summary>
    /// Checks if VPN is currently connected by examining network adapter/interface status.
    /// </summary>
    bool IsConnected();

    /// <summary>
    /// Disconnects from the current VPN connection.
    /// </summary>
    bool Disconnect(string? installPath = null);

    /// <summary>
    /// Connects to a specific server or location.
    /// </summary>
    /// <param name="target">The server identifier, country, or group to connect to.</param>
    /// <param name="installPath">The NordVPN installation path (Windows only).</param>
    /// <param name="isSpecificServer">True if target is a specific server ID (e.g., nl742).</param>
    /// <param name="isGroup">True if target is a specialty group.</param>
    /// <returns>Tuple of (success, serverName from output).</returns>
    (bool success, string? serverName) Connect(string target, string? installPath = null, bool isSpecificServer = false, bool isGroup = false);

    /// <summary>
    /// Gets the base connect command arguments.
    /// </summary>
    IReadOnlyList<string> GetConnectCommand();

    /// <summary>
    /// Gets the disconnect command arguments.
    /// </summary>
    IReadOnlyList<string> GetDisconnectCommand();
}
