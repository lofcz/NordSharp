using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NordSharp.Internal;

/// <summary>
/// Simple JSON parser for NordVPN API responses without external dependencies.
/// Only parses the specific structure needed for server list.
/// </summary>
internal static class SimpleJsonParser
{
    /// <summary>
    /// Parses the NordVPN servers JSON response.
    /// </summary>
    public static List<NordServer> ParseServers(string json)
    {
        var servers = new List<NordServer>();

        // The API returns an array of server objects
        // Each server has: hostname, name, load, groups[], locations[]
        // We need to filter for "Standard VPN servers" group

        var serverMatches = SplitJsonArray(json);

        foreach (var serverJson in serverMatches)
        {
            try
            {
                // Check if this is a Standard VPN server
                if (!IsStandardVpnServer(serverJson))
                    continue;

                var hostname = ExtractStringValue(serverJson, "hostname");
                var name = ExtractStringValue(serverJson, "name");
                var load = ExtractIntValue(serverJson, "load");

                if (string.IsNullOrEmpty(hostname) || string.IsNullOrEmpty(name))
                    continue;

                // Extract identifier from hostname (e.g., "nl742" from "nl742.nordvpn.com")
                // hostname is guaranteed non-null here due to the check above
                var identifier = hostname!.Split('.')[0];
                
                // Extract country code from identifier
                var countryCode = ExtractCountryCode(identifier);
                
                // Get country name from code
                var country = CountryList.CountryCodes.TryGetValue(countryCode, out var countryName)
                    ? countryName
                    : countryCode.ToUpperInvariant();

                // name is guaranteed non-null here due to the check above
                servers.Add(new NordServer(hostname, name!, identifier, countryCode, country, load));
            }
            catch
            {
                // Skip malformed entries
                continue;
            }
        }

        return servers;
    }

    /// <summary>
    /// Splits a JSON array into individual object strings.
    /// </summary>
    private static IEnumerable<string> SplitJsonArray(string json)
    {
        // Find the start of the array
        var startIndex = json.IndexOf('[');
        if (startIndex < 0)
            yield break;

        var depth = 0;
        var objectStart = -1;
        var inString = false;
        var escape = false;

        for (int i = startIndex; i < json.Length; i++)
        {
            var c = json[i];

            if (escape)
            {
                escape = false;
                continue;
            }

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '{')
            {
                if (depth == 0)
                    objectStart = i;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && objectStart >= 0)
                {
                    yield return json.Substring(objectStart, i - objectStart + 1);
                    objectStart = -1;
                }
            }
        }
    }

    /// <summary>
    /// Checks if a server JSON object is a Standard VPN server.
    /// </summary>
    private static bool IsStandardVpnServer(string serverJson)
    {
        // Look for groups array containing "Standard VPN servers"
        return serverJson.Contains("\"Standard VPN servers\"") ||
               serverJson.Contains("\"title\":\"Standard VPN servers\"");
    }

    /// <summary>
    /// Extracts a string value for a given key from JSON.
    /// </summary>
    private static string? ExtractStringValue(string json, string key)
    {
        // Pattern: "key":"value" or "key": "value"
        var pattern = $"\"{key}\"\\s*:\\s*\"([^\"]*)\"";
        var match = Regex.Match(json, pattern);
        return match.Success ? UnescapeJsonString(match.Groups[1].Value) : null;
    }

    /// <summary>
    /// Extracts an integer value for a given key from JSON.
    /// </summary>
    private static int ExtractIntValue(string json, string key)
    {
        // Pattern: "key":123 or "key": 123
        var pattern = $"\"{key}\"\\s*:\\s*(\\d+)";
        var match = Regex.Match(json, pattern);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }

    /// <summary>
    /// Extracts country code from server identifier (e.g., "nl" from "nl742").
    /// </summary>
    private static string ExtractCountryCode(string identifier)
    {
        var sb = new StringBuilder();
        foreach (var c in identifier)
        {
            if (char.IsLetter(c))
                sb.Append(c);
            else
                break;
        }
        return sb.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Unescapes a JSON string value.
    /// </summary>
    private static string UnescapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\")
            .Replace("\\/", "/")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t");
    }
}
