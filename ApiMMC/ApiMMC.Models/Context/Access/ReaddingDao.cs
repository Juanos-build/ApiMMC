using ApiMMC.Models.Context.Factory;
using ApiMMC.Models.Context.Interfaces;
using ApiMMC.Models.Entities;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ApiMMC.Models.Context.Access
{
    public class ReaddingDao : IReaddingDao
    {
        // -------------------------------------------------------------
        // 1) Obtener Configuración de Lectura (2 cursores)
        // -------------------------------------------------------------
        public async Task<Response<MeasureConfig>> GetEnergyParameter(SqlTransaction transaction, EnergyConfig request)
        {
            return await AccessDaoFactory.SafeExecuteAsync(async () =>
            {
                var parametro = new DynamicParameters();
                parametro.Add("OBJ_MESAURE",
                    request != null ? Utility.ConvertToDataTable(request) : null,
                    DbType.Object);

                var dt = await AccessDaoFactory.ExecuteStoreProcedureDataAsync(
                    transaction,
                    "[SP_GET_CONFIGREADMESAURE]",
                    parametro,
                    gr => gr.Read<EnergyConfigExtend>().FirstOrDefault(),
                    gr => gr.Read<MeasureReadConfig>().FirstOrDefault()
                );

                var response = new Response<MeasureConfig>
                {
                    StatusCode = dt.StatusCode,
                    StatusMessage = dt.StatusMessage
                };

                if (dt.StatusCode == 1)
                {
                    response.Result = new MeasureConfig
                    {
                        EnergyConfig = dt.Result?[0] as EnergyConfigExtend,
                        MeasureReadConfig = dt.Result?[1] as MeasureReadConfig
                    };
                }

                return response;
            });
        }

        // -------------------------------------------------------------
        // 2) Registrar Lectura Energía
        // -------------------------------------------------------------
        public async Task<Response<string>> SetEnergyRead(SqlTransaction transaction, SetEnergy request)
        {
            return await AccessDaoFactory.SafeExecuteAsync(async () =>
            {
                var parametro = new DynamicParameters();
                parametro.Add("OBJ_MESAURE", request.Measure != null ? Utility.ConvertToDataTable(request.Measure) : null, DbType.Object);
                parametro.Add("OBJ_MESAURE_READING_MESAURE", request.Energies != null ? Utility.ToDataTable(request.Energies) : null, DbType.Object);

                return await AccessDaoFactory.ExecuteStoreProcedureParamsAsync(
                    transaction,
                    "[SP_SET_READING_MESAURE]",
                    parametro
                );
            });
        }

        // -------------------------------------------------------------
        // 3) Guardar Lectura XM
        // -------------------------------------------------------------
        public async Task<Response<string>> SetEnergyXM(SqlTransaction transaction, List<EnergyXmInternal> request)
        {
            return await AccessDaoFactory.SafeExecuteAsync(async () =>
            {
                var parametro = new DynamicParameters();
                parametro.Add("OBJ_MESAURE", request != null ? Utility.ToDataTable(request) : null, DbType.Object);

                return await AccessDaoFactory.ExecuteStoreProcedureParamsAsync(
                    transaction,
                    "[SP_SET_READING_MESAURE_XM]",
                    parametro
                );
            });
        }

        // -------------------------------------------------------------
        // 4) Registrar Proceso XM
        // -------------------------------------------------------------
        public async Task<Response<string>> SetProccessXM(SqlTransaction transaction, ProccessXM request)
        {
            return await AccessDaoFactory.SafeExecuteAsync(async () =>
            {
                var parametro = new DynamicParameters();
                parametro.Add("OBJ_XM", request != null ? Utility.ConvertToDataTable(request) : null, DbType.Object);

                return await AccessDaoFactory.ExecuteStoreProcedureParamsAsync(
                    transaction,
                    "[SP_SET_PROCCESS_XM]",
                    parametro
                );
            });
        }

        // -------------------------------------------------------------
        // 5) Obtener Procesos XM (1 cursor)
        // -------------------------------------------------------------
        public async Task<Response<List<ProccessXM>>> GetProccessXM(SqlTransaction transaction)
        {
            return await AccessDaoFactory.SafeExecuteAsync(async () =>
            {
                var parametro = new DynamicParameters();

                var dt = await AccessDaoFactory.ExecuteStoreProcedureDataAsync(
                    transaction,
                    "[SP_GET_PROCCES_XM]",
                    parametro,
                    gr => gr.Read<ProccessXM>()
                );

                var response = new Response<List<ProccessXM>>
                {
                    StatusCode = dt.StatusCode,
                    StatusMessage = dt.StatusMessage
                };

                if (dt.StatusCode == 1)
                    response.Result = dt.Result?[0] as List<ProccessXM>;

                return response;
            });
        }

        // -------------------------------------------------------------
        // 6) Actualizar Proceso XM
        // -------------------------------------------------------------
        public async Task<Response<string>> UpdateProccessXM(SqlTransaction transaction, ProccessXM request)
        {
            return await AccessDaoFactory.SafeExecuteAsync(async () =>
            {
                var parametro = new DynamicParameters();
                parametro.Add("OBJ_XM", request != null ? Utility.ConvertToDataTable(request) : null, DbType.Object);

                return await AccessDaoFactory.ExecuteStoreProcedureParamsAsync(
                    transaction,
                    "[SP_UPDATE_PROCCES_XM]",
                    parametro
                );
            });
        }
    }

}
