using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NordSharp.Internal;
using NordSharp.Platform;

namespace NordSharp;

/// <summary>
/// Main entry point for NordVPN operations. Thread-safe static class.
/// </summary>
public static class NordVpn
{
    private static readonly SemaphoreSlim RotateLock = new SemaphoreSlim(1, 1);
    private static readonly object InitLock = new object();

    private static readonly Random Random = new Random();

    /// <summary>
    /// Maximum retry attempts for connection operations.
    /// </summary>
    public static int MaxRetries { get; set; } = 4;

    /// <summary>
    /// Timeout in milliseconds to wait for IP change verification.
    /// </summary>
    public static int IpChangeTimeoutMs { get; set; } = 60000;

    /// <summary>
    /// Initializes VPN settings based on the provided options.
    /// </summary>
    /// <param name="options">Configuration options. If null, uses quick connect.</param>
    /// <returns>Immutable settings object for use with Rotate/Terminate.</returns>
    /// <exception cref="InvalidOperationException">If NordVPN is not installed or service not running.</exception>
    public static VpnSettings Initialize(VpnOptions? options = null)
    {
        lock (InitLock)
        {
            options ??= VpnOptions.ForQuickConnect();

            var adapter = PlatformAdapterFactory.Create();
            var platform = adapter.Platform;

            // Check installation
            var (installed, installPath) = adapter.CheckInstallation(options.CustomInstallPath);
            if (!installed)
            {
                throw new InvalidOperationException(
                    $"NordVPN is not installed. " +
                    (platform == VpnPlatform.Windows
                        ? "Please install NordVPN or specify a custom installation path."
                        : "Please install NordVPN using your package manager."));
            }

            // Check service (Windows)
            if (platform == VpnPlatform.Windows && !adapter.IsServiceRunning())
            {
                throw new InvalidOperationException(
                    "NordVPN service is not running. Please start the nordvpn-service in Task Manager -> Services.");
            }
            
            // Build server list based on options
            var servers = new List<string>();
            var quickConnect = false;
            string? specialtyGroup = null;

            if (options.CompleteRotation)
            {
                // Fetch all servers from API
                var allServers = FetchServers();
                servers.AddRange(allServers.Select(s => s.Identifier));
            }
            else if (options.Servers != null && options.Servers.Count > 0)
            {
                servers.AddRange(options.Servers);
            }
            else if (options.Countries != null && options.Countries.Count > 0)
            {
                // Use country names directly
                foreach (var country in options.Countries)
                {
                    var formatted = platform == VpnPlatform.Windows
                        ? country
                        : CountryList.ToLinuxFormat(country);
                    servers.Add(formatted);
                }
            }
            else if (!string.IsNullOrEmpty(options.Region))
            {
                // Region is guaranteed non-null here
                var regionCountries = CountryList.GetRegion(options.Region!);
                if (regionCountries != null)
                {
                    foreach (var country in regionCountries)
                    {
                        var formatted = platform == VpnPlatform.Windows
                            ? country
                            : CountryList.ToLinuxFormat(country);
                        servers.Add(formatted);
                    }
                }
                else
                {
                    // Treat as a single location
                    servers.Add(platform == VpnPlatform.Windows
                        ? options.Region!
                        : CountryList.ToLinuxFormat(options.Region!));
                }
            }
            else if (!string.IsNullOrEmpty(options.SpecialtyGroup))
            {
                // SpecialtyGroup is guaranteed non-null here
                specialtyGroup = options.SpecialtyGroup;
                servers.Add(options.SpecialtyGroup!);
            }
            else
            {
                // Quick connect
                quickConnect = true;
            }

            // Try to get current IP (optional - used for change detection during rotate)
            // This is non-blocking and won't fail initialization if network is unavailable
            string originalIp = "unknown";
            try
            {
                originalIp = HttpHelper.GetCurrentIpv4(1) ?? "unknown";
            }
            catch
            {
                // Ignore - we'll detect IP change during rotate by comparing before/after
            }

            // Build base command
            var baseCommand = adapter.GetConnectCommand().ToList();

            return new VpnSettings(
                platform,
                installPath,
                originalIp,
                servers,
                quickConnect,
                specialtyGroup,
                baseCommand);
        }
    }

    /// <summary>
    /// Rotates to a new VPN server. Thread-safe - only one rotation can occur at a time.
    /// </summary>
    /// <param name="settings">Settings from Initialize().</param>
    /// <returns>Connection result with new IP and server info.</returns>
    public static VpnConnectionResult Rotate(VpnSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        // Ensure only one rotation at a time
        RotateLock.Wait();
        try
        {
            return RotateInternal(settings);
        }
        finally
        {
            RotateLock.Release();
        }
    }

