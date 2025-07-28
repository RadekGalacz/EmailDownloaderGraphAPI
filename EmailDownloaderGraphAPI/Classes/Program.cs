using log4net;

namespace EmailGraphAPI.Classes {

    internal class Program {
        private static readonly ILog _log = LogManager.GetLogger(typeof(Program)); // Inicializace loggeru pro třídu Program (log4net)
        static async Task Main(string[] args) {
            LoggerSetup.Configure(); // Načtení konfigurační metody pro log4net z LoggerSetup.cs

            try {

                _log.Info("Spouštím hlavní metodu aplikace...");

                AppConfigProps config = AppController.LoadConfig(); // Spuštění staticé metody pro deserializaci z config.json do props
                GraphAuthProvider auth = new GraphAuthProvider(config); // Autentizace aplikace
                EmailDownloader email = new EmailDownloader(auth, config);

                await email.DownloadEmailsAsync();

                _log.Info("=== Konec aplikace ===");

            }
            catch (Exception ex) {
                _log.Fatal("Chyba při spouštění aplikace. Pravděpodobně špatně zadané údaje v config.json", ex);
            }
                Console.WriteLine("_____________________________________________________________");
                Console.WriteLine("Pro ukončení aplikace stiskněte jakýkoliv znak na klávesnici ");
                Console.WriteLine("_____________________________________________________________");
                Console.ReadKey();
        }
        
    }
}
