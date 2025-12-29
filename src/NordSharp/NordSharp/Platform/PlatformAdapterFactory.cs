using System;

namespace NordSharp.Platform;

/// <summary>
/// Factory for creating platform-specific adapters.
/// </summary>
internal static class PlatformAdapterFactory
{
    /// <summary>
    /// Creates an adapter for the current platform.
    /// </summary>
    public static IPlatformAdapter Create()
    {
        var platform = VpnSettings.GetCurrentPlatform();
        return Create(platform);
    }

    /// <summary>
    /// Creates an adapter for the specified platform.
    /// </summary>
    public static IPlatformAdapter Create(VpnPlatform platform)
    {
        return platform switch
        {
            VpnPlatform.Windows => new WindowsAdapter(),
            VpnPlatform.Linux => new UnixAdapter(VpnPlatform.Linux),
            VpnPlatform.MacOS => new UnixAdapter(VpnPlatform.MacOS),
            _ => throw new PlatformNotSupportedException($"Platform {platform} is not supported.")
        };
    }
}
