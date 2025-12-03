using ApiMMC.Models.Entities;

namespace ApiMMC.Models.Context.Interfaces
{
    public interface ITransactionDao
    {
        Task<Response<MeasureConfig>> GetEnergyParameter(EnergyConfig request);
        Task<Response<string>> SetEnergyRead(SetEnergy request);
        Task<Response<string>> SetEnergyXM(List<EnergyXmInternal> request);
        Task<Response<string>> SetProccessXM(ProccessXM request);
        Task<Response<List<ProccessXM>>> GetProccessXM();
        Task<Response<string>> UpdateProccessXM(ProccessXM request);
    }
}
