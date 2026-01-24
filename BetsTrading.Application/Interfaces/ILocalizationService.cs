namespace BetsTrading.Application.Interfaces;

public interface ILocalizationService
{
    string GetTranslationByCountry(string countryCode, string key);
}
