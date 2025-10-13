namespace BetsTrading_Service.Locale
{
  

  public class LocalizedTexts
  {
    private static readonly Dictionary<string, Dictionary<string, string>> localizedTextsDictionary = new()
    {
      { "en", new Dictionary<string, string>()
        {
            { "emailSubjectUserVerified", "Account verified" },
            { "userVerifiedEmailBody", "Hello {0},\n\nYour account has been successfully verified.\n\nYou now have full access to Betrader features and can start enjoying the platform without restrictions.\n\nIf you did not complete this verification, please contact our support team immediately.\n\nBest regards,\nBetrader Support Team" },
            { "emailSubjectCode", "Betrader code" },
            { "emailSubjectWelcome", "Welcome to Betrader" },
            { "emailSubjectPassword", "Betrader password" },
            { "emailSubjectPayment", "Betrader payment" },
            { "registrationSuccessfullEmailBody", "Hello {0},\n\nWelcome to Betrader!\n\nYour registration has been successfully completed and your account is now active.\n\nWe’re excited to have you on board. Explore the markets, follow your favorite assets, and start trading smarter today.\n\nIf you didn’t create this account, please contact our support team immediately.\n\nBest regards,\nBetrader Support Team" },
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
            { "emailSubjectUserVerified", "Cuenta verificada" },
            { "userVerifiedEmailBody", "Hola {0},\n\nTu cuenta ha sido verificada correctamente.\n\nAhora tienes acceso completo a todas las funciones de Betrader y puedes disfrutar de la plataforma sin restricciones.\n\nSi no has completado esta verificación, contacta con el soporte de Betrader inmediatamente.\n\nUn saludo,\nEquipo de soporte de Betrader" },
            { "emailSubjectCode", "Código de Betrader" },
            { "emailSubjectWelcome", "Bienvenido a Betrader" },
            { "emailSubjectPassword", "Contraseña de Betrader" },
            { "emailSubjectPayment", "Pago de Betrader" },
            { "registrationSuccessfullEmailBody", "Hola {0},\n\n¡Bienvenido a Betrader!\n\nTu registro se ha completado correctamente y tu cuenta ya está activa.\n\nNos alegra tenerte con nosotros. Explora los mercados, sigue tus activos favoritos y empieza a operar de forma más inteligente hoy mismo.\n\nSi no has creado esta cuenta, contacta con el soporte de Betrader inmediatamente.\n\nUn saludo,\nEquipo de soporte de Betrader" },
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
            { "emailSubjectUserVerified", "Compte vérifié" },
            { "userVerifiedEmailBody", "Bonjour {0},\n\nVotre compte a été vérifié avec succès.\n\nVous avez désormais un accès complet à toutes les fonctionnalités de Betrader et pouvez profiter de la plateforme sans restrictions.\n\nSi vous n’êtes pas à l’origine de cette vérification, veuillez contacter immédiatement notre service d’assistance.\n\nCordialement,\nÉquipe de support Betrader" },
            { "emailSubjectCode", "Code Betrader" },
            { "emailSubjectWelcome", "Bienvenue chez Betrader" },
            { "emailSubjectPassword", "Mot de passe Betrader" },
            { "emailSubjectPayment", "Paiement Betrader" },
            { "registrationSuccessfullEmailBody", "Bonjour {0},\n\nBienvenue chez Betrader !\n\nVotre inscription a été complétée avec succès et votre compte est désormais actif.\n\nNous sommes ravis de vous compter parmi nous. Explorez les marchés, suivez vos actifs préférés et commencez à trader plus intelligemment dès aujourd’hui.\n\nSi vous n’avez pas créé ce compte, veuillez contacter immédiatement notre service d’assistance.\n\nCordialement,\nÉquipe de support Betrader" },
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
            { "emailSubjectUserVerified", "Account verificato" },
            { "userVerifiedEmailBody", "Ciao {0},\n\nIl tuo account è stato verificato con successo.\n\nOra hai pieno accesso a tutte le funzionalità di Betrader e puoi utilizzare la piattaforma senza restrizioni.\n\nSe non hai effettuato tu questa verifica, contatta immediatamente il supporto di Betrader.\n\nCordiali saluti,\nTeam di supporto Betrader" },
            { "emailSubjectCode", "Codice Betrader" },
            { "emailSubjectWelcome", "Benvenuto su Betrader" },
            { "emailSubjectPassword", "Password Betrader" },
            { "emailSubjectPayment", "Pagamento Betrader" },
            { "registrationSuccessfullEmailBody", "Ciao {0},\n\nBenvenuto su Betrader!\n\nLa tua registrazione è stata completata con successo e il tuo account è ora attivo.\n\nSiamo felici di averti con noi. Esplora i mercati, segui i tuoi asset preferiti e inizia a fare trading in modo più intelligente da oggi.\n\nSe non hai creato questo account, contatta immediatamente il supporto Betrader.\n\nCordiali saluti,\nTeam di supporto Betrader" },
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
            { "emailSubjectUserVerified", "Konto verifiziert" },
            { "userVerifiedEmailBody", "Hallo {0},\n\nIhr Konto wurde erfolgreich verifiziert.\n\nSie haben nun vollen Zugriff auf alle Funktionen von Betrader und können die Plattform uneingeschränkt nutzen.\n\nWenn Sie diese Verifizierung nicht selbst vorgenommen haben, wenden Sie sich bitte umgehend an den Betrader-Support.\n\nMit freundlichen Grüßen,\nBetrader Support Team" },
            { "emailSubjectCode", "Betrader-Code" },
            { "emailSubjectWelcome", "Willkommen bei Betrader" },
            { "emailSubjectPassword", "Betrader-Passwort" },
            { "emailSubjectPayment", "Betrader-Zahlung" },
            { "registrationSuccessfullEmailBody", "Hallo {0},\n\nWillkommen bei Betrader!\n\nIhre Registrierung wurde erfolgreich abgeschlossen und Ihr Konto ist jetzt aktiv.\n\nWir freuen uns, Sie an Bord zu haben. Entdecken Sie die Märkte, verfolgen Sie Ihre Lieblingswerte und handeln Sie ab heute klüger.\n\nWenn Sie dieses Konto nicht erstellt haben, wenden Sie sich bitte umgehend an den Betrader-Support.\n\nMit freundlichen Grüßen,\nBetrader Support Team" },
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
            { "emailSubjectUserVerified", "Conta verificada" },
            { "userVerifiedEmailBody", "Olá {0},\n\nSua conta foi verificada com sucesso.\n\nAgora você tem acesso total a todos os recursos do Betrader e pode aproveitar a plataforma sem restrições.\n\nSe você não realizou esta verificação, entre em contato imediatamente com o suporte da Betrader.\n\nAtenciosamente,\nEquipe de suporte Betrader" },
            { "emailSubjectCode", "Código Betrader" },
            { "emailSubjectWelcome", "Bem-vindo ao Betrader" },
            { "emailSubjectPassword", "Senha Betrader" },
            { "emailSubjectPayment", "Pagamento Betrader" },
            { "registrationSuccessfullEmailBody", "Olá {0},\n\nBem-vindo ao Betrader!\n\nSeu registro foi concluído com sucesso e sua conta já está ativa.\n\nEstamos felizes em tê-lo conosco. Explore os mercados, acompanhe seus ativos favoritos e comece a negociar de forma mais inteligente hoje mesmo.\n\nSe você não criou esta conta, entre em contato imediatamente com o suporte da Betrader.\n\nAtenciosamente,\nEquipe de suporte Betrader" },
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
    public static Dictionary<string, Dictionary<string, string>> Translations = localizedTextsDictionary;

    public static string GetTranslationByCountry(string countryCode, string key)
    {
     
      Dictionary<string, string> countryToLanguageMap = new()
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
      string languageCode = countryToLanguageMap.TryGetValue(countryCode, out string? value) ? value : "en";
      return GetTranslation(languageCode, key);
    }

    public static string GetTranslation(string languageCode, string key)
    {
      if (Translations.TryGetValue(languageCode, out Dictionary<string, string>? value) && value.ContainsKey(key))
      {
        return value[key];
      }
      return key;
    }
  }

}
