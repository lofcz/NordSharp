using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NordSharp.Internal;

/// <summary>
/// Simple INI file parser for settings persistence.
/// </summary>
internal static class IniFile
{
    private const string SectionName = "NordSharp";

    /// <summary>
    /// Saves VpnSettings to an INI file.
    /// </summary>
    public static void Save(VpnSettings settings, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{SectionName}]");
        sb.AppendLine($"Platform={settings.Platform}");
        sb.AppendLine($"OriginalIp={settings.OriginalIp}");
        
        if (!string.IsNullOrEmpty(settings.InstallPath))
            sb.AppendLine($"InstallPath={settings.InstallPath}");
        
        if (settings.QuickConnect)
            sb.AppendLine("QuickConnect=true");
        
        if (!string.IsNullOrEmpty(settings.SpecialtyGroup))
            sb.AppendLine($"SpecialtyGroup={settings.SpecialtyGroup}");
        
        if (settings.Servers.Count > 0)
            sb.AppendLine($"Servers={string.Join(",", settings.Servers)}");
        
        if (settings.BaseCommand.Count > 0)
            sb.AppendLine($"BaseCommand={string.Join(" ", settings.BaseCommand)}");

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Loads VpnSettings from an INI file.
    /// </summary>
    public static VpnSettings Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Settings file not found: {path}");

        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var inSection = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                continue;

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                var section = trimmed.Substring(1, trimmed.Length - 2);
                inSection = section.Equals(SectionName, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection)
                continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = trimmed.Substring(0, eqIndex).Trim();
                var value = trimmed.Substring(eqIndex + 1).Trim();
                values[key] = value;
            }
        }

        // Parse platform
        if (!values.TryGetValue("Platform", out var platformStr) ||
            !Enum.TryParse<VpnPlatform>(platformStr, true, out var platform))
        {
            platform = VpnSettings.GetCurrentPlatform();
        }

        // Parse original IP
        if (!values.TryGetValue("OriginalIp", out var originalIp))
            throw new InvalidOperationException("Settings file is missing OriginalIp.");

        // Parse optional values
        values.TryGetValue("InstallPath", out var installPath);
        values.TryGetValue("SpecialtyGroup", out var specialtyGroup);

        var quickConnect = values.TryGetValue("QuickConnect", out var qc) &&
                          bool.TryParse(qc, out var qcBool) && qcBool;

        // Parse servers list
        var servers = new List<string>();
        if (values.TryGetValue("Servers", out var serversStr) && !string.IsNullOrEmpty(serversStr))
        {
            servers.AddRange(serversStr.Split([','], StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim()));
        }

        // Parse base command
        var baseCommand = new List<string>();
        if (values.TryGetValue("BaseCommand", out var cmdStr) && !string.IsNullOrEmpty(cmdStr))
        {
            baseCommand.AddRange(cmdStr.Split([' '], StringSplitOptions.RemoveEmptyEntries));
        }
        else
        {
            // Default base command based on platform
            baseCommand.AddRange(platform == VpnPlatform.Windows
                ? new[] { "nordvpn", "-c" }
                : new[] { "nordvpn", "c" });
        }

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
