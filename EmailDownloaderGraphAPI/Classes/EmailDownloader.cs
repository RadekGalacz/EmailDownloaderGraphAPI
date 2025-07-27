using log4net;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EmailGraphAPI.Classes {
    internal class EmailDownloader {
        private AppConfigProps config = new AppConfigProps();
        private static readonly ILog log = LogManager.GetLogger(typeof(Program)); // Inicializace loggeru pro třídu Program (log4net)
        private readonly GraphAuthProvider graphAuthProvider;
        private GraphServiceClient graphClient; // Graph API pro poskytování metod pro práci s emaily

        // Konstruktor třídy EmailDownloader
        public EmailDownloader(GraphAuthProvider graphAuthProvide, AppConfigProps config) {
            this.graphAuthProvider = graphAuthProvide;
            this.config = config;
        }

        // Metoda pro kontrolu autentizace emailů z config.JSON
        public void AuthenticationMailBoxesCheck() {

            if (!config.AllowedMailBoxes.Contains(config.Mailbox)) {
                log.Error($"Nepovolený pokus o přístup ke schránce: {config.Mailbox}");

                throw new UnauthorizedAccessException($"Nepovolený přístup k {config.Mailbox}");
            }
            else {
                log.Info($"Autorizovaný přístup ke schránce: {config.Mailbox}");
            }
        }

        // Metoda vrací seřazené emaily v pořadí, v jakém přišly
        public async Task<List<Microsoft.Graph.Models.Message>> LoadEmailsAsync() {

            log.Info("=== Spouštím aplikaci pro stahování e-mailů ===");

            // Inicializace Graph API klienta (pokud ještě není inicializován)
            if (graphClient == null) {
                graphClient = graphAuthProvider.GetAuthenticatedClient();
            }

            List<Microsoft.Graph.Models.Message> allMessages = new List<Microsoft.Graph.Models.Message>();

            // Načtení seznamu zpráv z inboxu zadané emailové schránky

            var messages = await graphClient.Users[config.Mailbox]
                .MailFolders["Inbox"]
                .Messages
                .GetAsync(requestConfiguration => {
                    requestConfiguration.QueryParameters.Top = config.EmailPageSize; // První požadavek na API na počet e-mailů dle config.JSON
                    requestConfiguration.QueryParameters.Select = new[] { "id", "sender", "subject", "body", "receivedDateTime", "attachments", "internetMessageId" };
                    requestConfiguration.QueryParameters.Orderby = new[] { "receivedDateTime" }; // Seřazení podle data přijetí
                    requestConfiguration.Headers.Add("Prefer", "outlook.body-content-type=\"text\"");
                });
            if (messages == null) {
                log.Info("Žádné zprávy nebyly nalezeny.");
                return allMessages;
            }
            await ProcessEmailPagesAsync(messages, allMessages);
            log.Info($"Celkem načteno {allMessages.Count} e-mailů.");
            return allMessages.OrderBy(msg => msg.ReceivedDateTime).ToList();
        }

        // Asynchronní metoda pro zpracování emailů pomocí strákování
        // https://learn.microsoft.com/en-us/graph/sdks/paging?tabs=csharp
        public async Task ProcessEmailPagesAsync(MessageCollectionResponse messages, List<Microsoft.Graph.Models.Message> allMessages) {
            // Vytvoření PageIterator pro stránkování
            var pageIterator = PageIterator<Microsoft.Graph.Models.Message, MessageCollectionResponse>
                .CreatePageIterator(
                    graphClient,
                    messages,
                    // Callback pro zpracování každé zprávy
                    (msg) => {
                        allMessages.Add(msg); // Přidání zprávy do seznamu
                        log.Info($"Načten e-mail: {msg.Subject}");
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

        // Metoda pro vytvoření složky (pokud neexistuje) dle cesty načtené z config.JSON
        public void CreateFolderForEmails() {
            DirectoryInfo di = new DirectoryInfo(config.DownloadPath);
            int i = 1;

            // Kontrola, zda složka už existuje
            if (di.Exists) {
                DirectoryInfo[] subfolders = di.GetDirectories();

                string vypis = subfolders.Length == 0 ? "---" : string.Join("\n", subfolders.Select(d => $"[{i++}] {d.Name}")); // Vypíše názvy již stažených podsložek dle předmětů emailů

                log.Info($"Složka pro ukládání emailu úspěšně načtená {config.DownloadPath}, aktuálně obsahuje tyto podsložky:\n{vypis}");
            }
            else {
                // Pokud složka neexistuje, votvořit ji
                log.Info($"Vytvářím novou složku pro e-maily: {config.DownloadPath}");
                di.Create();
            }
        }

        // Asynchronní metoda pro ukládání id emailů do souboru donwloadedEmails.TXT
        public async Task SaveIdsToFile(string id) {
            if (!string.IsNullOrWhiteSpace(id)) {
                // Uložení ID emailo do souboru downloadedEmails.TXT pro zamezení opětovného stažení
                string downloadedPath = Path.Combine(config.DownloadPath, "downloadedEmails.txt");
                await File.AppendAllTextAsync(downloadedPath, id + Environment.NewLine);
                log.Debug($"Zapsáno ID emailu do seznamu stažených: {id}");
            }
        }

        // Asynchronní metoda pro načitání id emailů ze souboru donwloadedEmails.TXT
        public async Task<List<string>> GetSavedIds() {
            string downloadedPath = Path.Combine(config.DownloadPath, "downloadedEmails.txt");

            if (!File.Exists(downloadedPath)) {
                log.Warn("Soubor downloadedEmails.txt neexistuje – bude vytvořen při prvním stažení.");
                return new List<string>();  // Soubor neexistuje, přidat pouze prázdný seznam
            }
            try {
                string[] lines = File.ReadAllLines(downloadedPath);
                var ids = new List<string>();

                // Přidání ID emailů načtených z .TXT souboru
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

        // Metoda pro vytvoření unikátních názvu podsložek
        public async Task<string> CreateUniqueFolderPath(string basePath, string subject) {
            // Oříznutí délky názvu předmětu na maxLength
            var maxLength = 100;
            if (subject.Length > maxLength) {
                subject = subject.Substring(0, maxLength);
            }
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

        // HLAVNÍ METODA pro ukládání emailů do složek
        public async Task DownloadEmails() {

            try {
                // Ověření přístupu podle "AllowedMailBoxes" z config .json
                AuthenticationMailBoxesCheck();

                var orderedMessages = await LoadEmailsAsync(); // Přiřazení metody načtených emaliů do proměnné

                // Vytvoření Hlavní složky pro emaily - podle config.JSON
                CreateFolderForEmails();

                // Cysklus pro každý email ve složce Inbox
                foreach (var msg in orderedMessages) {
                    log.Info($"Zpracovávám e-mail s předmětem: '{msg.Subject}'");

                    // Content pro uložení celéhoobsahu zprávy - včetně příloh
                    var content = await graphClient.Users[config.Mailbox]
                        .Messages[msg.Id]
                        .Content
                        .GetAsync();

                    // Získání datumu přijetí emailů
                    var emailReceivedDateTime = msg.ReceivedDateTime?.UtcDateTime;

                    if (emailReceivedDateTime == null) continue;

                    // Filtrování: jen zprávy od určitého data
                    if (emailReceivedDateTime.Value.Date < config.StartDate) {
                        log.Info("E-mail je starší než povolené datum, přeskakuji...");
                        continue;
                    }

                    var subfolderPath = await CreateUniqueFolderPath(config.DownloadPath, msg.Subject);  // Přiřazení metody pro vytvoření unikátních názvů složek do proměnné
                    var savedIDs = await GetSavedIds(); // Přiřazení metody pro načtení ID uložených z .TXT do proměnné

                    // Pokud podsložky existují, přerušit stahování
                    if (savedIDs.Contains(msg.InternetMessageId)) {
                        log.Info($"Přeskakuji – email už byl dříve stažen (ID: {msg.InternetMessageId})");
                        continue;
                    }
                    else {

                        try {
                            //Pokus o vytvoření složky
                            Directory.CreateDirectory(subfolderPath);
                        }
                        catch (IOException ex) {
                            log.Error($"Chyba při vytváření složky {subfolderPath}: {ex.Message}", ex);
                        }

                        // Uložení emailů do souboru ve formátu .eml
                        var filePath = Path.Combine(subfolderPath, $"message.eml");
                        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                        await content.CopyToAsync(fs);

                        log.Info($"E-mail úspěšně uložen: {filePath}");

                        // Přidání ID emailu do seznamu stažených emailů, aby se nestahoval znovu.
                        await SaveIdsToFile(msg.InternetMessageId);
                    }
                }
            }
            catch (Exception ex) {
                log.Fatal("Chyba při stahování e-mailů", ex);
            }
        }
    }
}