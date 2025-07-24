using System.Text.Json;

namespace EmailGraphAPI.Classes {
    internal class AppController {

        // Statická metoda pro přiřazení props z config.json
        public static AppConfigProps LoadConfig() {

            // JSON součásti aplikace
            string path = "./Config/config.json";
            if (!File.Exists(path)) {
                throw new FileNotFoundException($"Konfigurační soubor '{path}' nebyl nalezen.");
            }

            string fileContent = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppConfigProps>(fileContent);
        }
    }
}