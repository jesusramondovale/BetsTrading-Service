namespace BetsTrading.Application.Services;

public static class CountryCodeMapper
{
    private static readonly Dictionary<string, string> CountryNameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        { "United Kingdom", "GB" },
        { "Spain", "ES" },
        { "France", "FR" },
        { "Germany", "DE" },
        { "Italy", "IT" },
        { "United States of America", "US" },
        { "United States", "US" },
        { "Canada", "CA" },
        { "Mexico", "MX" },
        { "Brazil", "BR" },
        { "Argentina", "AR" },
        { "Chile", "CL" },
        { "Colombia", "CO" },
        { "Peru", "PE" },
        { "Portugal", "PT" },
        { "Netherlands", "NL" },
        { "Belgium", "BE" },
        { "Switzerland", "CH" },
        { "Austria", "AT" },
        { "Sweden", "SE" },
        { "Norway", "NO" },
        { "Denmark", "DK" },
        { "Finland", "FI" },
        { "Poland", "PL" },
        { "Greece", "GR" },
        { "Turkey", "TR" },
        { "Russia", "RU" },
        { "Japan", "JP" },
        { "China", "CN" },
        { "India", "IN" },
        { "Australia", "AU" },
        { "New Zealand", "NZ" },
        { "South Africa", "ZA" }
    };

    public static string GetCountryCodeByName(string countryName)
    {
        if (string.IsNullOrWhiteSpace(countryName))
            return "GB"; // Default to UK

        if (CountryNameToCode.TryGetValue(countryName, out var code))
        {
            return code;
        }

        return "GB"; // Default fallback
    }
}
