using ApiMMC.Models.Entities;
using ApiMMC.Services.Jobs.Settings;

namespace ApiMMC.Services.Services.Contracts
{
    public interface IConfiguracionService
    {
        Response<List<JobSchedule>> GetCronService();
    }
}
