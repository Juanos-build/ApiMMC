using ApiMMC.Services.Helpers.Renderers;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Extensions.Logging;
using System.Reflection;

namespace ApiMMC.Services.Helpers.Logging
{
    public class LoggerManager
    {
        public static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Abre un scope que se cerrará al final del using
        /// <summary>
        /// Permite añadir un valor temporal al contexto del log (por ejemplo, RequestId, Usuario, etc.)
        /// </summary>
        public static IDisposable PushScopeProperty(string propertyName, object value) =>
            logger.PushScopeProperty(propertyName, value);

        // Métodos simplificados
        public static void LogDebug(string message) => logger.Debug(message);
        public static void LogError(string message) => logger.Error(message);
        public static void LogInfo(string message) => logger.Info(message);
        public static void LogWarn(string message) => logger.Warn(message);
    }

    public static class LoggingSetup
    {
        private const string DefaultConfigResource = "ApiMMC.Services.Helpers.Logging.nlog.config.json";
        private const string JobsConfigResource = "ApiMMC.Services.Helpers.Logging.nlog.config.jobs.json";

        /// <summary>
        /// Carga la configuración embebida de NLog y la integra con el sistema de logging de .NET.
        /// </summary>
        public static void ConfigureNLog(IConfiguration appConfig, bool forJobs = false)
        {
            var resourceName = forJobs ? JobsConfigResource : DefaultConfigResource;
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new Exception("No se pudo abrir el recurso embebido '" + resourceName + "'");

            // Cargar la configuración embebida JSON
            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();

            // Extrae solo la sección "nlog" y filtra nulos
            var nlogSection = config
                .GetSection("nlog")
                .AsEnumerable()
                .Where(kv => kv.Value != null);

            // Transformar la sub-sección “nlog” a un nuevo IConfigurationRoot
            var finalConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(nlogSection)
                .Build();

            // Configurar NLog con extensiones y settings
            LogManager.Setup()
                .SetupExtensions(s =>
                {
                    s.RegisterLayoutRenderer<JsonCamelCaseLayoutRenderer>("jsonCamelCase");
                    s.RegisterConfigSettings(appConfig);
                })
                .LoadConfigurationFromSection(finalConfig);
        }

        /// <summary>
        /// Helper directo para configuración general.
        /// </summary>
        public static void ConfigureMain(IConfiguration appConfig) => ConfigureNLog(appConfig, forJobs: false);

        /// <summary>
        /// Helper directo para configuración de jobs.
        /// </summary>
        public static void ConfigureJobs(IConfiguration appConfig) => ConfigureNLog(appConfig, forJobs: true);

    }
}
