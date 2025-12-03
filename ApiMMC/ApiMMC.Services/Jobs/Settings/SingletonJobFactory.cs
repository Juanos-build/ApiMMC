using ApiMMC.Services.Helpers.Filters;
using ApiMMC.Services.Helpers.Logging;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;

namespace ApiMMC.Services.Jobs.Settings
{
    public class SingletonJobFactory(
        IServiceProvider serviceProvider,
        ResponseHelper responseHelper) : IJobFactory
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ResponseHelper _responseHelper = responseHelper;
        private IScheduler _scheduler;

        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            _scheduler = scheduler;

            var jobType = bundle.JobDetail.JobType;

            // Creamos un scope de logging que marque que estamos en un Job
            var jobNameScope = LoggerManager.logger.PushScopeProperty("JobName", jobType.Name);

            _responseHelper.Info($"NewJob start - Creando Job de tipo: {jobType.Name}");

            IServiceScope scope = null;

            try
            {
                // Crear un scope por ejecución
                scope = _serviceProvider.CreateScope();

                var job = scope.ServiceProvider.GetRequiredService(jobType) as IJob
                    ?? throw new Exception($"No se pudo crear una instancia de {jobType.FullName}");


                // Guardamos el scope dentro del JobDataMap para liberarlo luego
                scheduler.Context.Put($"{jobType.FullName}.Scope", scope);
                scheduler.Context.Put($"{jobType.FullName}.JobNameScope", jobNameScope);

                return job;
            }
            catch (Exception ex)
            {
                _responseHelper.Error(ex, $"Error creando job {jobType.Name}");

                // Cerramos los scopes antes de relanzar la excepción
                jobNameScope.Dispose();
                scope?.Dispose();
                throw;
            }
        }

        public void ReturnJob(IJob job)
        {
            if (_scheduler == null) return;

            var jobType = job.GetType();
            var keyScope = $"{jobType.FullName}.Scope";
            var keyJobNameScope = $"{jobType.FullName}.JobNameScope";

            // Cerramos el scope "IsJob" si existe
            if (_scheduler.Context.TryGetValue(keyJobNameScope, out object valueJobNameScope) &&
                valueJobNameScope is IDisposable jobNameScope)
            {
                jobNameScope.Dispose();
                _scheduler.Context.Remove(keyJobNameScope);
            }

            if (_scheduler.Context.TryGetValue(keyScope, out object valueScope) &&
                valueScope is IDisposable scope)
            {
                scope.Dispose();
                _scheduler.Context.Remove(keyScope);
            }

            // Si el job está envuelto, liberar el scope
            if (job is ScopedJob scopedJob)
            {
                scopedJob.Dispose();
            }
        }
    }
}
