using log4net;
using Microsoft.Graph;
using System.Globalization;

namespace EmailGraphAPI.Classes {
    internal class EmailDownloader {
        private AppConfigProps config = new AppConfigProps();
        private static readonly ILog log = LogManager.GetLogger(typeof(Program)); // Inicializace loggeru pro třídu Program (log4net)
        private readonly GraphAuthProvider graphAuthProvider;
        private GraphServiceClient graphClient; // Graph API

        // Konstruktor třídy EmailDownloader
        public EmailDownloader( GraphAuthProvider graphAuthProvide, AppConfigProps config) {
            this.graphAuthProvider = graphAuthProvide;
            this.config = config;
        }

        // HLAVNÍ METODA pro stahování emailů
        //___________________________________

        public async Task DownloadEmails() {

            try {
                log.Info("=== Spouštím aplikaci pro stahování e-mailů ===");

                // Kontrola autentizovanách emailových schránek
                AuthenticationMailBoxesCheck();

                // Inicializace Graph API klienta (pokud ještě není inicializován)
                if (graphClient == null) {
                    graphClient = graphAuthProvider.GetAuthenticatedClient();
                }

                // Načtení seznamu zpráv z inboxu zadané emailové schránky

                var messages = await graphClient.Users[config.Mailbox]
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
                var configStartDate = DateTime.ParseExact(config.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

                // Cysklus pro každý email ve složce Inbox
                foreach (var msg in orderedMessages) {
                    log.Info($"Zpracovávám e-mail s předmětem: '{msg.Subject}'");
                    var content = await graphClient.Users[config.Mailbox]
                        .Messages[msg.Id]
                        .Content
                        .GetAsync();

                    // Získání datumu přijetí emailů
                    var emailReceivedDateTime = msg.ReceivedDateTime?.UtcDateTime;

                    if (emailReceivedDateTime == null) continue;

                    // Filtrování: jen zprávy od určitého data
                    if (emailReceivedDateTime.Value.Date < configStartDate.Date) {
                        log.Info("E-mail je starší než povolené datum, přeskakuji...");
                        continue;
                    }

                    // Metoda pro vytvoření unikátních složek
                    var subfolderPath = CreateUniqueFolderPath(config.DownloadPath, msg.Subject);  // Přiřazení výstupu do proměnné subfolderPath

                    // Pokud podsložky existují, přerušit stahování
                    if (downloadedIds.Contains(msg.InternetMessageId)) {
                        log.Info($"Přeskakuji – email už byl dříve stažen (ID: {msg.InternetMessageId})");
                        continue;
                    }
                    else {

                        try {
                            Directory.CreateDirectory(subfolderPath);
                        }
                        catch (IOException ex) {
                            log.Error($"Chyba při vytváření složky {subfolderPath}: {ex.Message}", ex);
                        }

                        // Uložení emailů do souboru
                        var filePath = Path.Combine(subfolderPath, $"message.eml");
                        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                        await content.CopyToAsync(fs);

                        log.Info($"E-mail úspěšně uložen: {filePath}");
                        DownloadedIds(msg.InternetMessageId);
                    }
                }
            }
            catch (Exception ex) {
                log.Fatal("Chyba při stahování e-mailů", ex);
            }
        }

        // POMOCNÉ METODY
        //___________________________________

        // Pomocná metoda pro vytvoření složky (pokud neexistuje) dle cesty načtené z config.JSON
        public void CreateFolderForEmails() {
            DirectoryInfo di = new DirectoryInfo(config.DownloadPath);
            int i = 1;

            if (di.Exists) {
                DirectoryInfo[] subfolders = di.GetDirectories();

                string vypis = subfolders.Length == 0 ? "---" : string.Join("\n", subfolders.Select(d => $"[{i++}] {d.Name}")); // Vypíše názvy již stažených podsložek dle předmětů emailů

                log.Info($"Složka pro ukládání emailu úspěšně načtená {config.DownloadPath}, aktuálně obsahuje tyto podsložky:\n{vypis}");
            }
            else {
                log.Info($"Vytvářím novou složku pro e-maily: {config.DownloadPath}");
                di.Create();
            }
        }

        // Pomocná metoda pro vytvoření unikátních podsložek
        public string CreateUniqueFolderPath(string basePath, string subject) {
            // Vytvoření podsložek s ochranou proti zakázaným znakům
            var subjectName = string.Join("_", subject.Split(Path.GetInvalidFileNameChars()));
            var subfolderPath = Path.Combine(basePath, subjectName);

            // Pokud složka existuje, přidat  číslování _1 atd.
            int i = 1;
            while (Directory.Exists(subfolderPath)) {
                subfolderPath = Path.Combine(config.DownloadPath, subjectName + "_" + i);
                i++;
            }
            return subfolderPath;
        }

        // Pomocná metoda pro ukládání id emailů do souboru donwloadedEmails.TXT
        public void DownloadedIds(string id) {
            if (!string.IsNullOrWhiteSpace(id)) {
                string downloadedPath = Path.Combine(config.DownloadPath, "downloadedEmails.TXT");
                File.AppendAllText(downloadedPath, id + Environment.NewLine);
                log.Debug($"Zapsáno ID emailu do seznamu stažených: {id}");
            }
        }

        // Pomocná metoda pro načitání id emailů ze souboru donwloadedEmails.TXT
        public List<string> GetDownloadedIds() {
            string downloadedPath = Path.Combine(config.DownloadPath, "downloadedEmails.TXT");

            if (!File.Exists(downloadedPath)) {
                log.Warn("Soubor downloadedEmails.TXT neexistuje – bude vytvořen při prvním stažení.");
                return new List<string>();  // Soubor neexistuje, přidat pouze prázdný seznam
            }
            try {
                string[] lines = File.ReadAllLines(downloadedPath);
                var ids = new List<string>();

                foreach (var line in lines) {
                    if (!string.IsNullOrWhiteSpace(line)) {
                        ids.Add(line.Trim());  // Přidat řádky
                    }
                }
                return ids;
            }
            catch (IOException ex) {
                log.Error($"Chyba při čtení souboru downloadedEmails.TXT: {ex.Message}", ex);
                throw;
            }
        }

        // Pomocná metoda pro kontrolu autentizace emailů z config.JSON
        public void AuthenticationMailBoxesCheck() {

            if (!config.AllowedMailBoxes.Contains(config.Mailbox)) {
                log.Error($"Nepovolený pokus o přístup ke schránce: {config.Mailbox}");

                throw new UnauthorizedAccessException($"Nepovolený přístup k {config.Mailbox}");
            }
            else {
                log.Info($"Autorizovaný přístup ke schránce: {config.Mailbox}");
            }
        }
    }
}