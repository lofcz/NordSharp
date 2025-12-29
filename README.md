[![NordSharp](https://shields.io/nuget/v/NordSharp?v=302&icon=nuget&label=NordSharp)](https://www.nuget.org/packages/NordSharp)
[![License:MIT](https://img.shields.io/badge/License-MIT-34D058.svg)](https://opensource.org/license/mit)

# NordSharp

Cross-platform NordVPN library and command-line tool for .NET.

## Features

- **Cross-platform**: Works on Windows, Linux, and macOS
- **Thread-safe**: Only one VPN rotation can occur at a time
- **No external dependencies**: Uses only .NET Standard 2.0 base class library
- **Full API support**: Connects to NordVPN API to fetch server list

## Library Usage

Install the library:

```
dotnet add package NordSharp
```

Use the static `NordVpn` class:

```csharp
using NordSharp;

// Quick connect - simplest usage
var result = NordVpn.Rotate();
Console.WriteLine($"New IP: {result.NewIp}");

// Or with specific countries
var settings = NordVpn.Initialize(VpnOptions.ForCountries("United States", "Germany", "France"));
var result = NordVpn.Rotate(settings);

// Or complete rotation (fetches all 4000+ servers)
var settings = NordVpn.Initialize(VpnOptions.ForCompleteRotation());

// Disconnect
NordVpn.Terminate();

// Get current IP
var ip = NordVpn.GetCurrentIp();

// Save/load settings for reuse
NordVpn.SaveSettings(settings, "settings.ini");
var loaded = NordVpn.LoadSettings("settings.ini");
```

## CLI Usage

```bash
# Initialize with countries and save settings
nordsharp-cli init --countries US,DE,FR --save

# Initialize with complete rotation
nordsharp-cli init --complete --save

# Rotate to new server
nordsharp-cli rotate

# Disconnect
nordsharp-cli disconnect

# Check current IP
nordsharp-cli status

# List servers
nordsharp-cli servers --country US --limit 20
```

## Building

```bash
# Build the solution
cd src/NordSharp
dotnet build NordSharp.slnx

# Publish CLI as single-file (trimmed)
cd NordSharp.Cli
dotnet publish -c Release -r win-x64    # Windows
dotnet publish -c Release -r linux-x64  # Linux
dotnet publish -c Release -r osx-x64    # macOS

# Publish with AOT (requires C++ build tools)
dotnet publish -c Release -r win-x64 -p:PublishAot=true
```

## API Reference

### NordVpn Static Class

| Method | Description |
|--------|-------------|
| `Rotate()` | Quick connect to best server |
| `Rotate(VpnSettings)` | Connect using specific settings |
| `RotateAsync(CancellationToken)` | Async quick connect |
| `RotateAsync(VpnSettings, CancellationToken)` | Async connect with settings |
| `Initialize(VpnOptions?)` | Sets up VPN with the specified options |
| `Terminate(VpnSettings?)` | Disconnects from VPN |
| `GetCurrentIp()` | Returns current public IP address |
| `FetchServers()` | Fetches all servers from NordVPN API |
| `SaveSettings(VpnSettings, string)` | Saves settings to INI file |
| `LoadSettings(string)` | Loads settings from INI file |

### VpnOptions Factory Methods

| Method | Description |
|--------|-------------|
| `ForCountries(params string[])` | Connect to specific countries |
| `ForServers(params string[])` | Connect to specific server IDs |
| `ForCompleteRotation()` | Fetch and rotate through all servers |
| `ForQuickConnect()` | Let NordVPN pick the best server |
| `ForRegion(string)` | Connect to a region (europe, americas, etc.) |

## License

This library is licensed under the MIT license.
