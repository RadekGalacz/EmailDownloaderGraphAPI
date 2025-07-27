using System.Text.Json;

namespace EmailGraphAPI.Classes {
    internal class AppController {

        // Metoda pro přiřazení props z config.json
       public static AppConfigProps LoadConfig(string path = "./Config/config.json") {
           
            if (!File.Exists(path)) {
                throw new FileNotFoundException($"Konfigurační soubor '{path}' nebyl nalezen.");
            }

            string fileContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfigProps>(fileContent);
        }
    }
}