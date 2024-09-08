using System;
using System.Collections.Generic;

namespace BetsTrading_Service.Locale
{
  

  public class LocalizedTexts
  {
    public static Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>()
    {
        { "en", new Dictionary<string, string>()
            {
                { "updatedTrends", "Updated trends!" }
            }
        },
        { "es", new Dictionary<string, string>()
            {
                { "updatedTrends", "Tendencias actualizadas!" }
            }
        },
        { "fr", new Dictionary<string, string>()
            {
                { "updatedTrends", "Tendances mises à jour!" }
            }
        },
        { "it", new Dictionary<string, string>()
            {
                { "updatedTrends", "Tendenze aggiornate!" }
            }
        },
        { "de", new Dictionary<string, string>()
            {
                { "updatedTrends", "Aktualisierte Trends!" }
            }
        }
    };

    public static string GetTranslationByCountry(string countryCode, string key)
    {
     
      Dictionary<string, string> countryToLanguageMap = new Dictionary<string, string>()
    {
        { "US", "en" },
        { "UK", "en" },
        { "ES", "es" },
        { "IT", "it" },
        { "DE", "de" },
        { "FR", "fr" },
    };

      countryCode = countryCode.ToUpper();
      string languageCode = countryToLanguageMap.ContainsKey(countryCode) ? countryToLanguageMap[countryCode] : "en";
      return GetTranslation(languageCode, key);
    }

    public static string GetTranslation(string languageCode, string key)
    {
      if (Translations.ContainsKey(languageCode) && Translations[languageCode].ContainsKey(key))
      {
        return Translations[languageCode][key];
      }
      return key;
    }
  }

}
