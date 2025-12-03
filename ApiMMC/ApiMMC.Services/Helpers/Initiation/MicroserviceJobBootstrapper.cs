using ApiMMC.Services.Helpers.Extension;
using ApiMMC.Services.Helpers.Logging;
using ApiMMC.Services.Helpers.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApiMMC.Services.Helpers.Initiation
{
    public static class MicroserviceJobBootstrapper
    {
        public static IHost CreateBuilder(
            string[] args,
            Action<IServiceCollection> configureExtraServices = null)
        {
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseWindowsService() // Mantiene tu Windows Service
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Configuración base compartida
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                          .AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    // Logging
                    LoggingSetup.ConfigureJobs(context.Configuration);

                    // Cargar y desencriptar configuración
                    var appSettings = context.Configuration
                        .GetSection("appSettings")
                        .Get<AppSettings>()
                        .Decrypt();

                    services.AddSingleton(appSettings);

                    // Configuración específica para procesos
                    services.ConfigureServicesProccess();

                    // Servicios adicionales específicos por job si aplica
                    // services.AddScoped<IMiJobAdicional, MiJobAdicional>();
                    configureExtraServices?.Invoke(services);
                });

            var host = hostBuilder.Build();

            return host;
        }
    }
}
