using ApiMMC.Models.Entities;

namespace ApiMMC.Services.Services.Contracts
{
    public interface IReaddingService
    {
        Task<Response<string>> SetEnergyRead(IProgress<ResultadoLectura> progress = null);
        Task<Response<string>> ConsultarProcceso(IProgress<ResultadoLectura> progress = null);
    }
}
