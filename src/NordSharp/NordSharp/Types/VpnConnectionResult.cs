namespace NordSharp;

/// <summary>
/// Result of a VPN connection or rotation operation.
/// </summary>
public sealed class VpnConnectionResult
{
    /// <summary>
    /// Gets whether the connection was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the new IP address after connection.
    /// </summary>
    public string? NewIp { get; }

    /// <summary>
    /// Gets the previous IP address before connection.
    /// </summary>
    public string? PreviousIp { get; }

    /// <summary>
    /// Gets the server that was connected to.
    /// </summary>
    public string? Server { get; }

    /// <summary>
    /// Gets the error message if the connection failed.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the number of retry attempts made.
    /// </summary>
    public int Attempts { get; }

    private VpnConnectionResult(bool success, string? newIp, string? previousIp, string? server, string? error, int attempts)
    {
        Success = success;
        NewIp = newIp;
        PreviousIp = previousIp;
        Server = server;
        Error = error;
        Attempts = attempts;
    }

    /// <summary>
    /// Creates a successful connection result.
    /// </summary>
    internal static VpnConnectionResult Succeeded(string newIp, string previousIp, string server, int attempts)
    {
        return new VpnConnectionResult(true, newIp, previousIp, server, null, attempts);
    }

    /// <summary>
    /// Creates a failed connection result.
    /// </summary>
    internal static VpnConnectionResult Failed(string error, string? previousIp = null, int attempts = 0)
    {
        return new VpnConnectionResult(false, null, previousIp, null, error, attempts);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Success)
            return $"Connected to {Server} - IP: {NewIp}";
        return $"Failed: {Error}";
    }
}
