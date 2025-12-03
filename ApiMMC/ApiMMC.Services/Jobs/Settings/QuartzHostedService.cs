using ApiMMC.Services.Helpers.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;

namespace ApiMMC.Services.Jobs.Settings
{
    public class QuartzHostedService(
          ISchedulerFactory schedulerFactory,
          IJobFactory jobFactory,
          IServiceProvider serviceProvider,
          ResponseHelper responseHelper,
          ILogger<QuartzHostedService> logger) : IHostedService
    {
        private readonly ISchedulerFactory _schedulerFactory = schedulerFactory;
        private readonly IJobFactory _jobFactory = jobFactory;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ResponseHelper _responseHelper = responseHelper;
        private readonly ILogger<QuartzHostedService> _logger = logger;
        public IScheduler Scheduler { get; set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _responseHelper.Info("StartAsync");

            Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            Scheduler.JobFactory = _jobFactory;

            // Iniciar el scheduler
            await Scheduler.Start(cancellationToken);

            // Ejecutar CronWatcherService
            using var scope = _serviceProvider.CreateScope();
            var cronWatcher = scope.ServiceProvider.GetRequiredService<ICronWatcherService>();
            await cronWatcher.ActualizarCronJobsAsync();

            // Programar ejecución periódica del watcher
            var refresherJob = JobBuilder.Create<CronRefresherJob>()
                .WithIdentity("CronRefresherJob")
                .Build();

            var refresherTrigger = TriggerBuilder.Create()
                .WithIdentity("CronRefresherTrigger")
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever())
                .Build();

            await Scheduler.ScheduleJob(refresherJob, refresherTrigger, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _responseHelper.Info("StopAsync");
            await Scheduler?.Shutdown(cancellationToken);
        }
    }
}
