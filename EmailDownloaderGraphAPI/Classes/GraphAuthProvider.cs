using Azure.Identity;
using Microsoft.Graph;

namespace EmailGraphAPI.Classes {
    internal class GraphAuthProvider {
        private readonly AppConfigProps config;

        // Konstruktor třídy GraphAuthProvider
        public GraphAuthProvider(AppConfigProps config) {
            this.config = config;
        }

        public GraphServiceClient GetAuthenticatedClient() { // Autentizace app vůči MS GraphApi
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var credential = new ClientSecretCredential(
                config.TenantId,
                config.ClientId,
                config.ClientSecret
            );

            // Inicializace klienta GraphAPI s danými přihlašovacími údaji a oprávněními
            return new GraphServiceClient(credential, scopes);
        }
    }
}
