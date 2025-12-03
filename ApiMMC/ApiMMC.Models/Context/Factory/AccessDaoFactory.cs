using ApiMMC.Models.Context.Access;
using ApiMMC.Models.Context.Interfaces;
using ApiMMC.Models.Entities;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;
using static Dapper.SqlMapper;

namespace ApiMMC.Models.Context.Factory
{
    public class AccessDaoFactory(IConnectionFactory connectionFactory) : DaoFactory
    {
        #region Static Access Members

        private readonly IConnectionFactory _connectionFactory = connectionFactory;

        /// <summary>
        /// Ejecuta un SP que solo devuelve CODIGO/MENSAJE vía parámetros de salida.
        /// </summary>
        public static async Task<Response<string>> ExecuteStoreProcedureParamsAsync(
            SqlTransaction transaction,
            string procedure,
            DynamicParameters procedureParams)
        {
            // Parámetros estándar del SP
            procedureParams.Add("CODIGO", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);
            procedureParams.Add("MENSAJE", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

            try
            {
                await transaction.Connection.ExecuteAsync(
                    procedure,
                    procedureParams,
                    transaction: transaction,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 400
                );

                return new Response<string>
                {
                    StatusCode = procedureParams.Get<int>("CODIGO"),
                    StatusMessage = procedureParams.Get<string>("MENSAJE")
                };
            }
            catch (Exception ex)
            {
                return new Response<string>
                {
                    StatusCode = -1,
                    StatusMessage = $"Error ejecutando SP: {ex.Message}"
                };
            }
        }


        /// <summary>
        /// Ejecuta un SP que devuelve múltiples tablas y CODIGO/MENSAJE.
        /// </summary>
        public static async Task<Response<List<object>>> ExecuteStoreProcedureDataAsync(
            SqlTransaction transaction,
            string procedure,
            DynamicParameters procedureParams,
            params Func<GridReader, object>[] readerFuncs)
        {
            var response = new Response<List<object>>();
            var resultList = new List<object>();

            // Parámetros estándar del SP
            procedureParams.Add("CODIGO", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);
            procedureParams.Add("MENSAJE", dbType: DbType.String, size: 400, direction: ParameterDirection.Output);

            try
            {
                using var grid = await transaction.Connection.QueryMultipleAsync(
                    procedure,
                    procedureParams,
                    transaction: transaction,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 400
                );

                // Lee cada cursor/tablas devueltas
                foreach (var reader in readerFuncs)
                {
                    try
                    {
                        if (!grid.IsConsumed)
                            resultList.Add(reader(grid));
                        else
                            resultList.Add(null);
                    }
                    catch
                    {
                        resultList.Add(null);
                    }
                }

                response.StatusCode = procedureParams.Get<int>("CODIGO");
                response.StatusMessage = procedureParams.Get<string>("MENSAJE");
                response.Result = resultList;
            }
            catch (Exception ex)
            {
                response.StatusCode = -1;
                response.StatusMessage = $"Error ejecutando SP: {ex.Message}";
                response.Result = null;
            }

            return response;
        }

        /// <summary>
        /// Manejo seguro de excepciones tipo SQL.
        /// </summary>
        public static async Task<T> SafeExecuteAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (SqlException ex)
            {
                throw new DataAccessException("Error de base de datos SQL", ex);
            }
            catch (Exception ex)
            {
                throw new UnexpectedException("Error inesperado en operación SQL", ex);
            }
        }

        #endregion

        #region Transactional Interfaces

        public override ITransactionDao GetTransactionDao()
        {
            return new TransactionDao(_connectionFactory, this);
        }
        public override IReaddingDao GetReaddingDao()
        {
            return new ReaddingDao();
        }

        #endregion
    }

}
