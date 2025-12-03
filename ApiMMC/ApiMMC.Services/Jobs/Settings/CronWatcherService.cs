using ApiMMC.Services.Services.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ApiMMC.Services.Jobs.Settings
{
    public interface ICronWatcherService
    {
        Task ActualizarCronJobsAsync();
    }

    public class CronWatcherService(
        IServiceScopeFactory serviceScopeFactory,
        ISchedulerFactory schedulerFactory) : ICronWatcherService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ISchedulerFactory _schedulerFactory = schedulerFactory;
        private string _lastConfigHash;

        public async Task ActualizarCronJobsAsync()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var configuracionService = scope.ServiceProvider.GetRequiredService<IConfiguracionService>();
            // Obtener las configuraciones de Cron desde la base de datos
            var jobSchedules = configuracionService.GetCronService();

            // Serializar y calcular hash de la configuración actual
            var currentConfigJson = JsonSerializer.Serialize(jobSchedules.Result);
            var currentHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(currentConfigJson));
            var currentHash = Convert.ToBase64String(currentHashBytes);

            // Comparar con el último hash
            if (_lastConfigHash == currentHash)
            {
                // No hay cambios
                return;
            }

            // Obtener scheduler en tiempo de ejecución
            var scheduler = await _schedulerFactory.GetScheduler();

            foreach (var jobSchedule in jobSchedules?.Result)
            {
                var jobType = Type.GetType(jobSchedule.JobTypeName) ?? throw new Exception($"No se pudo cargar el tipo: {jobSchedule.JobTypeName}");

                var jobKey = new JobKey($"{jobSchedule.JobTypeName}");
                var triggerKey = new TriggerKey($"{jobSchedule.JobTypeName}.trigger");

                if (await scheduler.CheckExists(jobKey) && await scheduler.CheckExists(triggerKey))
                {
                    var newTrigger = TriggerBuilder
                        .Create()
                        .WithIdentity(triggerKey)
                        .WithCronSchedule(jobSchedule.CronExpression)
                        .Build();

                    await scheduler.RescheduleJob(triggerKey, newTrigger);
                }
                else
                {
                    // Crear el Job y su Trigger si no existe
                    var job = JobBuilder
                        .Create(jobType)
                        .WithIdentity(jobKey)
                        .Build();

                    var trigger = TriggerBuilder
                        .Create()
                        .WithIdentity(triggerKey)
                        .WithCronSchedule(jobSchedule.CronExpression)
                        .Build();

                    await scheduler.ScheduleJob(job, trigger);
                }
            }
            // Guardamos el nuevo hash
            _lastConfigHash = currentHash;
        }
    }
}
