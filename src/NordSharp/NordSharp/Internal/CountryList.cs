using System;
using System.Collections.Generic;

namespace NordSharp.Internal;

/// <summary>
/// Embedded country and region data for NordVPN servers.
/// </summary>
internal static class CountryList
{
    /// <summary>
    /// All available countries.
    /// </summary>
    public static readonly IReadOnlyList<string> Countries =
    [
        // Europe
        "United Kingdom", "Germany", "France", "Netherlands", "Sweden", "Switzerland",
        "Denmark", "Italy", "Norway", "Spain", "Belgium", "Poland", "Ireland",
        "Czech Republic", "Austria", "Finland", "Portugal", "Ukraine", "Serbia",
        "Hungary", "Greece", "Latvia", "Luxembourg", "Romania", "Bulgaria", "Estonia",
        "Slovakia", "Iceland", "Albania", "Cyprus", "Croatia", "Slovenia",
        "Bosnia and Herzegovina", "Georgia", "Moldova", "North Macedonia",
        // Americas
        "United States", "Canada", "Brazil", "Argentina", "Mexico", "Chile", "Costa Rica",
        // Other
        "Australia", "South Africa", "India", "United Arab Emirates", "Israel", "Turkey",
        "Singapore", "Taiwan", "Japan", "Hong Kong", "New Zealand", "Indonesia",
        "Malaysia", "Vietnam", "South Korea", "Thailand"
    ];

    /// <summary>
    /// European countries.
    /// </summary>
    public static readonly IReadOnlyList<string> Europe =
    [
        "United Kingdom", "Germany", "France", "Netherlands", "Sweden", "Switzerland",
        "Denmark", "Italy", "Norway", "Spain", "Belgium", "Poland", "Ireland",
        "Czech Republic", "Austria", "Finland", "Portugal", "Ukraine", "Serbia",
        "Hungary", "Greece", "Latvia", "Luxembourg", "Romania", "Bulgaria", "Estonia",
        "Slovakia", "Iceland", "Albania", "Cyprus", "Croatia", "Slovenia",
        "Bosnia and Herzegovina", "Georgia", "Moldova", "North Macedonia"
    ];

    /// <summary>
    /// Americas countries.
    /// </summary>
    public static readonly IReadOnlyList<string> Americas =
    [
        "United States", "Canada", "Brazil", "Argentina", "Mexico", "Chile", "Costa Rica"
    ];

    /// <summary>
    /// Asia Pacific countries.
    /// </summary>
    public static readonly IReadOnlyList<string> AsiaPacific =
    [
        "Australia", "Singapore", "Taiwan", "Japan", "Hong Kong", "New Zealand",
        "Indonesia", "Malaysia", "Vietnam", "South Korea", "Thailand"
    ];

    /// <summary>
    /// Africa, Middle East, and India.
    /// </summary>
    public static readonly IReadOnlyList<string> AfricaMiddleEastIndia =
    [
        "South Africa", "India", "United Arab Emirates", "Israel", "Turkey"
    ];

    /// <summary>
    /// Australian regions/cities.
    /// </summary>
    public static readonly IReadOnlyList<string> RegionsAustralia =
    [
        "Sydney", "Adelaide", "Brisbane", "Perth", "Melbourne"
    ];

    /// <summary>
    /// Canadian regions/cities.
    /// </summary>
    public static readonly IReadOnlyList<string> RegionsCanada =
    [
        "Vancouver", "Toronto", "Montreal"
    ];

    /// <summary>
    /// German regions/cities.
    /// </summary>
    public static readonly IReadOnlyList<string> RegionsGermany =
    [
        "Frankfurt", "Berlin"
    ];

    /// <summary>
    /// Indian regions/cities.
    /// </summary>
    public static readonly IReadOnlyList<string> RegionsIndia =
    [
        "Mumbai", "Chennai"
    ];

    /// <summary>
    /// United States regions/cities.
    /// </summary>
    public static readonly IReadOnlyList<string> RegionsUnitedStates =
    [
        "Dallas", "Chicago", "Atlanta", "Miami", "Los Angeles", "New York",
        "San Francisco", "Seattle", "Buffalo", "Saint Louis", "Denver",
        "Manassas", "Charlotte", "Salt Lake City", "Phoenix"
    ];

