using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace NordSharp.Platform;

/// <summary>
/// Unix (Linux/macOS) specific NordVPN adapter.
/// </summary>
internal sealed class UnixAdapter : IPlatformAdapter
{
    private readonly VpnPlatform _platform;

    /// <summary>
    /// Known NordVPN network interface name patterns on Linux/macOS.
    /// </summary>
    private static readonly string[] VpnInterfacePatterns =
    [
        "nordlynx",     // WireGuard-based NordLynx interface
        "nordtun",      // NordVPN tunnel interface
        "tun",          // Generic TUN interface (OpenVPN)
        "tap"           // Generic TAP interface (OpenVPN)
    ];

    public UnixAdapter(VpnPlatform platform)
    {
        if (platform != VpnPlatform.Linux && platform != VpnPlatform.MacOS)
            throw new ArgumentException("UnixAdapter only supports Linux and macOS", nameof(platform));
        _platform = platform;
    }

    public VpnPlatform Platform => _platform;

    public (bool installed, string? path) CheckInstallation(string? customPath = null)
    {
        // On Linux/macOS, nordvpn should be in PATH
        var (exitCode, output, _) = ProcessRunner.Run("which", "nordvpn", null, 5000);
        
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            return (true, null); // No specific path needed on Unix

        // Fallback: try running nordvpn directly
        var (exitCode2, _, _) = ProcessRunner.Run("nordvpn", "--version", null, 5000);
        return (exitCode2 == 0, null);
    }

    public bool IsServiceRunning()
    {
        // On Linux, check if nordvpnd daemon is running
        var (exitCode, output, _) = ProcessRunner.Run("pgrep", "-x nordvpnd", null, 5000);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            return true;

        // Alternative check via systemctl on Linux
        if (_platform == VpnPlatform.Linux)
        {
            var (exitCode2, output2, _) = ProcessRunner.Run("systemctl", "is-active nordvpnd", null, 5000);
            if (output2.Trim() == "active")
                return true;
        }

        // Try a simple nordvpn command to see if it works
        var (exitCode3, _, error3) = ProcessRunner.Run("nordvpn", "status", null, 10000);
        return exitCode3 == 0 || !error3.Contains("daemon");
    }

    public bool IsLoggedIn()
    {
        var (exitCode, output, error) = ProcessRunner.Run("nordvpn", "account", null, 10000);
        
        // If command succeeds and doesn't say "not logged in", assume logged in
        if (exitCode == 0)
        {
            var combined = (output + error).ToLowerInvariant();
            return !combined.Contains("not logged in") && !combined.Contains("log in");
        }

        return false;
    }

    /// <summary>
    /// Checks if VPN is currently connected by examining network interface status.
    /// </summary>
    public bool IsConnected()
    {
        // First check via nordvpn status command (most reliable on Linux/macOS)
        var (exitCode, output, _) = ProcessRunner.Run("nordvpn", "status", null, 5000);
        if (exitCode == 0)
        {
            var lowerOutput = output.ToLowerInvariant();
            // Check for "Status: Connected" pattern, avoiding false positives from "Disconnected"
            if (lowerOutput.Contains("status: connected") || 
                (lowerOutput.Contains("connected") && !lowerOutput.Contains("disconnected")))
                return true;
            if (lowerOutput.Contains("disconnected") || lowerOutput.Contains("status: disconnected"))
                return false;
        }

        // Fallback: check network interfaces for actual IP assignment
        return IsVpnInterfaceActive();
    }

    /// <summary>
    /// Checks if a NordVPN network interface is active with an IP and/or gateway assigned.
    /// </summary>
    private static bool IsVpnInterfaceActive()
    {
        var iface = GetNordVpnInterface();
        if (iface == null || iface.OperationalStatus != OperationalStatus.Up)
            return false;

        try
        {
            var ipProps = iface.GetIPProperties();
            
            // Best indicator: Check if the VPN interface has a gateway assigned
            var gateways = ipProps.GatewayAddresses;
            if (gateways != null && gateways.Count > 0)
            {
                foreach (var gw in gateways)
                {
                    if (gw.Address != null && !gw.Address.Equals(System.Net.IPAddress.None))
                        return true;
                }
            }

            // Fallback: Check for valid IP assignment
            foreach (var addr in ipProps.UnicastAddresses)
            {
                if (IsValidVpnAddress(addr.Address))
                    return true;
            }
        }
        catch
        {
            // Fall back to just operational status
            return iface.OperationalStatus == OperationalStatus.Up;
        }

        return false;
    }

