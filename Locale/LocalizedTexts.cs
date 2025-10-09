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
            { "emailCodeSentBody", "Hello ,\n\nYour verification code is: {0}\n\nPlease enter this code in the app to complete your registration.\n\nIf you did not request this, please ignore this message.\n\nBest regards,\nBetrader Support Team" },
            { "updatedTrends", "Updated trends!" },
            { "youWon", "You've won {0} points on {1}!" },
            { "sessionStartedElsewhere", "Session started on another device" },
            { "resetPasswordEmailBody", "Hello {0},\n\nA new password has been generated for your account.\n\nYour new password is: {1}\n\nFor security reasons, please log in and change this password as soon as possible.\n\nBest regards,\nBetrader Support Team" },
            { "withdrawalEmailBody", "Hello {0},\n\nAn amount of {1} {2} has been withdrawn from your account.\n\nWithdrawal method: {3}\nLocation: {4}, {5}, {6}\n\nIf this was not you, please contact support immediately.\n\nBest regards,\nBetrader Support Team" },
            { "newPasswordEmailBody", "Hello {0},\n\nYour account password has been successfully changed.\n\nIf you did not request this change, contact Betrader support immediately.\n\nBest regards,\nBetrader Support Team" }
        }
      },
      { "es", new Dictionary<string, string>()
        {      
            { "emailCodeSentBody", "Hola,\n\nTu código de verificación es: {0}\n\nIntroduce este código en la aplicación para completar tu registro.\n\nSi no has solicitado este código, simplemente ignora este mensaje.\n\nAtentamente,\nEquipo de soporte de Betrader" },
            { "updatedTrends", "Tendencias actualizadas!" },
            { "youWon", "¡Has ganado {0} puntos en {1}!" },
            { "sessionStartedElsewhere", "Sesión iniciada en otro dispositivo" },
            { "resetPasswordEmailBody", "Hola {0},\n\nSe ha generado una nueva contraseña para su cuenta.\n\nSu nueva contraseña es: {1}\n\nPor razones de seguridad, inicie sesión y cambie esta contraseña lo antes posible.\n\nUn saludo,\nEquipo de soporte de Betrader" },
            { "withdrawalEmailBody", "Hola {0},\n\nSe ha retirado una cantidad de {1} {2} de su cuenta.\n\nMétodo de retirada: {3}\nLocalización: {4}, {5}, {6}\n\nSi no ha sido usted, póngase en contacto con el soporte inmediatamente.\n\nUn saludo,\nEquipo de soporte de Betrader" },
            { "newPasswordEmailBody", "Hola {0},\n\nLa contraseña de su cuenta se ha actualizado correctamente.\n\nSi usted no ha solicitado este cambio, póngase en contacto inmediatamente con el soporte de Betrader.\n\nUn saludo,\nEquipo de soporte de Betrader" }
        }
      },
      { "fr", new Dictionary<string, string>()
        {
            { "emailCodeSentBody", "Bonjour,\n\nVotre code de vérification est : {0}\n\nVeuillez saisir ce code dans l'application pour finaliser votre inscription.\n\nSi vous n'avez pas demandé ce code, ignorez simplement ce message.\n\nCordialement,\nÉquipe du support Betrader" },
            { "updatedTrends", "Tendances mises à jour !" },
            { "youWon", "Vous avez gagné {0} points sur {1} !" },
            { "sessionStartedElsewhere", "Session démarrée sur un autre appareil" },
            { "resetPasswordEmailBody", "Bonjour {0},\n\nUn nouveau mot de passe a été généré pour votre compte.\n\nVotre nouveau mot de passe est : {1}\n\nPour des raisons de sécurité, veuillez vous connecter et changer ce mot de passe dès que possible.\n\nCordialement,\nÉquipe de support Betrader" },
            { "withdrawalEmailBody", "Bonjour {0},\n\nUn montant de {1} {2} a été retiré de votre compte.\n\nMéthode de retrait : {3}\nLocalisation : {4}, {5}, {6}\n\nSi ce n'était pas vous, veuillez contacter le support immédiatement.\n\nCordialement,\nÉquipe de support Betrader" },
            { "newPasswordEmailBody", "Bonjour {0},\n\nLe mot de passe de votre compte a été modifié avec succès.\n\nSi vous n'êtes pas à l'origine de ce changement, contactez immédiatement le support de Betrader.\n\nCordialement,\nÉquipe de support Betrader" }
        }
      },
      { "it", new Dictionary<string, string>()
        {
            { "emailCodeSentBody", "Ciao,\n\nIl tuo codice di verifica è: {0}\n\nInserisci questo codice nell'app per completare la registrazione.\n\nSe non hai richiesto questo codice, ignora semplicemente questo messaggio.\n\nCordiali saluti,\nTeam di supporto Betrader" },
            { "updatedTrends", "Tendenze aggiornate!" },
            { "youWon", "Hai vinto {0} punti su {1}!" },
            { "sessionStartedElsewhere", "Sessione avviata su un altro dispositivo" },
            { "resetPasswordEmailBody", "Ciao {0},\n\nÈ stata generata una nuova password per il tuo account.\n\nLa tua nuova password è: {1}\n\nPer motivi di sicurezza, accedi e cambia questa password il prima possibile.\n\nCordiali saluti,\nTeam di supporto Betrader" },
            { "withdrawalEmailBody", "Ciao {0},\n\nUn importo di {1} {2} è stato prelevato dal tuo account.\n\nMetodo di prelievo: {3}\nLocalizzazione: {4}, {5}, {6}\n\nSe non sei stato tu, contatta subito il supporto.\n\nCordiali saluti,\nTeam di supporto Betrader" },
            { "newPasswordEmailBody", "Ciao {0},\n\nLa password del tuo account è stata modificata con successo.\n\nSe non sei stato tu a richiedere questo cambiamento, contatta immediatamente il supporto di Betrader.\n\nCordiali saluti,\nTeam di supporto Betrader" }
        }
      },
      { "de", new Dictionary<string, string>()
        {
            { "emailCodeSentBody", "Hallo,\n\nIhr Bestätigungscode lautet: {0}\n\nBitte geben Sie diesen Code in der App ein, um Ihre Registrierung abzuschließen.\n\nWenn Sie diesen Code nicht angefordert haben, ignorieren Sie bitte diese Nachricht.\n\nMit freundlichen Grüßen,\nBetrader Support Team" },
            { "updatedTrends", "Aktualisierte Trends!" },
            { "youWon", "Du hast {0} Punkte auf {1} gewonnen!" },
            { "sessionStartedElsewhere", "Sitzung auf einem anderen Gerät gestartet" },
            { "resetPasswordEmailBody", "Hallo {0},\n\nEin neues Passwort wurde für Ihr Konto erstellt.\n\nIhr neues Passwort lautet: {1}\n\nAus Sicherheitsgründen melden Sie sich bitte an und ändern Sie dieses Passwort so schnell wie möglich.\n\nMit freundlichen Grüßen,\nBetrader Support Team" },
            { "withdrawalEmailBody", "Hallo {0},\n\nEin Betrag von {1} {2} wurde von Ihrem Konto abgebucht.\n\nAuszahlungsmethode: {3}\nStandort: {4}, {5}, {6}\n\nWenn Sie das nicht waren, wenden Sie sich bitte sofort an den Support.\n\nMit freundlichen Grüßen,\nBetrader Support Team" },
            { "newPasswordEmailBody", "Hallo {0},\n\nDas Passwort Ihres Kontos wurde erfolgreich geändert.\n\nWenn Sie diese Änderung nicht selbst veranlasst haben, wenden Sie sich bitte umgehend an den Betrader-Support.\n\nMit freundlichen Grüßen,\nBetrader Support Team" }
        }
      },
      { "pt", new Dictionary<string, string>()
        {
            { "emailCodeSentBody", "Olá,\n\nO seu código de verificação é: {0}\n\nInsira este código no aplicativo para concluir o seu registro.\n\nSe não solicitou este código, ignore esta mensagem.\n\nAtenciosamente,\nEquipe de suporte Betrader" },
            { "updatedTrends", "Tendências atualizadas!" },
            { "youWon", "Você ganhou {0} pontos em {1}!" },
            { "sessionStartedElsewhere", "Sessão iniciada em outro dispositivo" },
            { "resetPasswordEmailBody", "Olá {0},\n\nUma nova senha foi gerada para a sua conta.\n\nA sua nova senha é: {1}\n\nPor motivos de segurança, faça login e altere esta senha quanto antes.\n\nAtenciosamente,\nEquipe de suporte Betrader" },
            { "withdrawalEmailBody", "Olá {0},\n\nUm valor de {1} {2} foi retirado da sua conta.\n\nMétodo de retirada: {3}\nLocalização: {4}, {5}, {6}\n\nSe não foi você, entre em contato com o suporte imediatamente.\n\nAtenciosamente,\nEquipe de suporte Betrader" },
            { "newPasswordEmailBody", "Olá {0},\n\nA senha da sua conta foi alterada com sucesso.\n\nA sua nova senha é: {1}\n\nPor favor, exclua esta mensagem imediatamente após a leitura. Se você não solicitou esta alteração, entre em contato imediatamente com o suporte da Betrader.\n\nAtenciosamente,\nEquipe de suporte Betrader" }
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
