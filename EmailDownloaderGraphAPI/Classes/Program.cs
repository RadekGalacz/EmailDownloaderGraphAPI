using log4net;
using log4net.Config;
using System.Reflection;
using System.Threading.Tasks;

namespace EmailGraphAPI.Classes {

    internal class Program {
        private static readonly ILog log = LogManager.GetLogger(typeof(Program)); // Inicializace loggeru pro třídu Program (log4net)
        static async Task Main(string[] args) {
            LoggerSetup.Configure(); // Načtení konfigurační metody pro log4net z LoggerSetup.cs

            try {

                log.Info("Spouštím hlavní metodu aplikace...");
                AppConfigProps config = AppController.LoadConfig();

                GraphAuthenticator auth = new GraphAuthenticator(config);
                await auth.DownloadEmail();
                log.Info("=== Konec aplikace ===");

            }
            catch (Exception ex) {
                log.Fatal("Chyba při spouštění aplikace. Pravděpodobně špatně zadané údaje v config.json", ex);
            }
                Console.WriteLine("_____________________________________________________________");
                Console.WriteLine("Pro ukončení aplikace stiskněte jakýkoliv znak na klávesnici ");
                Console.WriteLine("_____________________________________________________________");
                Console.ReadKey();
        }
        
    }
}
