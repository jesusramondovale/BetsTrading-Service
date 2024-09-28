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
              { "updatedTrends", "Updated trends!" },
              { "youWon", "You've won {0} points on {1}!" }
        }
            },
        { "es", new Dictionary<string, string>()
            {
                { "updatedTrends", "Tendencias actualizadas!" },
                { "youWon", "¡Has ganado {0} puntos en {1}!" }
            }
        },
        { "fr", new Dictionary<string, string>()
            {
                { "updatedTrends", "Tendances mises à jour!" },
                { "youWon", "Vous avez gagné {0} points sur {1} !" }
            }
        },
        { "it", new Dictionary<string, string>()
            {
                { "updatedTrends", "Tendenze aggiornate!" },
                { "youWon", "Hai vinto {0} punti su {1}!" }
            }
        },
        { "de", new Dictionary<string, string>()
            {
                { "updatedTrends", "Aktualisierte Trends!" },
                { "youWon", "Du hast {0} Punkte auf {1} gewonnen!" }
            }
        },
        { "pt", new Dictionary<string, string>()
            {
                { "updatedTrends", "Tendências atualizadas!" },
                { "youWon", "Você ganhou {0} pontos em {1}!" }
            }
        },


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
        { "PT", "pt" },
        { "BR", "pt" },
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
