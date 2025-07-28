using log4net;
using log4net.Config;
using System.Reflection;

namespace EmailGraphAPI.Classes {

    // Pomocná třída pro jednorázovou inicializaci logování pomocí log4net.
    internal static class LoggerSetup {
        private static bool _isConfigured = false; // log4net se nakonfiguruje pouze jednou za běhu aplikace

        public static void Configure() {
            if (!_isConfigured) {
                var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
                XmlConfigurator.Configure(logRepository, new FileInfo("./Config/log4net.config"));
                _isConfigured = true;
            }
        }
    }
}
