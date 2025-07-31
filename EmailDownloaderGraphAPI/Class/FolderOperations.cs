using log4net;
using EmailGraphAPI.Interface;

namespace EmailGraphAPI.Classes {
    internal class FolderOperations : IFoldersOperations {
        private readonly AppConfigProps _config;
        private static readonly ILog _log = LogManager.GetLogger(typeof(FolderOperations));
        private string subFolderPathName;

        public string SubFolderPathName { get => subFolderPathName; set => subFolderPathName = value; }

        public FolderOperations(AppConfigProps config) {
            _config = config;
        }

        // Metoda pro vytvoření unikátních názvu podsložek
        public void CreateUniqueFolderPath(string basePath, string subject) {
            // Oříznutí délky názvu předmětu na maxLength
            int maxLength = 100;
            if (subject.Length > maxLength) {
                subject = subject.Substring(0, maxLength);
            }
            // Vytvoření podsložek s ochranou proti zakázaným znakům
            string subjectName = string.Join("_", subject.Split(Path.GetInvalidFileNameChars()));
            string subfolderPath = Path.Combine(basePath, subjectName);

            // Pokud složka existuje, přidat  číslování _1 atd.
            int i = 1;
            while (Directory.Exists(subfolderPath)) {
                subfolderPath = Path.Combine(_config.DownloadPath, subjectName + "_" + i);
                i++;
            }
            SubFolderPathName = subfolderPath;
        }

        // Metoda pro vytvoření složky (pokud neexistuje) dle cesty načtené z config.JSON
        public void CreateFolderForEmails() {
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
        // Metoda pro uložení emailů do složek s unikátním názvem
        public async Task SaveEmailsToSubfolders(Stream content) {
            string filePath = Path.Combine(SubFolderPathName, $"message.eml");
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            Console.WriteLine(subFolderPathName);
            await content.CopyToAsync(fs);
        }
    }
}
