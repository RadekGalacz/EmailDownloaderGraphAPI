using EmailGraphAPI.Classes;
using EmailGraphAPI.Interface;
using log4net;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EmailGraphAPI.Class {
    internal class EmailOperations : IEmailOperations {

        private static readonly ILog _log = LogManager.GetLogger(typeof(EmailOperations));
        private GraphServiceClient _graphClient;
        private readonly GraphAuthProvider _graphAuthProvider;
        private AppConfigProps _config;

        public EmailOperations(GraphAuthProvider graphAuthProvide, AppConfigProps config) {
            _graphAuthProvider = graphAuthProvide;
            _config = config;
        }

        // Metoda vrací seřazené emaily v pořadí, v jakém přišly
        public async Task<List<Message>> LoadEmailsAsync() {

            _log.Info("=== Spouštím aplikaci pro stahování e-mailů ===");

            // Inicializace Graph API klienta (pokud ještě není inicializován)
            if (_graphClient == null) {
                _graphClient = _graphAuthProvider.GetAuthenticatedClient();
            }

            List<Message> allMessages = new List<Message>();

            // Načtení seznamu zpráv z inboxu zadané emailové schránky
            var messages = await _graphClient.Users[_config.Mailbox]
                .MailFolders["Inbox"]
                .Messages
                .GetAsync(requestConfiguration => {
                    requestConfiguration.QueryParameters.Top = _config.EmailPageSize; // První požadavek na API na počet e-mailů dle config.JSON
                    requestConfiguration.QueryParameters.Select = new[] { "id", "sender", "subject", "body", "receivedDateTime", "attachments", "internetMessageId" };
                    requestConfiguration.QueryParameters.Orderby = new[] { "receivedDateTime" }; // Seřazení podle data přijetí
                    requestConfiguration.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");
                });
            if (messages == null) {
                _log.Info("Žádné zprávy nebyly nalezeny.");
                return allMessages;
            }
            await ProcessEmailPagesAsync(messages, allMessages);
            _log.Info($"Celkem načteno {allMessages.Count} e-mailů.");
            return allMessages.OrderBy(msg => msg.ReceivedDateTime).ToList();
        }

        // Asynchronní metoda pro zpracování emailů pomocí stránkování
        // https://learn.microsoft.com/en-us/graph/sdks/paging?tabs=csharp
        public async Task ProcessEmailPagesAsync(MessageCollectionResponse messages, List<Message> allMessages) {
            // Vytvoření PageIterator pro stránkování
            var pageIterator = PageIterator<Message, MessageCollectionResponse>
                .CreatePageIterator(
                    _graphClient,
                    messages,
                    // Callback pro zpracování každé zprávy
                    (msg) => {
                        allMessages.Add(msg); // Přidání zprávy do seznamu
                        _log.Info($"Načten e-mail: {msg.Subject}");
                        return true; // Pokračovat v iteraci
                    },
                    // Konfigurace dalších požadavků
                    (req) => {
                        req.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");
                        return req;
                    });

            // Spuštění iterace přes všechny stránky
            await pageIterator.IterateAsync();
        }
    }
}
