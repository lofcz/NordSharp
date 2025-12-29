using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace NordSharp.Platform;

/// <summary>
/// Windows-specific NordVPN adapter.
/// </summary>
internal sealed class WindowsAdapter : IPlatformAdapter
{
    private static readonly string[] DefaultPaths =
    [
        @"C:\Program Files\NordVPN",
        @"C:\Program Files (x86)\NordVPN"
    ];

    /// <summary>
    /// Known NordVPN network adapter name patterns.
    /// </summary>
    private static readonly string[] VpnAdapterPatterns =
    [
        "NordLynx",           // WireGuard-based NordLynx adapter
        "TAP-NordVPN",        // OpenVPN TAP adapter
        "NordVPN",            // Generic NordVPN adapter
        "TAP-Windows"         // Fallback TAP adapter (used by NordVPN)
    ];

    #region Windows IP Helper API P/Invoke

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetIpForwardTable(IntPtr pIpForwardTable, ref int pdwSize, bool bOrder);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_IPFORWARDROW
    {
        public uint dwForwardDest;
        public uint dwForwardMask;
        public uint dwForwardPolicy;
        public uint dwForwardNextHop;
        public uint dwForwardIfIndex;
        public uint dwForwardType;
        public uint dwForwardProto;
        public uint dwForwardAge;
        public uint dwForwardNextHopAS;
        public uint dwForwardMetric1;
        public uint dwForwardMetric2;
        public uint dwForwardMetric3;
        public uint dwForwardMetric4;
        public uint dwForwardMetric5;
    }

    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int NO_ERROR = 0;

    #endregion

    public VpnPlatform Platform => VpnPlatform.Windows;

    public (bool installed, string? path) CheckInstallation(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            var exePath = Path.Combine(customPath, "NordVPN.exe");
            if (File.Exists(exePath))
                return (true, customPath);
            return (false, null);
        }

        foreach (var path in DefaultPaths)
        {
            if (Directory.Exists(path))
            {
                var exePath = Path.Combine(path, "NordVPN.exe");
                if (File.Exists(exePath))
                    return (true, path);
            }
        }

