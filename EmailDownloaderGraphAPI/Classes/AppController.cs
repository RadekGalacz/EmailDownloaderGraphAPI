﻿using System.Text.Json;

namespace EmailGraphAPI.Classes {
    internal static class AppController {

        // Statická metoda pro přiřazení props z config.json
        internal static AppConfigProps LoadConfig() {

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