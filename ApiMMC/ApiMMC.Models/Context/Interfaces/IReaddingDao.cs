using ApiMMC.Models.Entities;
using Microsoft.Data.SqlClient;

namespace ApiMMC.Models.Context.Interfaces
{
    public interface IReaddingDao
    {
        Task<Response<MeasureConfig>> GetEnergyParameter(SqlTransaction transaction, EnergyConfig request);
        Task<Response<string>> SetEnergyRead(SqlTransaction transaction, SetEnergy request);
        Task<Response<string>> SetEnergyXM(SqlTransaction transaction, List<EnergyXmInternal> request);
        Task<Response<string>> SetProccessXM(SqlTransaction transaction, ProccessXM request);
        Task<Response<List<ProccessXM>>> GetProccessXM(SqlTransaction transaction);
        Task<Response<string>> UpdateProccessXM(SqlTransaction transaction, ProccessXM request);
    }
}
