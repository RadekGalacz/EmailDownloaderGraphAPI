using Azure.Identity;
using log4net;
using Microsoft.Graph;
using System.Globalization;

namespace EmailGraphAPI.Classes {
    internal class GraphAuthenticator {
        private AppConfigProps AppConfig;
        private static readonly ILog log = LogManager.GetLogger(typeof(Program)); // Inicializace loggeru pro třídu Program (log4net)

        public GraphAuthenticator(AppConfigProps AppConfig) {
            this.AppConfig = AppConfig;
        }

        // HLAVNÍ METODA pro stahování emailů
        //___________________________________

        public async Task DownloadEmail() {

            log.Info("=== Spouštím aplikaci pro stahování e-mailů ===");

            // Kontrola autentizovanách emailových schránek
            AuthenticationEmailsCheck();

            // Oprávnění pro přístup k Microsoft Graph API
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var credential = new ClientSecretCredential( // Autentizace app vůči MS GraphApi
                AppConfig.TenantId,
                AppConfig.ClientId,
                AppConfig.ClientSecret
            );

            // Inicializace klienta GraphAPI s danými přihlašovacími údaji a oprávněními
            var graphClient = new GraphServiceClient(credential, scopes);

            // Načtení seznamu zpráv z inboxu zadané emailové schránky
            var messages = await graphClient.Users[AppConfig.Mailbox]
                .MailFolders["Inbox"]
                .Messages
                .GetAsync();

            // Seřazení zpráv podle data přijetí
            var orderedMessages = messages.Value.OrderBy(msg => msg.ReceivedDateTime).ToList();

            // Vytvoření složky (pokud neexistuje) dle cesty načtené z config.JSON
            CreateFolderForEmails();

            // Načtení uložených Idček emailů z donwloadedEmails.TXT
            List<string> downloadedIds = GetDownloadedIds();

            // Parsoání datumu z config.JSON
            var configStartDate = DateTime.ParseExact(AppConfig.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Cysklus pro každý email ve složce Inbox
            foreach (var msg in orderedMessages) {
                log.Info($"Zpracovávám e-mail s předmětem: '{msg.Subject}");
                var content = await graphClient.Users[AppConfig.Mailbox]
                    .Messages[msg.Id]
                    .Content
                    .GetAsync();

                // Získání datumu přijetí emailů
                var emailReceivedDateTime = msg.ReceivedDateTime?.UtcDateTime;

                if (emailReceivedDateTime == null) continue;

                // Filtrování: jen zprávy od určitého data
                if (emailReceivedDateTime.Value.Date < configStartDate.Date) continue;

                // Vytvoření podsložek s ochranou proti zakázaným znakům
                var subjectName = string.Join("_", msg.Subject.Split(Path.GetInvalidFileNameChars()));
                var subfolderPath = Path.Combine(AppConfig.DownloadPath, subjectName);

                // Pokud složka existuje, přidat  číslování _1 atd.
                int i = 1;
                while (Directory.Exists(subfolderPath)) {
                    subfolderPath = Path.Combine(AppConfig.DownloadPath, subjectName + "_" + i);
                    i++;
                }

                // Pokud podsložky existují, přerušit stahování
                if (downloadedIds.Contains(msg.InternetMessageId)) {
                    log.Info($"Přeskakuji – email už byl dříve stažen (ID: {msg.InternetMessageId})");
                    continue;
                }
                else {

                    Directory.CreateDirectory(subfolderPath);

                    // Uložení emailů do souboru
                    var filePath = Path.Combine(subfolderPath, $"message.eml");
                    using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    await content.CopyToAsync(fs);

                    log.Info($"E-mail úspěšně uložen: {filePath}");
                    DownloadedIds(msg.InternetMessageId);
                }
            }
        }

        // POMOCNÉ METODY
        //___________________________________

        // Pomocná metoda pro vytvoření složky (pokud neexistuje) dle cesty načtené z config.JSON
        public void CreateFolderForEmails() {
            DirectoryInfo di = new DirectoryInfo(AppConfig.DownloadPath);
            int i = 1;

            if (di.Exists) {
                DirectoryInfo[] subfolders = di.GetDirectories();

                string vypis = subfolders.Length == 0 ? "---" : string.Join("\n", subfolders.Select(d => $"[{i++}] {d.Name}")); // Vypíše názvy již stažených podsložek dle předmětů emailů

                Console.WriteLine($"Složka pro ukládání emailu úspěšně načtená {AppConfig.DownloadPath}, aktuálně obsahuje tyto podsložky:\n{vypis}");
            }
            else {
                log.Info($"Vytvářím novou složku pro e-maily: {AppConfig.DownloadPath}");
                di.Create();
            }
        }

        // Pomocná metoda pro ukládání id emailů do souboru donwloadedEmails.TXT
        public void DownloadedIds(string id) {
            if (!string.IsNullOrWhiteSpace(id)) {
                string downloadedPath = Path.Combine(AppConfig.DownloadPath, "downloadedEmails.TXT");
                File.AppendAllText(downloadedPath, id + Environment.NewLine);
                log.Debug($"Zapsáno ID emailu do seznamu stažených: {id}");
            }
        }

        // Pomocná metoda pro načitání id emailů ze souboru donwloadedEmails.TXT
        public List<string> GetDownloadedIds() {
            string downloadedPath = Path.Combine(AppConfig.DownloadPath, "downloadedEmails.TXT");

            if (!File.Exists(downloadedPath)) {
                log.Warn("Soubor downloadedEmails.TXT neexistuje – bude vytvořen při prvním stažení.");
                return new List<string>();  // Soubor neexistuje, přidat pouze prázdný seznam
            }

            string[] lines = File.ReadAllLines(downloadedPath);
            var ids = new List<string>();

            foreach (var line in lines) {
                if (!string.IsNullOrWhiteSpace(line)) {
                    ids.Add(line.Trim());  // Přidat řádky
                }
            }
            return ids;
        }

        // Pomocná metoda pro kontrolu autentizace emailů z config.JSON
        public void AuthenticationEmailsCheck() {

            if (!AppConfig.AllowedMailBoxes.Contains(AppConfig.Mailbox)) {
                log.Error($"Nepovolený pokus o přístup ke schránce: {AppConfig.Mailbox}");

                throw new UnauthorizedAccessException($"Nepovolený přístup k {AppConfig.Mailbox}");
            }
            else {
                log.Info($"Autorizovaný přístup ke schránce: {AppConfig.Mailbox}");
            }
        }
    }
}