    /// <summary>
    /// Checks if an IP address is a valid VPN address (not link-local, loopback, or multicast).
    /// </summary>
    private static bool IsValidVpnAddress(System.Net.IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            
            // Skip loopback (127.x.x.x)
            if (bytes[0] == 127)
                return false;
            
            // Skip link-local (169.254.x.x)
            if (bytes[0] == 169 && bytes[1] == 254)
                return false;
            
            // Skip multicast (224.0.0.0 - 239.255.255.255)
            if (bytes[0] >= 224 && bytes[0] <= 239)
                return false;
            
            // Skip broadcast (255.255.255.255)
            if (bytes[0] == 255 && bytes[1] == 255 && bytes[2] == 255 && bytes[3] == 255)
                return false;

            // Skip 0.0.0.0
            if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0)
                return false;

            // Accept private ranges (commonly used by VPNs):
            // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
            // Also accept public IPs (some VPNs assign these)
            return true;
        }
        else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // Skip link-local IPv6 (fe80::)
            if (ip.IsIPv6LinkLocal)
                return false;
            
            // Skip loopback (::1)
            if (System.Net.IPAddress.IsLoopback(ip))
                return false;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the NordVPN network interface if present.
    /// </summary>
    private static NetworkInterface? GetNordVpnInterface()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            return interfaces.FirstOrDefault(ni =>
                VpnInterfacePatterns.Any(pattern =>
                    ni.Name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets information about all active network interfaces.
    /// </summary>
    public static IReadOnlyList<(string Name, string Description, OperationalStatus Status)> GetActiveInterfaces()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(ni => (ni.Name, ni.Description, ni.OperationalStatus))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public bool Disconnect(string? installPath = null)
    {
        var (exitCode, output, error) = ProcessRunner.Run("nordvpn", "d", null, 30000);
        
        var combined = (output + error).ToLowerInvariant();
        
        // Success if disconnected or already disconnected
        return exitCode == 0 || 
               combined.Contains("disconnected") || 
               combined.Contains("not connected") ||
               combined.Contains("you are not");
    }

    public (bool success, string? serverName) Connect(string target, string? installPath = null, bool isSpecificServer = false, bool isGroup = false)
    {
        string args;

        if (string.IsNullOrEmpty(target))
        {
            // Quick connect
            args = "c";
        }
        else if (isSpecificServer)
        {
            // Specific server like nl742
            args = $"c {target}";
        }
        else if (isGroup)
        {
            // Specialty group - use --group flag
            args = $"c --group \"{target}\"";
        }
        else
        {
            // Country or region - replace spaces with underscores for Linux
            var formattedTarget = target.Replace(" ", "_");
            args = $"c {formattedTarget}";
        }

        var (exitCode, output, error) = ProcessRunner.Run("nordvpn", args, null, 60000);

        var combined = output + error;
        var lowerCombined = combined.ToLowerInvariant();

        // Check for success
        if (lowerCombined.Contains("you are connected") || lowerCombined.Contains("connected to"))
        {
            var serverName = ExtractServerName(combined) ?? target;
            return (true, serverName);
        }

        // Check for already connected
        if (lowerCombined.Contains("already connected"))
        {
            var serverName = ExtractServerName(combined) ?? target;
            return (true, serverName);
        }

        return (false, null);
    }

    public IReadOnlyList<string> GetConnectCommand()
    {
        return ["nordvpn", "c"];
    }

    public IReadOnlyList<string> GetDisconnectCommand()
    {
        return ["nordvpn", "d"];
    }

    private static string? ExtractServerName(string output)
    {
        // Try to extract server name from output like "You are connected to nl742 (Netherlands #742)"
        var match = Regex.Match(output, @"(?:connected to|connecting to)\s+(\S+)", RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        // Try alternative format
        match = Regex.Match(output, @"(\w+\d+)(?:\s*\(|$)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
