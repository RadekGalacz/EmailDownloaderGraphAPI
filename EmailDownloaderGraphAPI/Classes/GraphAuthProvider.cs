using Azure.Identity;
using Microsoft.Graph;

namespace EmailGraphAPI.Classes {
    internal class GraphAuthProvider {
        private readonly AppConfigProps _config;

        // Konstruktor třídy GraphAuthProvider
        public GraphAuthProvider(AppConfigProps config) {
            this._config = config;
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
    }
}
