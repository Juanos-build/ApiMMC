using ApiMMC.Models.Entities;
using ApiMMC.Services.Helpers.Settings;
using ApiMMC.Services.Jobs.Settings;
using ApiMMC.Services.Services.Contracts;

namespace ApiMMC.Services.Services.Implementation
{
    public class ConfiguracionService(
        AppSettings appSettings) : IConfiguracionService
    {
        private readonly AppSettings _appSettings = appSettings;

        public Response<List<JobSchedule>> GetCronService()
        {
            var response = new Response<List<JobSchedule>>();

            var configuracion = new List<JobSchedule>{
                new(
                    jobTypeName: "ApiMMC.Services.Jobs.Proccess.LecturaJob, ApiMMC.Services",
                    cronExpression: _appSettings.NotificationSettings.ScheduledTimeLectura
                ),
                new(
                    jobTypeName: "ApiMMC.Services.Jobs.Proccess.ConsultaJob, ApiMMC.Services",
                    cronExpression: _appSettings.NotificationSettings.ScheduledTimeConsulta
                )
            };

            response.Result = configuracion;

            return response;
        }
    }
}
