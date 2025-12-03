using ApiMMC.Models.Context.Access;
using ApiMMC.Models.Context.Factory;
using ApiMMC.Models.Context.Interfaces;
using ApiMMC.Models.Entities;
using ApiMMC.Services.Helpers.Filters;
using ApiMMC.Services.Helpers.Integration;
using ApiMMC.Services.Helpers.Settings;
using ApiMMC.Services.Jobs.Proccess;
using ApiMMC.Services.Jobs.Settings;
using ApiMMC.Services.Services.Contracts;
using ApiMMC.Services.Services.Implementation;
using Microsoft.Extensions.DependencyInjection;
using NLog.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using System.Net;

namespace ApiMMC.Services.Helpers.Extension
{
    public static class ServicesExtensionProccess
    {
        public static IServiceCollection ConfigureServicesProccess(this IServiceCollection services)
        {
            // Registro de helpers
            services.AddSingleton<ResponseHelper>();
            services.AddSingleton<RequestHttp>();
            services.AddSingleton<Utility>();

            // Registrar AutoMapper en DI (importante)
            services.AddAutoMapper(typeof(MapperBootstrapper).Assembly);

            // Registro de conexión y factory
            services.AddSingleton<IConnectionFactory>(sp =>
            {
                var settings = sp.GetRequiredService<AppSettings>();
                return new ConnectionFactory(settings.Connection);
            });
            services.AddSingleton<DaoFactory, AccessDaoFactory>();
            services.AddSingleton<ITransactionDao, TransactionDao>();

            services.AddSingleton<ILecturaService, LecturaService>();
            services.AddSingleton<IConfiguracionService, ConfiguracionService>();

            // Registro del servicio CronWatcher
            services.AddSingleton<ICronWatcherService, CronWatcherService>();

            services.AddSingleton<CronRefresherJob>();

            services.AddTransient<LecturaJob>();
            services.AddTransient<ConsultaJob>();

            services.AddHostedService<QuartzHostedService>();
            services.AddSingleton<IJobFactory, SingletonJobFactory>();
            services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();

            services.AddHttpClient("DefaultClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(120); // evita respuestas cortadas
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
            });

            //NLog
            services.AddLogging(logging =>
            {
                logging.AddNLog();
            });

            return services;
        }
    }
}
