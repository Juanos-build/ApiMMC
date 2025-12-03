using ApiMMC.Models.Entities;
using ApiMMC.Services.Helpers.Filters;
using ApiMMC.Services.Helpers.Logging;
using ApiMMC.Services.Services.Contracts;
using Quartz;

namespace ApiMMC.Services.Jobs.Proccess
{
    public class LecturaJob(
        ILecturaService readdingService,
        ResponseHelper responseHelper) : IJob
    {
        private readonly ILecturaService _readdingService = readdingService;
        private readonly ResponseHelper _responseHelper = responseHelper;

        public async Task Execute(IJobExecutionContext context)
        {
            using (LoggerManager.logger.PushScopeProperty("JobName", nameof(LecturaJob)))
            {
                _responseHelper.Info($"Inicio proceso lecturas");

                var progress = new Progress<ResultadoLectura>(r =>
                {
                    _responseHelper.Info(r.Mensaje);
                    if (!r.Exito)
                        LoggerManager.logger.WithProperty("request", r.DatosSolicitud).WithProperty("response", r.DatosRespuesta).Error(r.Mensaje);
                });

                var result = await _readdingService.SetEnergyRead(progress);

                _responseHelper.Info(result.StatusMessage);
            }
        }
    }
}
