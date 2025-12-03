using ApiMMC.Models.Context.Factory;
using ApiMMC.Models.Context.Interfaces;
using ApiMMC.Models.Entities;
using Microsoft.Data.SqlClient;

namespace ApiMMC.Models.Context.Access
{
    public class TransactionDao(IConnectionFactory connection, DaoFactory factory) : ITransactionDao
    {
        private readonly IConnectionFactory _connection = connection;
        private readonly DaoFactory _factory = factory;

        // método genérico para ejecutar transacción del dao
        private async Task<Response<T>> Execute<TDao, T>(
            Func<TDao, SqlTransaction, Task<Response<T>>> action
        ) where TDao : class
        {
            // crear una nueva instancia del DAO por cada ejecución
            var dao = _factory.GetDao<TDao>();

            return await TransactionHelper.ExecuteInTransactionAsync(
                _connection,
                transaction => action(dao, transaction)
            );
        }

        public Task<Response<MeasureConfig>> GetEnergyParameter(EnergyConfig request) =>
            Execute<IReaddingDao, MeasureConfig>((dao, transaction) => dao.GetEnergyParameter(transaction, request));

        public Task<Response<string>> SetEnergyRead(SetEnergy request) =>
            Execute<IReaddingDao, string>((dao, transaction) => dao.SetEnergyRead(transaction, request));

        public Task<Response<string>> SetEnergyXM(List<EnergyXmInternal> request) =>
            Execute<IReaddingDao, string>((dao, transaction) => dao.SetEnergyXM(transaction, request));

        public Task<Response<string>> SetProccessXM(ProccessXM request) =>
            Execute<IReaddingDao, string>((dao, transaction) => dao.SetProccessXM(transaction, request));

        public Task<Response<List<ProccessXM>>> GetProccessXM() =>
            Execute<IReaddingDao, List<ProccessXM>>((dao, transaction) => dao.GetProccessXM(transaction));

        public Task<Response<string>> UpdateProccessXM(ProccessXM request) =>
            Execute<IReaddingDao, string>((dao, transaction) => dao.UpdateProccessXM(transaction, request));
    }
}
