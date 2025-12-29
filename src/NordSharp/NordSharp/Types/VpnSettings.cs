using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NordSharp;

/// <summary>
/// Immutable runtime VPN settings created by Initialize().
/// </summary>
public sealed class VpnSettings
{
    /// <summary>
    /// Gets the detected operating system platform.
    /// </summary>
    public VpnPlatform Platform { get; }

    /// <summary>
    /// Gets the NordVPN installation path (Windows only).
    /// </summary>
    public string? InstallPath { get; }

    /// <summary>
    /// Gets the original IP address before VPN connection.
    /// </summary>
    public string OriginalIp { get; }

    /// <summary>
    /// Gets the list of server identifiers to rotate between.
    /// </summary>
    public IReadOnlyList<string> Servers { get; }

    /// <summary>
    /// Gets whether quick connect mode is enabled.
    /// </summary>
    public bool QuickConnect { get; }

    /// <summary>
    /// Gets the specialty group if specified.
    /// </summary>
    public string? SpecialtyGroup { get; }

    /// <summary>
    /// Gets the base command arguments for NordVPN CLI.
    /// </summary>
    internal IReadOnlyList<string> BaseCommand { get; }

    /// <summary>
    /// Creates a new VpnSettings instance.
    /// </summary>
    internal VpnSettings(
        VpnPlatform platform,
        string? installPath,
        string originalIp,
        IReadOnlyList<string> servers,
        bool quickConnect,
        string? specialtyGroup,
        IReadOnlyList<string> baseCommand)
    {
        Platform = platform;
        InstallPath = installPath;
        OriginalIp = originalIp ?? throw new ArgumentNullException(nameof(originalIp));
        Servers = servers ?? throw new ArgumentNullException(nameof(servers));
        QuickConnect = quickConnect;
        SpecialtyGroup = specialtyGroup;
        BaseCommand = baseCommand ?? throw new ArgumentNullException(nameof(baseCommand));
    }

    /// <summary>
    /// Gets the current platform.
    /// </summary>
    public static VpnPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return VpnPlatform.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return VpnPlatform.Linux;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return VpnPlatform.MacOS;

        throw new PlatformNotSupportedException("Only Windows, Linux, and macOS are supported.");
    }
}

/// <summary>
/// Supported VPN platforms.
/// </summary>
public enum VpnPlatform
{
    /// <summary>Windows operating system.</summary>
    Windows,
    /// <summary>Linux operating system.</summary>
    Linux,
    /// <summary>macOS operating system.</summary>
    MacOS
}
