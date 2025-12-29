using System.Collections.Generic;

namespace NordSharp;

/// <summary>
/// Configuration options for VPN initialization.
/// </summary>
public sealed class VpnOptions
{
    /// <summary>
    /// Gets or sets the list of countries to rotate between (e.g., "United States", "Germany").
    /// </summary>
    public IReadOnlyList<string>? Countries { get; set; }

    /// <summary>
    /// Gets or sets the list of specific server identifiers (e.g., "nl742", "be166").
    /// </summary>
    public IReadOnlyList<string>? Servers { get; set; }

    /// <summary>
    /// Gets or sets whether to use complete rotation (fetch all servers from NordVPN API).
    /// </summary>
    public bool CompleteRotation { get; set; }

    /// <summary>
    /// Gets or sets whether to use quick connect (let NordVPN pick the best server).
    /// </summary>
    public bool QuickConnect { get; set; }

    /// <summary>
    /// Gets or sets the region to connect to (e.g., "europe", "americas").
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the specialty group (e.g., "Double VPN", "P2P", "Dedicated IP").
    /// </summary>
    public string? SpecialtyGroup { get; set; }

    /// <summary>
    /// Gets or sets the custom NordVPN installation path (Windows only).
    /// </summary>
    public string? CustomInstallPath { get; set; }

    /// <summary>
    /// Creates a new VpnOptions instance with default settings.
    /// </summary>
    public VpnOptions()
    {
    }

    /// <summary>
    /// Creates options for connecting to specific countries.
    /// </summary>
    public static VpnOptions ForCountries(params string[] countries)
    {
        return new VpnOptions { Countries = countries };
    }

    /// <summary>
    /// Creates options for connecting to specific servers.
    /// </summary>
    public static VpnOptions ForServers(params string[] servers)
    {
        return new VpnOptions { Servers = servers };
    }

    /// <summary>
    /// Creates options for complete rotation (all servers).
    /// </summary>
    public static VpnOptions ForCompleteRotation()
    {
        return new VpnOptions { CompleteRotation = true };
    }

    /// <summary>
    /// Creates options for quick connect.
    /// </summary>
    public static VpnOptions ForQuickConnect()
    {
        return new VpnOptions { QuickConnect = true };
    }

    /// <summary>
    /// Creates options for a specific region.
    /// </summary>
    public static VpnOptions ForRegion(string region)
    {
        return new VpnOptions { Region = region };
    }
}