        return (false, null);
    }

    public bool IsServiceRunning()
    {
        return ProcessRunner.IsProcessRunning("nordvpn-service");
    }

    public bool IsLoggedIn()
    {
        // Check if the NordVPN network adapter exists (installed during setup)
        return GetNordVpnAdapter() != null;
    }

    /// <summary>
    /// Checks if VPN is currently connected by examining the routing table.
    /// Returns true only if traffic to the internet would route through the VPN adapter.
    /// </summary>
    public bool IsConnected()
    {
        var vpnAdapter = GetNordVpnAdapter();
        if (vpnAdapter == null)
            return false;

        // Get the VPN adapter's interface index
        var vpnIfIndex = GetInterfaceIndex(vpnAdapter);
        if (vpnIfIndex == 0)
            return false;

        // Method 1: Use GetBestInterface to ask Windows which interface
        // would be used to reach an external IP (e.g., 8.8.8.8)
        if (IsVpnBestInterfaceForDestination(vpnIfIndex, "8.8.8.8"))
            return true;

        // Method 2: Check the routing table for default routes through VPN
        if (HasActiveDefaultRouteThrough(vpnIfIndex))
            return true;

        return false;
    }

    /// <summary>
    /// Asks Windows which interface would be used to reach a destination IP.
    /// </summary>
    private static bool IsVpnBestInterfaceForDestination(uint vpnIfIndex, string destinationIp)
    {
        try
        {
            var destAddr = IpToUint(IPAddress.Parse(destinationIp));
            int result = GetBestInterface(destAddr, out uint bestIfIndex);
            
            if (result == NO_ERROR)
            {
                return bestIfIndex == vpnIfIndex;
            }
        }
        catch
        {
            // Fall through to other methods
        }

        return false;
    }

    /// <summary>
    /// Checks if the routing table has an active default route (0.0.0.0/0) through the VPN interface.
    /// </summary>
    private static bool HasActiveDefaultRouteThrough(uint vpnIfIndex)
    {
        try
        {
            var routes = GetIpForwardTableEntries();
            
            // Look for default routes (destination 0.0.0.0, mask 0.0.0.0) through VPN
            foreach (var route in routes)
            {
                // Check if this is a default route
                if (route.dwForwardDest == 0 && route.dwForwardMask == 0)
                {
                    // Check if it goes through the VPN interface
                    if (route.dwForwardIfIndex == vpnIfIndex)
                    {
                        // Verify it has a valid next hop (gateway)
                        if (route.dwForwardNextHop != 0)
                            return true;
                    }
                }
            }
        }
        catch
        {
            // Routing table query failed
        }

        return false;
    }

    /// <summary>
    /// Retrieves all entries from the IP forwarding (routing) table.
    /// </summary>
    private static List<MIB_IPFORWARDROW> GetIpForwardTableEntries()
    {
        var routes = new List<MIB_IPFORWARDROW>();
        int size = 0;

        // First call to get required buffer size
        int result = GetIpForwardTable(IntPtr.Zero, ref size, true);
        if (result != ERROR_INSUFFICIENT_BUFFER && result != NO_ERROR)
            return routes;

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            result = GetIpForwardTable(buffer, ref size, true);
            if (result != NO_ERROR)
                return routes;

            // First DWORD is the number of entries
            int numEntries = Marshal.ReadInt32(buffer);
            IntPtr currentPtr = buffer + 4; // Skip count

            int rowSize = Marshal.SizeOf<MIB_IPFORWARDROW>();
            for (int i = 0; i < numEntries; i++)
            {
                var row = Marshal.PtrToStructure<MIB_IPFORWARDROW>(currentPtr);
                routes.Add(row);
                currentPtr += rowSize;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return routes;
    }

    /// <summary>
    /// Gets the interface index for a NetworkInterface.
    /// </summary>
    private static uint GetInterfaceIndex(NetworkInterface ni)
    {
        try
        {
            var ipProps = ni.GetIPProperties();
            var ipv4Props = ipProps.GetIPv4Properties();
            return (uint)ipv4Props.Index;
        }
        catch
        {
            // Try to get from IPv6
            try
            {
                var ipProps = ni.GetIPProperties();
                var ipv6Props = ipProps.GetIPv6Properties();
                return (uint)ipv6Props.Index;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Converts an IP address to a uint in network byte order.
    /// </summary>
    private static uint IpToUint(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        // IP Helper API expects network byte order (big endian)
        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// Gets the NordVPN network adapter if present.
    /// </summary>
    private static NetworkInterface? GetNordVpnAdapter()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            return interfaces.FirstOrDefault(ni =>
                VpnAdapterPatterns.Any(pattern =>
                    ni.Name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    ni.Description.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets information about all active network adapters.
    /// </summary>
    public static IReadOnlyList<(string Name, string Description, OperationalStatus Status, int Index)> GetActiveAdapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(ni => (ni.Name, ni.Description, ni.OperationalStatus, (int)GetInterfaceIndex(ni)))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Gets the current default routes from the routing table.
    /// </summary>
    public static IReadOnlyList<(uint InterfaceIndex, string Gateway, uint Metric)> GetDefaultRoutes()
    {
        var result = new List<(uint, string, uint)>();
        try
        {
            var routes = GetIpForwardTableEntries();
            foreach (var route in routes)
            {
                if (route.dwForwardDest == 0 && route.dwForwardMask == 0)
                {
                    var gateway = new IPAddress(route.dwForwardNextHop).ToString();
                    result.Add((route.dwForwardIfIndex, gateway, route.dwForwardMetric1));
                }
            }
        }
        catch { }
        return result;
    }

    public bool Disconnect(string? installPath = null)
    {
        var path = installPath ?? GetInstallPath();
        if (path == null)
            return false;

        var exePath = Path.Combine(path, "NordVPN.exe");
        ProcessRunner.Run(exePath, "-d", path, 10000);
        return true;
    }

    public (bool success, string? serverName) Connect(string target, string? installPath = null, bool isSpecificServer = false, bool isGroup = false)
    {
        var path = installPath ?? GetInstallPath();
        if (path == null)
            return (false, null);

        var exePath = Path.Combine(path, "NordVPN.exe");
        var args = new List<string> { "-c" };

        if (isSpecificServer)
        {
            args.Add("-n");
            args.Add(target);
        }
        else if (isGroup || !string.IsNullOrEmpty(target))
        {
            args.Add("-g");
            args.Add(target);
        }

        var (exitCode, output, error) = ProcessRunner.Run(
            exePath,
            string.Join(" ", args),
            path,
            15000); // NordVPN connects fast

        var serverName = ExtractServerName(output) ?? target;
        return (true, serverName);
    }

    public IReadOnlyList<string> GetConnectCommand()
    {
        var path = GetInstallPath();
        var exePath = path != null ? Path.Combine(path, "NordVPN.exe") : "NordVPN.exe";
        return [exePath, "-c"];
    }

    public IReadOnlyList<string> GetDisconnectCommand()
    {
        var path = GetInstallPath();
        var exePath = path != null ? Path.Combine(path, "NordVPN.exe") : "NordVPN.exe";
        return [exePath, "-d"];
    }

    private string? GetInstallPath()
    {
        var (installed, path) = CheckInstallation();
        return installed ? path : null;
    }

    private static string? ExtractServerName(string output)
    {
        // Try to extract server name from output if available
        var match = Regex.Match(output, @"(?:connected to|connecting to)\s+(.+?)(?:\s|$)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
