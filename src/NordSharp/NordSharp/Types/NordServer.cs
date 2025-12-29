using System;

namespace NordSharp;

/// <summary>
/// Represents a NordVPN server.
/// </summary>
public sealed class NordServer
{
    /// <summary>
    /// Gets the server hostname (e.g., "nl742.nordvpn.com").
    /// </summary>
    public string Hostname { get; }

    /// <summary>
    /// Gets the display name (e.g., "Netherlands #742").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the short identifier for CLI use (e.g., "nl742").
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Gets the country code (e.g., "nl").
    /// </summary>
    public string CountryCode { get; }

    /// <summary>
    /// Gets the country name (e.g., "Netherlands").
    /// </summary>
    public string Country { get; }

    /// <summary>
    /// Gets the server load percentage (0-100).
    /// </summary>
    public int Load { get; }

    /// <summary>
    /// Creates a new NordServer instance.
    /// </summary>
    public NordServer(string hostname, string name, string identifier, string countryCode, string country, int load)
    {
        Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        CountryCode = countryCode ?? throw new ArgumentNullException(nameof(countryCode));
        Country = country ?? throw new ArgumentNullException(nameof(country));
        Load = load;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Identifier})";
}
