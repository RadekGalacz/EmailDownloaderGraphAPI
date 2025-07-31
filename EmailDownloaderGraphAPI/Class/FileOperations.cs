using log4net;
using EmailGraphAPI.Interface;


namespace EmailGraphAPI.Classes {
    internal class FileOperations : IFilesOperations {
        private readonly AppConfigProps _config;
        private static readonly ILog _log = LogManager.GetLogger(typeof(FileOperations));
        public FileOperations(AppConfigProps config) {
            _config = config;
        }

        // Asynchronní metoda pro ukládání id emailů do souboru donwloadedEmails.TXT
        public async Task SaveIdsToFileAsync(string id) {
            if (!string.IsNullOrWhiteSpace(id)) {
                // Uložení ID emailů do souboru downloadedEmails.TXT pro zamezení opětovného stažení
                string downloadedPath = Path.Combine(_config.DownloadPath, "downloadedEmails.txt");
                await File.AppendAllTextAsync(downloadedPath, id + Environment.NewLine);
                _log.Debug($"Zapsáno ID emailu do seznamu stažených: {id}");
            }
        }

        // Asynchronní metoda pro načitání id emailů ze souboru donwloadedEmails.TXT
        public async Task<List<string>> GetSavedIdsAsync() {
            string downloadedPath = Path.Combine(_config.DownloadPath, "downloadedEmails.txt");

            if (!File.Exists(downloadedPath)) {
                _log.Warn("Soubor downloadedEmails.txt neexistuje – bude vytvořen při prvním stažení.");
                return new List<string>();  // Soubor neexistuje, přidat pouze prázdný seznam
            }
            try {
                string[] lines = await File.ReadAllLinesAsync(downloadedPath);
                List<string> ids = new List<string>();

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
    }
}
