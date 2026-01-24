using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.Services;

public class LocalizationService : ILocalizationService
{
    private static readonly Dictionary<string, Dictionary<string, string>> LocalizedTextsDictionary = new()
    {
        { "GB", new Dictionary<string, string>()
            {
                { "emailSubjectUserVerified", "Account verified" },
                { "userVerifiedEmailBody", "Hello {0},\n\nYour account has been successfully verified.\n\nYou now have full access to Betrader features and can start enjoying the platform without restrictions.\n\nIf you did not complete this verification, please contact our support team immediately.\n\nBest regards,\nBetrader Support Team" },
                { "emailSubjectPayment", "Betrader payment" },
                { "withdrawalEmailBody", "Hello {0},\n\nAn amount of {1} {2} has been withdrawn from your account.\n\nWithdrawal method: {3}\nLocation: {4}, {5}, {6}\n\nIf this was not you, please contact support immediately.\n\nBest regards,\nBetrader Support Team" }
            }
        },
        { "UK", new Dictionary<string, string>()
            {
                { "emailSubjectUserVerified", "Account verified" },
                { "userVerifiedEmailBody", "Hello {0},\n\nYour account has been successfully verified.\n\nYou now have full access to Betrader features and can start enjoying the platform without restrictions.\n\nIf you did not complete this verification, please contact our support team immediately.\n\nBest regards,\nBetrader Support Team" },
                { "emailSubjectPayment", "Betrader payment" },
                { "withdrawalEmailBody", "Hello {0},\n\nAn amount of {1} {2} has been withdrawn from your account.\n\nWithdrawal method: {3}\nLocation: {4}, {5}, {6}\n\nIf this was not you, please contact support immediately.\n\nBest regards,\nBetrader Support Team" }
            }
        },
        { "ES", new Dictionary<string, string>()
            {
                { "emailSubjectUserVerified", "Cuenta verificada" },
                { "userVerifiedEmailBody", "Hola {0},\n\nTu cuenta ha sido verificada correctamente.\n\nAhora tienes acceso completo a todas las funciones de Betrader y puedes disfrutar de la plataforma sin restricciones.\n\nSi no has completado esta verificación, contacta con el soporte de Betrader inmediatamente.\n\nUn saludo,\nEquipo de soporte de Betrader" },
                { "emailSubjectPayment", "Pago de Betrader" },
                { "withdrawalEmailBody", "Hola {0},\n\nSe ha retirado una cantidad de {1} {2} de su cuenta.\n\nMétodo de retirada: {3}\nLocalización: {4}, {5}, {6}\n\nSi no ha sido usted, póngase en contacto con el soporte inmediatamente.\n\nUn saludo,\nEquipo de soporte de Betrader" }
            }
        },
        { "FR", new Dictionary<string, string>()
            {
                { "emailSubjectUserVerified", "Compte vérifié" },
                { "userVerifiedEmailBody", "Bonjour {0},\n\nVotre compte a été vérifié avec succès.\n\nVous avez désormais un accès complet à toutes les fonctionnalités de Betrader et pouvez profiter de la plateforme sans restrictions.\n\nSi vous n'êtes pas à l'origine de cette vérification, veuillez contacter immédiatement notre service d'assistance.\n\nCordialement,\nÉquipe de support Betrader" },
                { "emailSubjectPayment", "Paiement Betrader" },
                { "withdrawalEmailBody", "Bonjour {0},\n\nUn montant de {1} {2} a été retiré de votre compte.\n\nMéthode de retrait : {3}\nLocalisation : {4}, {5}, {6}\n\nSi ce n'était pas vous, veuillez contacter le support immédiatement.\n\nCordialement,\nÉquipe de support Betrader" }
            }
        },
        { "IT", new Dictionary<string, string>()
            {
                { "emailSubjectUserVerified", "Account verificato" },
                { "userVerifiedEmailBody", "Ciao {0},\n\nIl tuo account è stato verificato con successo.\n\nOra hai pieno accesso a tutte le funzionalità di Betrader e puoi utilizzare la piattaforma senza restrizioni.\n\nSe non hai effettuato tu questa verifica, contatta immediatamente il supporto di Betrader.\n\nCordiali saluti,\nTeam di supporto Betrader" },
                { "emailSubjectPayment", "Pagamento Betrader" },
                { "withdrawalEmailBody", "Ciao {0},\n\nUn importo di {1} {2} è stato prelevato dal tuo account.\n\nMetodo di prelievo: {3}\nLocalizzazione: {4}, {5}, {6}\n\nSe non sei stato tu, contatta subito il supporto.\n\nCordiali saluti,\nTeam di supporto Betrader" }
            }
        },
        { "DE", new Dictionary<string, string>()
            {
                { "emailSubjectUserVerified", "Konto verifiziert" },
                { "userVerifiedEmailBody", "Hallo {0},\n\nIhr Konto wurde erfolgreich verifiziert.\n\nSie haben nun vollen Zugriff auf alle Funktionen von Betrader und können die Plattform uneingeschränkt nutzen.\n\nWenn Sie diese Verifizierung nicht selbst vorgenommen haben, wenden Sie sich bitte umgehend an den Betrader-Support.\n\nMit freundlichen Grüßen,\nBetrader Support Team" },
                { "emailSubjectPayment", "Betrader-Zahlung" },
                { "withdrawalEmailBody", "Hallo {0},\n\nEin Betrag von {1} {2} wurde von Ihrem Konto abgebucht.\n\nAuszahlungsmethode: {3}\nStandort: {4}, {5}, {6}\n\nWenn Sie das nicht waren, wenden Sie sich bitte sofort an den Support.\n\nMit freundlichen Grüßen,\nBetrader Support Team" }
            }
        },
        { "PT", new Dictionary<string, string>()
            {
                { "emailSubjectUserVerified", "Conta verificada" },
                { "userVerifiedEmailBody", "Olá {0},\n\nSua conta foi verificada com sucesso.\n\nAgora você tem acesso total a todos os recursos do Betrader e pode aproveitar a plataforma sem restrições.\n\nSe você não realizou esta verificação, entre em contato imediatamente com o suporte da Betrader.\n\nAtenciosamente,\nEquipe de suporte Betrader" },
                { "emailSubjectPayment", "Pagamento Betrader" },
                { "withdrawalEmailBody", "Olá {0},\n\nUm valor de {1} {2} foi retirado da sua conta.\n\nMétodo de retirada: {3}\nLocalização: {4}, {5}, {6}\n\nSe não foi você, entre em contato com o suporte imediatamente.\n\nAtenciosamente,\nEquipe de suporte Betrader" }
            }
        }
    };

    public string GetTranslationByCountry(string countryCode, string key)
    {
        // Normalize country code
        var normalizedCode = countryCode?.ToUpperInvariant() ?? "GB";
        
        // Try to get translation for the country
        if (LocalizedTextsDictionary.TryGetValue(normalizedCode, out var translations))
        {
            if (translations.TryGetValue(key, out var translation))
            {
                return translation;
            }
        }

        // Fallback to GB (English)
        if (LocalizedTextsDictionary.TryGetValue("GB", out var defaultTranslations))
        {
            if (defaultTranslations.TryGetValue(key, out var defaultTranslation))
            {
                return defaultTranslation;
            }
        }

        return key; // Return key if translation not found
    }
}
