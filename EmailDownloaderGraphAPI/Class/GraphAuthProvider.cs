using Azure.Identity;
using log4net;
using Microsoft.Graph;

namespace EmailGraphAPI.Classes {
    internal class GraphAuthProvider {
        private readonly AppConfigProps _config;
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program));

        // Konstruktor třídy GraphAuthProvider
        public GraphAuthProvider(AppConfigProps config) {
            _config = config;
        }

        public GraphServiceClient GetAuthenticatedClient() { // Autentizace app vůči MS GraphApi
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var credential = new ClientSecretCredential( // Třída, která se stará o přihlášení do MS - získá token a přidá ho k requestu
                _config.TenantId,
                _config.ClientId,
                _config.ClientSecret
            );

            // Inicializace klienta GraphAPI s danými přihlašovacími údaji a oprávněními
            return new GraphServiceClient(credential, scopes);
        }

        // Metoda pro kontrolu autentizace emailů z config.JSON
        public void AuthenticationMailBoxesCheck() {

            if (!_config.AllowedMailBoxes.Contains(_config.Mailbox)) {
                _log.Error($"Nepovolený pokus o přístup ke schránce: {_config.Mailbox}");

                throw new UnauthorizedAccessException($"Nepovolený přístup k {_config.Mailbox}");
            }
            else {
                _log.Info($"Autorizovaný přístup ke schránce: {_config.Mailbox}");
            }
        }
    }
}