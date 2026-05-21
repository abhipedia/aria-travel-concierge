using System.Globalization;

namespace TripPlannerV1.Services;

/// <summary>
/// Produces a comprehensive country -> ISO currency map driven by the .NET
/// globalization data, so we don't have to hand-maintain a short list.
/// </summary>
public interface ICountryCurrencyService
{
    IReadOnlyDictionary<string, string> Map { get; }
    IReadOnlyList<string> Countries { get; }
    string? GetCurrency(string? country);
}

public sealed class CountryCurrencyService : ICountryCurrencyService
{
    public CountryCurrencyService()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;
            try { region = new RegionInfo(culture.Name); }
            catch { continue; }

            // EnglishName is the human-readable country name (e.g. "United States").
            // Use TryAdd so the first culture per country wins (they all share currency).
            if (!string.IsNullOrWhiteSpace(region.EnglishName) &&
                !string.IsNullOrWhiteSpace(region.ISOCurrencySymbol))
            {
                map.TryAdd(region.EnglishName, region.ISOCurrencySymbol);
            }
        }

        Map = map;
        Countries = map.Keys.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyDictionary<string, string> Map { get; }
    public IReadOnlyList<string> Countries { get; }

    public string? GetCurrency(string? country) =>
        !string.IsNullOrWhiteSpace(country) && Map.TryGetValue(country.Trim(), out var code)
            ? code
            : null;
}
