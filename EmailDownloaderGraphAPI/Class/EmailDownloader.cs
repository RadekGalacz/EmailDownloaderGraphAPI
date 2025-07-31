using EmailGraphAPI.Interface;
using log4net;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace EmailGraphAPI.Classes {
    internal class EmailDownloader {
        private AppConfigProps _config;
        private readonly GraphAuthProvider _graphAuthProvider;
        private GraphServiceClient _graphClient; // Graph API pro poskytování metod pro práci s emaily
        private static readonly ILog _log = LogManager.GetLogger(typeof(EmailDownloader)); // Inicializace loggeru pro třídu Program (log4net)
        private readonly IFoldersOperations _foldersOperations;
        private readonly IFilesOperations _filesOperations;
        private readonly IEmailOperations _emailOperations;

        // Konstruktor třídy EmailDownloader
        public EmailDownloader(GraphAuthProvider graphAuthProvide, AppConfigProps config, IFoldersOperations foldersOperations, IFilesOperations filesOperations, IEmailOperations emailOperations) {
            _graphAuthProvider = graphAuthProvide;
            _config = config;
            _foldersOperations = foldersOperations;
            _filesOperations = filesOperations;
            _emailOperations = emailOperations;
        }

        // HLAVNÍ METODA pro ukládání emailů do složek
        public async Task DownloadEmailsAsync() {

            try {

                // Ověření přístupu podle "AllowedMailBoxes" z config .json
                _graphAuthProvider.AuthenticationMailBoxesCheck();

                // Inicializcae GraphApi
                if (_graphClient == null) {
                    _graphClient = _graphAuthProvider.GetAuthenticatedClient();
                }

                List<Message> orderedMessages = await _emailOperations.LoadEmailsAsync(); // Přiřazení metody načtených emaliů do proměnné

                // Vytvoření Hlavní složky pro emaily - podle config.JSON
                _foldersOperations.CreateFolderForEmails();

                _log.Info($"Z config.json bylo načteno datum pro stažení emailů od: {_config.StartDate.Date.ToString("yyyy-MM-dd")}");

                // Načtení již stažených ID, uložených v.TXT
                List<string> downloadedIds = await _filesOperations.GetSavedIdsAsync();
                
                // Cysklus pro každý email ve složce Inbox
                foreach (var msg in orderedMessages) {
                    _log.Info($"Zpracovávám e-mail s předmětem: '{msg.Subject}'");

                    // Content pro uložení celého obsahu zprávy - včetně příloh
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

                    _foldersOperations.CreateUniqueFolderPath(_config.DownloadPath, msg.Subject);

                    // Pokud podsložky existují, přeskočit stahování
                    if (downloadedIds.Contains(msg.InternetMessageId)) {
                        _log.Info($"Přeskakuji – email už byl dříve stažen (ID: {msg.InternetMessageId})");
                        continue;
                    }
                    else {

                        try {
                            // Pokus o vytvoření podsložek
                            Directory.CreateDirectory(_foldersOperations.SubFolderPathName);
                        }
                        catch (IOException ex) {
                            _log.Error($"Chyba při vytváření složky {_foldersOperations.SubFolderPathName}: {ex.Message}", ex);
                        }

                        // Uložení emailů do souboru ve formátu .eml
                        await _foldersOperations.SaveEmailsToSubfolders(content);

                        _log.Info($"E-mail úspěšně uložen: {_foldersOperations.SubFolderPathName}");

                        // Přidání ID emailu do seznamu stažených emailů, aby se nestahoval znovu.
                        await _filesOperations.SaveIdsToFileAsync(msg.InternetMessageId);
                    }
                }
            }
            catch (Exception ex) {
                _log.Fatal("Chyba při stahování e-mailů", ex);
            }
        }
    }
}