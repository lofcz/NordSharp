using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NordSharp.Internal;

/// <summary>
/// Minimal HTTP helper without external dependencies.
/// </summary>
internal static class HttpHelper
{
    private static readonly Lazy<HttpClient> LazyClient = new Lazy<HttpClient>(() =>
    {
#if NETSTANDARD2_0
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
#endif
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10) // Global timeout
        };

        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("Accept", "application/json,text/html,*/*");

        return client;
    });

    private static HttpClient Client => LazyClient.Value;

    /// <summary>
    /// IPv4-only IP checking service URLs (ordered by reliability/speed).
    /// </summary>
    private static readonly string[] Ipv4CheckUrls =
    [
        "https://api.ipify.org/",
        "https://v4.ident.me/",
        "https://ipv4.icanhazip.com/"
    ];

    /// <summary>
    /// IPv6-only IP checking service URLs.
    /// </summary>
    private static readonly string[] Ipv6CheckUrls =
    [
        "https://api6.ipify.org/",
        "https://v6.ident.me/",
        "https://ipv6.icanhazip.com/"
    ];

    /// <summary>
    /// NordVPN API URL for fetching servers.
    /// </summary>
    public const string NordVpnApiUrl = "https://api.nordvpn.com/v1/servers?limit=0";

    /// <summary>
    /// Performs an HTTP GET request with proper timeout handling.
    /// </summary>
    public static string Get(string url, int timeoutSeconds = 10)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        return GetAsync(url, cts.Token).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Performs an async HTTP GET request with cancellation support.
    /// </summary>
    private static async Task<string> GetAsync(string url, CancellationToken ct)
    {
        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Request to {url} timed out.");
        }
    }

    /// <summary>
    /// Gets the current public IP address (prefers IPv4).
    /// </summary>
    public static string GetCurrentIp(int maxRetries = 2)
    {
        var ipv4 = TryGetIpFromUrlsFast(Ipv4CheckUrls, 3);
        if (ipv4 != null)
            return ipv4;

        throw new InvalidOperationException("Failed to fetch current IP address.");
    }

    /// <summary>
    /// Gets both IPv4 and IPv6 addresses in parallel with fast timeout.
    /// </summary>
    public static (string? Ipv4, string? Ipv6) GetCurrentIpAddresses(int timeoutMs = 3000)
    {
        string? ipv4 = null;
        string? ipv6 = null;

        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            // Run both fetches in parallel
            var ipv4Task = TryGetIpFromUrlsAsync(Ipv4CheckUrls, 2, cts.Token);
            var ipv6Task = TryGetIpFromUrlsAsync(Ipv6CheckUrls, 2, cts.Token);

            // Wait for both with overall timeout
            Task.WhenAll(ipv4Task, ipv6Task).GetAwaiter().GetResult();

            ipv4 = ipv4Task.Result;
            ipv6 = ipv6Task.Result;
        }
        catch (OperationCanceledException)
        {
            // Timeout - return whatever we got
        }
        catch (AggregateException)
        {
            // One or both failed - return whatever we got
        }

        return (ipv4, ipv6);
    }

    /// <summary>
    /// Gets the current public IPv4 address with fast timeout.
    /// </summary>
    public static string? GetCurrentIpv4(int timeoutSeconds = 3)
    {
        return TryGetIpFromUrlsFast(Ipv4CheckUrls, timeoutSeconds);
    }

    /// <summary>
    /// Gets the current public IPv6 address with fast timeout.
    /// </summary>
    public static string? GetCurrentIpv6(int timeoutSeconds = 3)
    {
        return TryGetIpFromUrlsFast(Ipv6CheckUrls, timeoutSeconds);
    }

    /// <summary>
    /// Fetches the raw JSON response from NordVPN server API.
    /// </summary>
    public static string FetchNordVpnServersJson(int maxRetries = 3)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return Get(NordVpnApiUrl, 60);
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < maxRetries - 1)
                    Thread.Sleep(1000 * (attempt + 1));
            }
        }

        throw new InvalidOperationException("Failed to fetch NordVPN server list from API.", lastException);
    }

    /// <summary>
    /// Tries to get an IP from multiple URLs with fast racing (first success wins).
    /// </summary>
    private static string? TryGetIpFromUrlsFast(string[] urls, int timeoutSeconds)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            // Race all URLs - first valid response wins
            var tasks = urls.Select(url => TryGetSingleIpAsync(url, cts.Token)).ToArray();
            
            while (tasks.Length > 0)
            {
                var completedTask = Task.WhenAny(tasks).GetAwaiter().GetResult();
                
                if (completedTask.Status == TaskStatus.RanToCompletion && completedTask.Result != null)
                {
                    cts.Cancel(); // Cancel remaining
                    return completedTask.Result;
                }

                // Remove completed task and continue
                tasks = tasks.Where(t => t != completedTask).ToArray();
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout
        }
        catch
        {
            // All failed
        }

        return null;
    }

    /// <summary>
    /// Async version for parallel fetching.
    /// </summary>
    private static async Task<string?> TryGetIpFromUrlsAsync(string[] urls, int perUrlTimeoutSeconds, CancellationToken ct)
    {
        // Race all URLs
        var tasks = urls.Select(url => TryGetSingleIpAsync(url, ct)).ToList();

        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks).ConfigureAwait(false);

            if (completedTask.Status == TaskStatus.RanToCompletion && completedTask.Result != null)
                return completedTask.Result;

            tasks.Remove(completedTask);
        }

        return null;
    }

    /// <summary>
    /// Tries to get IP from a single URL, returns null on any failure.
    /// </summary>
    private static async Task<string?> TryGetSingleIpAsync(string url, CancellationToken ct)
    {
        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(2)); // Per-URL timeout

            var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ip = content.Trim();

            if (IsValidIpAddress(ip))
                return ip;
        }
        catch
        {
            // Ignore - return null
        }

        return null;
    }

    private static bool IsValidIpAddress(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || ip.Length > 45) // Max IPv6 length
            return false;

        ip = ip.Trim();

        // IPv6 check
        if (ip.Contains(':'))
            return ip.Split(':').Length >= 3 && ip.Length >= 7;

        // IPv4 check
        var parts = ip.Split('.');
        if (parts.Length != 4)
            return false;

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var num) || num < 0 || num > 255)
                return false;
        }

        return true;
    }
}
