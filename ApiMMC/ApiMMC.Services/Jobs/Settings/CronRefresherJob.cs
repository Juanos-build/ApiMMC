using Quartz;

namespace ApiMMC.Services.Jobs.Settings
{
    public class CronRefresherJob(ICronWatcherService cronWatcher) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            await cronWatcher.ActualizarCronJobsAsync();
        }
    }
}
