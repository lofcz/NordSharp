using NordSharp;

namespace NordSharp.Cli;

/// <summary>
/// NordSharp CLI application.
/// </summary>
internal static class Program
{
    private const string DefaultSettingsFile = "nordsharp_settings.ini";

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "init" => HandleInit(args),
                "rotate" => HandleRotate(args),
                "disconnect" or "d" or "terminate" => HandleDisconnect(),
                "status" => HandleStatus(),
                "servers" => HandleServers(args),
                "help" or "-h" or "--help" => PrintUsage(),
                "version" or "-v" or "--version" => PrintVersion(),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            WriteError(ex.Message);
            return 1;
        }
    }

    private static int HandleInit(string[] args)
    {
        var options = new VpnOptions();
        string? savePath = null;

        for (int i = 1; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();
            
            switch (arg)
            {
                case "--countries" or "-c":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("--countries requires a comma-separated list of countries.");
                        return 1;
                    }
                    options.Countries = args[++i].Split(',')
                        .Select(c => c.Trim())
                        .Where(c => !string.IsNullOrEmpty(c))
                        .ToArray();
                    break;

                case "--servers" or "-s":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("--servers requires a comma-separated list of server IDs.");
                        return 1;
                    }
                    options.Servers = args[++i].Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
                    break;

                case "--complete" or "--all":
                    options.CompleteRotation = true;
                    break;

                case "--quick" or "-q":
                    options.QuickConnect = true;
                    break;

                case "--region" or "-r":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("--region requires a region name.");
                        return 1;
                    }
                    options.Region = args[++i];
                    break;

                case "--group" or "-g":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("--group requires a specialty group name.");
                        return 1;
                    }
                    options.SpecialtyGroup = args[++i];
                    break;

                case "--save":
                    savePath = i + 1 < args.Length && !args[i + 1].StartsWith("-")
                        ? args[++i]
                        : DefaultSettingsFile;
                    break;

                case "--path" or "-p":
                    if (i + 1 >= args.Length)
                    {
                        WriteError("--path requires a NordVPN installation path.");
                        return 1;
                    }
                    options.CustomInstallPath = args[++i];
                    break;

                default:
                    WriteError($"Unknown option: {arg}");
                    return 1;
            }
        }

        WriteInfo("Initializing NordVPN...");
        WriteInfo("Performing system check...");

        var settings = NordVpn.Initialize(options);

        WriteSuccess($"Platform: {settings.Platform}");
        WriteSuccess($"Original IP: {settings.OriginalIp}");
        WriteSuccess($"Servers configured: {(settings.QuickConnect ? "Quick Connect" : settings.Servers.Count.ToString())}");

        if (!string.IsNullOrEmpty(savePath))
        {
            NordVpn.SaveSettings(settings, savePath);
            WriteSuccess($"Settings saved to: {savePath}");
        }

        WriteSuccess("Initialization complete!");
        return 0;
    }

    private static int HandleRotate(string[] args)
    {
        string? settingsPath = null;
        bool autoInit = false;

        for (int i = 1; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();
            
            if (arg is "--settings" or "-s")
            {
                if (i + 1 >= args.Length)
                {
                    WriteError("--settings requires a file path.");
                    return 1;
                }
                settingsPath = args[++i];
            }
        }

        VpnSettings settings;

        if (!string.IsNullOrEmpty(settingsPath))
        {
            settings = NordVpn.LoadSettings(settingsPath);
        }
        else if (File.Exists(DefaultSettingsFile))
        {
            settings = NordVpn.LoadSettings(DefaultSettingsFile);
        }
        else
        {
            WriteInfo("No settings found. Auto-initializing.");
            settings = NordVpn.Initialize(VpnOptions.ForQuickConnect());
            
            NordVpn.SaveSettings(settings, DefaultSettingsFile);
            WriteSuccess($"Settings saved to: {DefaultSettingsFile}");
        }

        WriteInfo("Rotating VPN connection...");

        var result = NordVpn.Rotate(settings);

        if (result.Success)
        {
            WriteSuccess($"New IP: {result.NewIp}");
            return 0;
        }

        WriteError($"Rotation failed: {result.Error}");
        return 1;
    }

    private static int HandleDisconnect()
    {
        WriteInfo("Disconnecting from VPN...");
        
        VpnSettings? settings = null;
        if (File.Exists(DefaultSettingsFile))
        {
            try
            {
                settings = NordVpn.LoadSettings(DefaultSettingsFile);
            }
            catch
            {
                // Ignore - will use default platform
            }
        }

        NordVpn.Terminate(settings);
        WriteSuccess("Disconnected.");
        return 0;
    }

    private static int HandleStatus()
    {
        try
        {
            // Check connection status first (fast, local check)
            var isConnected = NordVpn.IsConnected();
            
            Console.WriteLine();
            if (isConnected)
            {
                WriteSuccess("NordVPN Status: Connected");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("NordVPN Status: Disconnected");
                Console.ResetColor();
            }

            // Fetch IPs (with timeout protection)
            Console.Write("Fetching IP addresses... ");
            var (ipv4, ipv6) = NordVpn.GetCurrentIpAddresses();
            Console.WriteLine("done");
            
            // Display IP addresses
            if (ipv4 != null)
                Console.WriteLine($"Current IPv4: {ipv4}");
            if (ipv6 != null)
                Console.WriteLine($"Current IPv6: {ipv6}");
            if (ipv4 == null && ipv6 == null)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("Current IP: Unable to determine (network issue?)");
                Console.ResetColor();
            }

            // Show original IP if settings exist and connected
            if (isConnected && File.Exists(DefaultSettingsFile))
            {
                try
                {
                    var settings = NordVpn.LoadSettings(DefaultSettingsFile);
                    var currentIp = ipv4 ?? ipv6;
                    if (currentIp != null && !string.Equals(settings.OriginalIp, currentIp, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"Original IP: {settings.OriginalIp}");
                        Console.ResetColor();
                    }
                }
                catch { /* ignore */ }
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            WriteError($"Failed to get status: {ex.Message}");
            return 1;
        }
    }

    private static int HandleServers(string[] args)
    {
        string? countryFilter = null;
        int limit = 50;

        for (int i = 1; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();
            
            if (arg is "--country" or "-c")
            {
                if (i + 1 >= args.Length)
                {
                    WriteError("--country requires a country code.");
                    return 1;
                }
                countryFilter = args[++i].ToLowerInvariant();
            }
            else if (arg is "--limit" or "-l")
            {
                if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out limit))
                {
                    WriteError("--limit requires a number.");
                    return 1;
                }
                i++;
            }
        }

        WriteInfo("Fetching server list from NordVPN API...");
        
        var servers = NordVpn.FetchServers();
        
        if (!string.IsNullOrEmpty(countryFilter))
        {
            servers = servers
                .Where(s => s.CountryCode.Equals(countryFilter, StringComparison.OrdinalIgnoreCase) ||
                           s.Country.Contains(countryFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        WriteSuccess($"Found {servers.Count} servers.");
        Console.WriteLine();

        var displayed = servers.Take(limit).ToList();
        
        Console.WriteLine("ID         | Country              | Load");
        Console.WriteLine("-----------|----------------------|-----");
        
        foreach (var server in displayed)
        {
            var id = server.Identifier.PadRight(10);
            var country = server.Country.Length > 20 
                ? server.Country.Substring(0, 17) + "..." 
                : server.Country.PadRight(20);
            Console.WriteLine($"{id} | {country} | {server.Load}%");
        }

        if (servers.Count > limit)
        {
            Console.WriteLine($"... and {servers.Count - limit} more. Use --limit to see more.");
        }

        return 0;
    }

    private static int PrintUsage()
    {
        Console.WriteLine(@"
NordSharp - Cross-platform NordVPN CLI wrapper

USAGE:
    nordsharp <command> [options]

COMMANDS:
    init        Initialize VPN settings
    rotate      Connect to a new server
    disconnect  Disconnect from VPN (aliases: d, terminate)
    status      Show current IP address
    servers     List available NordVPN servers
    help        Show this help message
    version     Show version information

INIT OPTIONS:
    --countries, -c <list>   Comma-separated list of countries (e.g., US,DE,FR)
    --servers, -s <list>     Comma-separated list of server IDs (e.g., nl742,be166)
    --complete, --all        Use complete rotation (fetch all 4000+ servers)
    --quick, -q              Use quick connect (let NordVPN pick best server)
    --region, -r <name>      Connect to a region (europe, americas, asia pacific)
    --group, -g <name>       Connect to specialty group (P2P, Double VPN, etc.)
    --save [path]            Save settings to file (default: nordsharp_settings.ini)
    --path, -p <path>        Custom NordVPN installation path (Windows only)

ROTATE OPTIONS:
    --settings, -s <path>    Path to settings file

SERVERS OPTIONS:
    --country, -c <code>     Filter by country code (e.g., US, DE)
    --limit, -l <num>        Maximum servers to display (default: 50)

EXAMPLES:
    nordsharp init --countries US,DE,FR --save
    nordsharp init --complete --save settings.ini
    nordsharp rotate
    nordsharp rotate --settings settings.ini
    nordsharp disconnect
    nordsharp status
    nordsharp servers --country US --limit 20
");
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine("NordSharp v1.0.0");
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        WriteError($"Unknown command: {command}");
        Console.WriteLine("Run 'nordsharp help' for usage information.");
        return 1;
    }

    private static void WriteInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Error: {message}");
        Console.ResetColor();
    }
}