    /// <summary>
    /// Specialty groups.
    /// </summary>
    public static readonly IReadOnlyList<string> SpecialtyGroups =
    [
        "Africa The Middle East And India", "Onion Over VPN", "Asia Pacific",
        "P2P", "Dedicated IP", "Standard VPN Servers", "Double VPN",
        "The Americas", "Europe"
    ];

    /// <summary>
    /// Country code to name mapping.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CountryCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["al"] = "Albania", ["ar"] = "Argentina", ["au"] = "Australia", ["at"] = "Austria",
        ["be"] = "Belgium", ["ba"] = "Bosnia and Herzegovina", ["br"] = "Brazil", ["bg"] = "Bulgaria",
        ["ca"] = "Canada", ["cl"] = "Chile", ["cr"] = "Costa Rica", ["hr"] = "Croatia",
        ["cy"] = "Cyprus", ["cz"] = "Czech Republic", ["dk"] = "Denmark", ["ee"] = "Estonia",
        ["fi"] = "Finland", ["fr"] = "France", ["ge"] = "Georgia", ["de"] = "Germany",
        ["gr"] = "Greece", ["hk"] = "Hong Kong", ["hu"] = "Hungary", ["is"] = "Iceland",
        ["in"] = "India", ["id"] = "Indonesia", ["ie"] = "Ireland", ["il"] = "Israel",
        ["it"] = "Italy", ["jp"] = "Japan", ["lv"] = "Latvia", ["lu"] = "Luxembourg",
        ["my"] = "Malaysia", ["mx"] = "Mexico", ["md"] = "Moldova", ["nl"] = "Netherlands",
        ["nz"] = "New Zealand", ["mk"] = "North Macedonia", ["no"] = "Norway", ["pl"] = "Poland",
        ["pt"] = "Portugal", ["ro"] = "Romania", ["rs"] = "Serbia", ["sg"] = "Singapore",
        ["sk"] = "Slovakia", ["si"] = "Slovenia", ["za"] = "South Africa", ["kr"] = "South Korea",
        ["es"] = "Spain", ["se"] = "Sweden", ["ch"] = "Switzerland", ["tw"] = "Taiwan",
        ["th"] = "Thailand", ["tr"] = "Turkey", ["ua"] = "Ukraine", ["ae"] = "United Arab Emirates",
        ["uk"] = "United Kingdom", ["gb"] = "United Kingdom", ["us"] = "United States",
        ["vn"] = "Vietnam"
    };

    /// <summary>
    /// Gets countries for a named region.
    /// </summary>
    public static IReadOnlyList<string>? GetRegion(string regionName)
    {
        return regionName.ToLowerInvariant() switch
        {
            "europe" => Europe,
            "americas" or "the americas" => Americas,
            "asia pacific" => AsiaPacific,
            "africa east india" or "africa the middle east and india" => AfricaMiddleEastIndia,
            "regions australia" => RegionsAustralia,
            "regions canada" => RegionsCanada,
            "regions germany" => RegionsGermany,
            "regions india" => RegionsIndia,
            "regions united states" => RegionsUnitedStates,
            _ => null
        };
    }

    /// <summary>
    /// Validates that a country or region name is known.
    /// </summary>
    public static bool IsValidLocation(string name)
    {
        var lowerName = name.ToLowerInvariant().Replace("_", " ");
        
        foreach (var country in Countries)
        {
            if (country.Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check regions
        foreach (var region in RegionsAustralia) if (region.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var region in RegionsCanada) if (region.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var region in RegionsGermany) if (region.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var region in RegionsIndia) if (region.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var region in RegionsUnitedStates) if (region.Equals(name, StringComparison.OrdinalIgnoreCase)) return true;

        // Check specialty groups
        foreach (var group in SpecialtyGroups)
        {
            if (group.Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Converts a location name to Linux CLI format (spaces to underscores).
    /// </summary>
    public static string ToLinuxFormat(string location)
    {
        return location.Replace(" ", "_");
    }
}
