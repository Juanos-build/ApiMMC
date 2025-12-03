using ApiMMC.Models.Entities;
using Microsoft.Data.SqlClient;

namespace ApiMMC.Models.Context.Factory
{
    public interface IConnectionFactory
    {
        SqlConnection GetConnection();
    }

    public class ConnectionFactory(string connectionString) : IConnectionFactory
    {
        private readonly string _connectionString = connectionString;

        public SqlConnection GetConnection() => new(_connectionString);
    }

    public static class TransactionHelper
    {
        public static async Task<Response<T>> ExecuteInTransactionAsync<T>(
        IConnectionFactory connectionFactory,
        Func<SqlTransaction, Task<Response<T>>> action)
        {
            // Crear y abrir conexión
            await using var connection = connectionFactory.GetConnection();
            await connection.OpenAsync();

            // Iniciar transacción
            using var transaction = connection.BeginTransaction();

            try
            {
                var result = await action(transaction);

                if (result.StatusCode == 1)
                {
                    await transaction.CommitAsync();
                    return result;
                }
                else
                {
                    throw new ResultException(result.StatusCode, result.StatusMessage);
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
