using log4net;
using log4net.Config;
using System.Reflection;

namespace EmailGraphAPI.Classes {

    // Pomocná třída pro jednorázovou inicializaci logování pomocí log4net.
    internal static class LoggerSetup {
        private static bool isConfigured = false;

        public static void Configure() {
            if (!isConfigured) {
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("./Config/log4net.config"));
                isConfigured = true;
            }
        }
    }
}