    /// <summary>
    /// Rotates to a new VPN server asynchronously. Thread-safe.
    /// </summary>
    public static async Task<VpnConnectionResult> RotateAsync(VpnSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        await RotateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return RotateInternal(settings);
        }
        finally
        {
            RotateLock.Release();
        }
    }

    private static VpnConnectionResult RotateInternal(VpnSettings settings)
    {
        var adapter = PlatformAdapterFactory.Create(settings.Platform);

        // Pick a server
        string target;
        bool isSpecificServer = false;
        bool isGroup = false;

        if (settings.QuickConnect)
        {
            target = string.Empty;
        }
        else if (settings.Servers.Count > 0)
        {
            target = settings.Servers[Random.Next(settings.Servers.Count)];
            isSpecificServer = IsSpecificServer(target);
            isGroup = !string.IsNullOrEmpty(settings.SpecialtyGroup);
        }
        else
        {
            target = string.Empty;
        }

        // Just connect - NordVPN handles everything
        var (success, serverName) = adapter.Connect(target, settings.InstallPath, isSpecificServer, isGroup);

        if (!success)
        {
            return VpnConnectionResult.Failed("Connection command failed.", null, 1);
        }

        // Fetch new IP - retry if we get garbage (tunnel not ready yet)
        string? newIp = null;
        for (int i = 0; i < 3; i++)
        {
            try
            {
                var ip = HttpHelper.GetCurrentIpv4(3);
                if (ip != null && IsValidIpv4(ip))
                {
                    newIp = ip;
                    break;
                }
            }
            catch { }

            // Not ready yet
            if (i < 2)
            {
                Console.WriteLine("Waiting for tunnel...");
                Thread.Sleep(1000);
            }
        }

        return VpnConnectionResult.Succeeded(newIp ?? "connected", null, serverName ?? target, 1);
    }

    private static bool IsValidIpv4(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return false;

        var parts = ip.Trim().Split('.');
        if (parts.Length != 4)
            return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var num) || num < 0 || num > 255)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Disconnects from the current VPN server.
    /// </summary>
    /// <param name="settings">Optional settings. If null, uses current platform defaults.</param>
    public static void Terminate(VpnSettings? settings = null)
    {
        var adapter = settings != null
            ? PlatformAdapterFactory.Create(settings.Platform)
            : PlatformAdapterFactory.Create();

        adapter.Disconnect(settings?.InstallPath);
    }

    /// <summary>
    /// Gets the current public IP address (prefers IPv4).
    /// </summary>
    public static string GetCurrentIp()
    {
        return HttpHelper.GetCurrentIp();
    }

    /// <summary>
    /// Gets both IPv4 and IPv6 public addresses if available.
    /// </summary>
    /// <returns>Tuple with IPv4 and IPv6 addresses (either can be null if not available).</returns>
    public static (string? Ipv4, string? Ipv6) GetCurrentIpAddresses()
    {
        return HttpHelper.GetCurrentIpAddresses();
    }

    /// <summary>
    /// Gets the current public IPv4 address.
    /// </summary>
    /// <returns>IPv4 address or null if not available.</returns>
    public static string? GetCurrentIpv4()
    {
        return HttpHelper.GetCurrentIpv4();
    }

    /// <summary>
    /// Gets the current public IPv6 address.
    /// </summary>
    /// <returns>IPv6 address or null if not available.</returns>
    public static string? GetCurrentIpv6()
    {
        return HttpHelper.GetCurrentIpv6();
    }

    /// <summary>
    /// Checks if the VPN is currently connected by examining network adapter status.
    /// </summary>
    /// <returns>True if connected to VPN, false otherwise.</returns>
    public static bool IsConnected()
    {
        var adapter = PlatformAdapterFactory.Create();
        return adapter.IsConnected();
    }

    /// <summary>
    /// Fetches all available NordVPN servers from the API.
    /// </summary>
    /// <returns>List of available servers.</returns>
    public static IReadOnlyList<NordServer> FetchServers()
    {
        var json = HttpHelper.FetchNordVpnServersJson();
        return SimpleJsonParser.ParseServers(json);
    }

    /// <summary>
    /// Saves settings to a file.
    /// </summary>
    public static void SaveSettings(VpnSettings settings, string path)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        IniFile.Save(settings, path);
    }

    /// <summary>
    /// Loads settings from a file.
    /// </summary>
    public static VpnSettings LoadSettings(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentNullException(nameof(path));

        return IniFile.Load(path);
    }

    private static bool IsSpecificServer(string target)
    {
        if (string.IsNullOrEmpty(target))
            return false;

        // Specific servers follow pattern: 2+ letters followed by numbers (e.g., nl742, us1234)
        if (target.Length < 3)
            return false;

        var letterCount = 0;
        var hasNumbers = false;

        foreach (var c in target)
        {
            if (char.IsLetter(c))
            {
                if (hasNumbers) return false; // Letters after numbers = not specific server
                letterCount++;
            }
            else if (char.IsDigit(c))
            {
                hasNumbers = true;
            }
            else
            {
                return false; // Invalid character
            }
        }

        return letterCount >= 2 && hasNumbers;
    }
}
