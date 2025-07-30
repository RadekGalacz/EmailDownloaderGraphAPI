using log4net;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EmailGraphAPI.Classes {
    internal class EmailDownloader {
        private AppConfigProps _config = new AppConfigProps();
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program)); // Inicializace loggeru pro třídu Program (log4net)
        private readonly GraphAuthProvider _graphAuthProvider;
        private GraphServiceClient _graphClient; // Graph API pro poskytování metod pro práci s emaily

        // Konstruktor třídy EmailDownloader
        public EmailDownloader(GraphAuthProvider graphAuthProvide, AppConfigProps config) {
            this._graphAuthProvider = graphAuthProvide;
            this._config = config;
        }

        // Metoda pro kontrolu autentizace emailů z config.JSON
        private void AuthenticationMailBoxesCheck() {

            if (!_config.AllowedMailBoxes.Contains(_config.Mailbox)) {
                _log.Error($"Nepovolený pokus o přístup ke schránce: {_config.Mailbox}");

                throw new UnauthorizedAccessException($"Nepovolený přístup k {_config.Mailbox}");
            }
            else {
                _log.Info($"Autorizovaný přístup ke schránce: {_config.Mailbox}");
            }
        }

        // Metoda vrací seřazené emaily v pořadí, v jakém přišly
        private async Task<List<Message>> LoadEmailsAsync() {

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
        private async Task ProcessEmailPagesAsync(MessageCollectionResponse messages, List<Message> allMessages) {
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

        // Metoda pro vytvoření složky (pokud neexistuje) dle cesty načtené z config.JSON
        private void CreateFolderForEmails() {
            DirectoryInfo di = new DirectoryInfo(_config.DownloadPath);
            int i = 1;

            // Kontrola, zda složka už existuje
            if (di.Exists) {
                DirectoryInfo[] subfolders = di.GetDirectories();

                string vypis = subfolders.Length == 0 ? "---" : string.Join("\n", subfolders.Select(d => $"[{i++}] {d.Name}")); // Vypíše názvy již stažených podsložek dle předmětů emailů

                _log.Info($"Složka pro ukládání emailu úspěšně načtená {_config.DownloadPath}, aktuálně obsahuje tyto podsložky:\n{vypis}");
            }
            else {
                // Pokud složka neexistuje, vytvořit ji
                _log.Info($"Vytvářím novou složku pro e-maily: {_config.DownloadPath}");
                di.Create();
            }
        }

        // Asynchronní metoda pro ukládání id emailů do souboru donwloadedEmails.TXT
        private async Task SaveIdsToFileAsync(string id) {
            if (!string.IsNullOrWhiteSpace(id)) {
                // Uložení ID emailů do souboru downloadedEmails.TXT pro zamezení opětovného stažení
                string downloadedPath = Path.Combine(_config.DownloadPath, "downloadedEmails.txt");
                await File.AppendAllTextAsync(downloadedPath, id + Environment.NewLine);
                _log.Debug($"Zapsáno ID emailu do seznamu stažených: {id}");
            }
        }

        // Asynchronní metoda pro načitání id emailů ze souboru donwloadedEmails.TXT
        private async Task<List<string>> GetSavedIdsAsync() {
            string downloadedPath = Path.Combine(_config.DownloadPath, "downloadedEmails.txt");

            if (!File.Exists(downloadedPath)) {
                _log.Warn("Soubor downloadedEmails.txt neexistuje – bude vytvořen při prvním stažení.");
                return new List<string>();  // Soubor neexistuje, přidat pouze prázdný seznam
            }
            try {
                string[] lines = await File.ReadAllLinesAsync(downloadedPath);
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
                _log.Error($"Chyba při čtení souboru downloadedEmails.TXT: {ex.Message}", ex);
                throw;
            }
        }

        // Metoda pro vytvoření unikátních názvu podsložek
        private async Task<string> CreateUniqueFolderPathAsync(string basePath, string subject) {
            // Oříznutí délky názvu předmětu na maxLength
            int maxLength = 100;
            if (subject.Length > maxLength) {
                subject = subject.Substring(0, maxLength);
            }
            // Vytvoření podsložek s ochranou proti zakázaným znakům
            var subjectName = string.Join("_", subject.Split(Path.GetInvalidFileNameChars()));
            var subfolderPath = Path.Combine(basePath, subjectName);

            // Pokud složka existuje, přidat  číslování _1 atd.
            int i = 1;
            while (Directory.Exists(subfolderPath)) {
                subfolderPath = Path.Combine(_config.DownloadPath, subjectName + "_" + i);
                i++;
            }
            return subfolderPath;
        }

        // HLAVNÍ METODA pro ukládání emailů do složek
        public async Task DownloadEmailsAsync() {

            try {

                // Ověření přístupu podle "AllowedMailBoxes" z config .json
                AuthenticationMailBoxesCheck();

                List<Message> orderedMessages = await LoadEmailsAsync(); // Přiřazení metody načtených emaliů do proměnné

                // Vytvoření Hlavní složky pro emaily - podle config.JSON
                CreateFolderForEmails();

                _log.Info($"Z config.json bylo načteno datum pro stažení emailů od: {_config.StartDate.Date.ToString("yyyy-MM-dd")}");

                // Načtení již stažených ID uložených v.TXT
                List<string> downloadedIds = await GetSavedIdsAsync();
                
                // Cysklus pro každý email ve složce Inbox
                foreach (var msg in orderedMessages) {
                    _log.Info($"Zpracovávám e-mail s předmětem: '{msg.Subject}'");

                    // Content pro uložení celéhoobsahu zprávy - včetně příloh
                    var content = await _graphClient.Users[_config.Mailbox]
                        .Messages[msg.Id]
                        .Content
                        .GetAsync();

                    // Získání datumu přijetí emailů
                    DateTime? emailReceivedDateTime = msg.ReceivedDateTime?.UtcDateTime;

                    if (emailReceivedDateTime == null) continue;

                    // Filtrování: jen zprávy od určitého data
                    if (emailReceivedDateTime.Value.Date < _config.StartDate) {
                        _log.Info("E-mail je starší než povolené datum, přeskakuji...");
                        continue;
                    }

                    string subfolderPath = await CreateUniqueFolderPathAsync(_config.DownloadPath, msg.Subject);  // Přiřazení metody pro vytvoření unikátních názvů složek do proměnné

                    // Pokud podsložky existují, přerušit stahování
                    if (downloadedIds.Contains(msg.InternetMessageId)) {
                        _log.Info($"Přeskakuji – email už byl dříve stažen (ID: {msg.InternetMessageId})");
                        continue;
                    }
                    else {

                        try {
                            //Pokus o vytvoření složky
                            Directory.CreateDirectory(subfolderPath);
                        }
                        catch (IOException ex) {
                            _log.Error($"Chyba při vytváření složky {subfolderPath}: {ex.Message}", ex);
                        }

                        // Uložení emailů do souboru ve formátu .eml
                        string filePath = Path.Combine(subfolderPath, $"message.eml");
                        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                        await content.CopyToAsync(fs);

                        _log.Info($"E-mail úspěšně uložen: {filePath}");

                        // Přidání ID emailu do seznamu stažených emailů, aby se nestahoval znovu.
                        await SaveIdsToFileAsync(msg.InternetMessageId);
                    }
                }
            }
            catch (Exception ex) {
                _log.Fatal("Chyba při stahování e-mailů", ex);
            }
        }
    }
}