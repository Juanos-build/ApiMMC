using ApiMMC.Models.Context.Interfaces;
using ApiMMC.Models.Entities;
using ApiMMC.Services.Helpers.Extension;
using ApiMMC.Services.Helpers.Filters;
using ApiMMC.Services.Helpers.Integration;
using ApiMMC.Services.Helpers.Settings;
using ApiMMC.Services.Services.Contracts;
using AutoMapper;
using SpreadsheetLight;
using System.Text.Json;
namespace ApiMMC.Services.Services.Implementation
{
    public class LecturaService(
    AppSettings settings,
    ITransactionDao transactionDao,
    RequestHttp requestHttp,
    ResponseHelper responseHelper,
    IMapper mapper) : ILecturaService
    {
        private readonly ITransactionDao _objTransaction = transactionDao;
        private readonly AppSettings _appSettings = settings;
        private readonly ResponseHelper _responseHelper = responseHelper;
        private readonly RequestHttp _requestHttp = requestHttp;
        private readonly IMapper _mapper = mapper;

        // estado in-memory durante la ejecución
        private MeasureConfig _energyConfig = new();
        private readonly List<EnergyXm> _energyXM = [];

        #region Lectura y Envío de Valores (public API)

        public async Task<Response<string>> SetEnergyRead(IProgress<ResultadoLectura> progress = null)
        {
            var response = new Response<string>();
            try
            {
                // 1) Leer y registrar lecturas desde archivos
                progress?.Report(new ResultadoLectura { Exito = true, Mensaje = "Inicio de lectura de archivos", DatosSolicitud = null });
                var lecturaResults = await LeerArchivosYRegistrarAsync(progress);

                // 2) Agrupar y preparar datos para XM (llenar _energyXM)
                progress?.Report(new ResultadoLectura { Exito = true, Mensaje = "Iniciando agrupación de lecturas para XM", DatosSolicitud = null });
                ProcesarAgruparParaXm(lecturaResults, progress);

                // 3) Enviar agrupado por frontera
                if (_energyXM.Count != 0)
                {
                    progress?.Report(new ResultadoLectura { Exito = true, Mensaje = "Iniciando envío de lecturas a XM por frontera", DatosSolicitud = null });
                    var sendResponse = await ProcesarYEnviarPorFronteraAsync(progress);
                    // Hacemos success si al menos una frontera fue enviada
                    if (sendResponse.IsSuccess)
                        response.Ok();
                    else
                        response = sendResponse; // propagar el detalle
                }
                else
                {
                    _responseHelper.Info("No hay lecturas configuradas para enviar a XM.");
                    _responseHelper.Success(response);
                    progress?.Report(new ResultadoLectura { Exito = true, Mensaje = "No hay lecturas para enviar a XM", DatosSolicitud = null });
                }
            }
            catch (ResultException rex)
            {
                _responseHelper.Error(rex, "Error en SetEnergyRead (ResultException)");
                _responseHelper.Exception(response, rex);
                progress?.Report(new ResultadoLectura { Exito = false, Mensaje = $"Error general: {rex.Message}", DatosSolicitud = null, DatosRespuesta = rex.InnerException?.Message });
            }
            catch (Exception ex)
            {
                _responseHelper.Error(ex, "Error en SetEnergyRead");
                _responseHelper.Exception(response, ex);
                progress?.Report(new ResultadoLectura { Exito = false, Mensaje = $"Error general: {ex.Message}", DatosSolicitud = null, DatosRespuesta = ex.InnerException?.Message });
            }

            return response;
        }

        #endregion

        #region Lectura de archivos

        // Resultado mínimo por archivo leído
        private record LecturaArchivoResult(string FilePath, string FileName, List<Energy> Energies, EnergyConfigExtend SettingEnergy);

        // -----------------------------------------------------------------------
        // Lectura de archivos y registro inicial en BD (separa responsabilidad)
        // -----------------------------------------------------------------------
        private async Task<List<LecturaArchivoResult>> LeerArchivosYRegistrarAsync(IProgress<ResultadoLectura> progress)
        {
            var resultados = new List<LecturaArchivoResult>();
            var files = Directory.GetFiles(_appSettings.FileSettings.FileToProcess, "*.xlsx");

            if (files == null || files.Length == 0)
            {
                _responseHelper.Warn("No se encuentra archivo para procesar");
                progress?.Report(new ResultadoLectura { Exito = false, Mensaje = "No se encuentra archivo para procesar", DatosSolicitud = null });
                return resultados;
            }

            foreach (var file in files)
            {
                string nameFile = Path.GetFileName(file);
                try
                {
                    using var sl = new SLDocument(file);
                    var sheetName = sl.GetCurrentWorksheetName();
                    var measureId = sheetName?.Split('-')?[1]?.Trim()?.ToUpper();

                    if (string.IsNullOrEmpty(measureId))
                    {
                        var destNo = Path.Combine(_appSettings.FileSettings.FileNoProcessed, nameFile);
                        ExtensionMethods.MoveFileSafe(file, destNo);
                        var msg = $"No se pudo determinar MeasureId en {nameFile}. Archivo movido a {destNo}";
                        progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = nameFile });
                        continue;
                    }

                    var energyMeasure = new EnergyConfig { MeasureId = measureId };
                    var dbConfigResp = await _objTransaction.GetEnergyParameter(energyMeasure);
                    _energyConfig = dbConfigResp.Result;

                    if (_energyConfig.MeasureReadConfig == null || _energyConfig.EnergyConfig == null)
                    {
                        var destNo = Path.Combine(_appSettings.FileSettings.FileNoProcessed, nameFile);
                        ExtensionMethods.MoveFileSafe(file, destNo);
                        var msg = $"No configuración lectura para serial {measureId}. Archivo movido a {destNo}";
                        progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = nameFile });
                        continue;
                    }

                    // Usar helper para leer filas (tu ExtensionMethods o FileReadUtility)
                    var energies = ExtensionMethods.LeerArchivoEnergia(file, _energyConfig.MeasureReadConfig, nameFile);

                    // Registrar lecturas en BD (SetEnergyRead)
                    var setEnergy = new SetEnergy
                    {
                        Measure = _energyConfig.EnergyConfig,
                        Energies = _mapper.Map<List<EnergyInternal>>(energies)
                    };

                    var dbResult = await _objTransaction.SetEnergyRead(setEnergy);
                    var msgDb = $"Lecturas de archivo registradas en BD. {dbResult.StatusMessage}";
                    _responseHelper.Info(msgDb);
                    progress?.Report(new ResultadoLectura { Exito = true, Mensaje = $"Archivo registrado en BD: {nameFile}. {dbResult.StatusMessage}", DatosSolicitud = nameFile });

                    // Mover archivo a processed
                    var destProcessed = Path.Combine(_appSettings.FileSettings.FileProcessed, nameFile);
                    ExtensionMethods.MoveFileSafe(file, destProcessed);
                    var msgProc = $"Archivo procesado con éxito. directorio {destProcessed}";
                    progress?.Report(new ResultadoLectura { Exito = true, Mensaje = msgProc, DatosSolicitud = nameFile });

                    // devolver resultado para procesamiento XM
                    resultados.Add(new LecturaArchivoResult(destProcessed, nameFile, energies, _energyConfig.EnergyConfig));
                }
                catch (ResultException rex)
                {
                    var destNo = Path.Combine(_appSettings.FileSettings.FileNoProcessed, nameFile);
                    ExtensionMethods.MoveFileSafe(file, destNo);
                    _responseHelper.Error(rex, $"Resultado leyendo archivo {nameFile}");
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = rex.Message, DatosSolicitud = nameFile, DatosRespuesta = rex.InnerException?.Message });
                }
                catch (Exception ex)
                {
                    var destNo = Path.Combine(_appSettings.FileSettings.FileNoProcessed, nameFile);
                    ExtensionMethods.MoveFileSafe(file, destNo);
                    _responseHelper.Error(ex, $"Error leyendo archivo {nameFile}");
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = ex.Message, DatosSolicitud = nameFile, DatosRespuesta = ex.InnerException?.Message });
                }
            }

            return resultados;
        }

        #endregion

        #region Procesamiento / Agrupación para XM

        // -----------------------------------------------------------------------
        // Agrupar y preparar estructura _energyXM (manteniendo 24 lecturas)
        // -----------------------------------------------------------------------
        private void ProcesarAgruparParaXm(List<LecturaArchivoResult> lecturaResults, IProgress<ResultadoLectura> progress)
        {
            _energyXM.Clear();

            var dateRead = DateTime.Today.AddDays(-1).Date;

            foreach (var lr in lecturaResults)
            {
                var setting = lr.SettingEnergy;
                if (setting == null)
                {
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = $"No hay configuración para archivo {lr.FileName}", DatosSolicitud = lr.FileName });
                    continue;
                }

                var energies = lr.Energies;

                // Aplicar KTE si aplica
                ExtensionMethods.AplicarKTE(energies, setting);

                // Reporte: inicio agrupación por medidor
                progress?.Report(new ResultadoLectura
                {
                    Exito = true,
                    Mensaje = $"Agrupando lecturas para medidor {setting.MeasureId} (frontera {setting.BorderIdXM}).",
                    DatosSolicitud = lr.FileName
                });

                // Construir 24 lecturas (manteniendo CountReadHour)
                var energyReadList = ExtensionMethods.Construir24Lecturas(energies, setting, dateRead);

                // Persistir EnergyXM interno en BD (SetEnergyXM)
                var energyXmInternal = _mapper.Map<List<EnergyXmInternal>>(energyReadList);
                var dbSet = _objTransaction.SetEnergyXM(energyXmInternal).Result;

                // Reporte: persistencia interna
                progress?.Report(new ResultadoLectura
                {
                    Exito = true,
                    Mensaje = $"Lecturas agregadas y registradas internamente para medidor {setting.MeasureId}.",
                    DatosSolicitud = lr.FileName,
                    DatosRespuesta = dbSet.StatusMessage
                });

                // Guardar en memoria para envío
                _energyXM.Add(new EnergyXm { Config = setting, Energies = energyReadList });
            }

            progress?.Report(new ResultadoLectura { Exito = true, Mensaje = $"Agrupación completada. Total medidores para XM: {_energyXM.Count}", DatosSolicitud = null });
        }

        #endregion

        #region Envío a XM (agrupa por frontera y envía ZIPs)

        // -----------------------------------------------------------------------
        // Envío por frontera -> agrupa multiples JSON en un ZIP (<=5 archivos, <=25MB)
        // -----------------------------------------------------------------------
        private async Task<Response<string>> ProcesarYEnviarPorFronteraAsync(IProgress<ResultadoLectura> progress)
        {
            var response = new Response<string>();
            var dateRead = DateTime.Today.AddDays(-1).Date;

            var fronteras = _energyXM.Select(d => d.Config.BorderIdXM).Distinct().ToList();

            foreach (var frontera in fronteras)
            {
                var energiesFor = _energyXM.Where(e => e.Config.BorderIdXM == frontera).ToList();
                if (energiesFor.Count == 0) continue;

                progress?.Report(new ResultadoLectura
                {
                    Exito = true,
                    Mensaje = $"Preparando envío para frontera {frontera}. Medidores: {energiesFor.Count}",
                    DatosSolicitud = frontera
                });

                var archivosJson = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var energy in energiesFor)
                {
                    if (string.IsNullOrEmpty(energy.Config.BorderIdXM))
                    {
                        var msg = $"Id Frontera nulo para medidor {energy.Config.MeasureId}. Se omite.";
                        progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = energy.Config.MeasureId });
                        continue;
                    }

                    var readItems = energy.Energies.Select(e => e.EnergyReadding).ToList();
                    if (readItems.Count != 24)
                    {
                        var msg = $"Medidor {energy.Config.MeasureId} no tiene 24 valores ({readItems.Count}). Se omite.";
                        progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = energy.Config.MeasureId });
                        continue;
                    }

                    // nombre del json (medidor + fecha)
                    var nombreJson = $"{energy.Config.MeasureId}_{dateRead:yyyyMMdd}.json";
                    // usar extension methods para generar el json (validará 24 valores)
                    var json = ExtensionMethods.CrearJsonLecturas(energy.Config.BorderIdXM, dateRead, readItems);
                    archivosJson[nombreJson] = json;

                    progress?.Report(new ResultadoLectura
                    {
                        Exito = true,
                        Mensaje = $"JSON preparado para medidor {energy.Config.MeasureId}: {nombreJson}",
                        DatosSolicitud = energy.Config.MeasureId
                    });
                }

                if (archivosJson.Count == 0)
                {
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = $"No se generaron JSONs para frontera {frontera}", DatosSolicitud = frontera });
                    continue;
                }

                // Validacion: max 5 archivos por ZIP
                if (archivosJson.Count > 5)
                {
                    var msg = $"Frontera {frontera} tiene {archivosJson.Count} JSONs. XM permite máximo 5. Omitido.";
                    _responseHelper.Warn(msg);
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = frontera });
                    continue;
                }

                // Crear ZIP
                byte[] zipBytes = null;
                try
                {
                    zipBytes = ExtensionMethods.CrearZipLecturasMultiple(archivosJson);
                    progress?.Report(new ResultadoLectura
                    {
                        Exito = true,
                        Mensaje = $"ZIP creado para frontera {frontera}. Nombre: reportelecturas_{frontera}_{dateRead:yyyyMMdd}.zip Size: {zipBytes.Length / 1024} KB",
                        DatosSolicitud = frontera
                    });
                }
                catch (Exception exZip)
                {
                    var msg = $"Error creando ZIP para frontera {frontera}: {exZip.Message}";
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = frontera, DatosRespuesta = exZip.InnerException?.Message });
                    continue;
                }

                // VALIDACIÓN XM → máximo 25MB
                if (zipBytes.Length > (25 * 1024 * 1024))
                {
                    var msg = $"El ZIP excede los 25 MB permitidos ({zipBytes.Length / 1024 / 1024} MB) para frontera {frontera}. Omitido.";
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = frontera });
                    continue;
                }

                // Construir form-data
                var multipart = new RequestHttp.MultipartBody
                {
                    Fields = new Dictionary<string, string> { { "cgm", frontera } },
                    Files =
                    [
                        new() {
                            FieldName = "ArchivoZip",
                            FileName = $"reportelecturas_{frontera}_{dateRead:yyyyMMdd}.zip",
                            ContentType = "application/zip",
                            Bytes = zipBytes
                        }
                    ]
                };

                // Enviar a XM
                XmReporteResponse result = null;
                try
                {
                    progress?.Report(new ResultadoLectura { Exito = true, Mensaje = $"Enviando lecturas a XM para frontera {frontera}", DatosSolicitud = frontera });
                    result = await _requestHttp.CallMethod<XmReporteResponse>(
                        service: "xmLecturas",
                        action: "reporteLecturas",
                        model: multipart,
                        method: HttpMethod.Post,
                        typeBody: RequestHttp.TypeBody.Multipart,
                        token: null
                    );

                    // Evaluar respuesta XM
                    if (result == null)
                    {
                        progress?.Report(new ResultadoLectura { Exito = false, Mensaje = $"XM no respondió para frontera {frontera}", DatosSolicitud = frontera });
                        continue;
                    }

                    var msg = $"XM devolvió mensaje para {frontera}: {result.Mensaje}";
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = frontera, DatosRespuesta = result.Mensaje });

                }
                catch (Exception exSend)
                {
                    var msg = $"Excepción enviando lecturas a XM para frontera {frontera}: {exSend.Message}";
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = frontera, DatosRespuesta = exSend.InnerException?.Message });
                    continue;
                }

                // XM OK -> Registrar proceso en BD
                try
                {
                    var proccessXM = new ProccessXM
                    {
                        BorderIdXM = frontera,
                        ProccessIdXM = result.IdMensaje,
                        Date = dateRead.ToString("yyyy-MM-dd HH:mm:ss"),
                        NameFile = string.Join('|', archivosJson.Keys),
                        DatoEnviado = JsonSerializer.Serialize(archivosJson)
                    };

                    var dbResp = await _objTransaction.SetProccessXM(proccessXM);
                    var msgDb = $"Proceso XM registrado en Base Datos: {dbResp.StatusMessage}";
                    _responseHelper.Info(msgDb);
                    progress?.Report(new ResultadoLectura { Exito = true, Mensaje = $"Registro proceso XM correcto para frontera {frontera}", DatosSolicitud = frontera, DatosRespuesta = result.IdMensaje });

                    // marcar success parcial (al menos 1 frontera)
                    response.Ok($"Envios a XM procesados OK para frontera {frontera}");
                }
                catch (Exception exDb)
                {
                    var msg = $"Error registrando proceso XM en BD para frontera {frontera}: {exDb.Message}";
                    progress?.Report(new ResultadoLectura { Exito = false, Mensaje = msg, DatosSolicitud = frontera, DatosRespuesta = exDb.InnerException?.Message });
                }
            }

            return response;
        }

        public async Task<XmReporteResponse> ReportarLecturasXmAsync(Dictionary<string, string> archivosJson, string cgm)
        {
            // Crear ZIP usando tu extension method
            var zipBytes = ExtensionMethods.CrearZipLecturasMultiple(archivosJson);

            // VALIDACIÓN XM → máximo 25MB
            if (zipBytes.Length > (25 * 1024 * 1024))
                throw new Exception($"El ZIP excede los 25 MB permitidos ({zipBytes.Length / 1024 / 1024} MB).");

            // Construir form-data
            var multipart = new RequestHttp.MultipartBody
            {
                Fields = new Dictionary<string, string> { { "cgm", cgm } },
                Files =
                [
                    new RequestHttp.FileMultipart
                    {
                        FieldName = "ArchivoZip",
                        FileName = "reportelecturas.zip",
                        ContentType = "application/zip",
                        Bytes = zipBytes
                    }
                ]
            };

            var result = await _requestHttp.CallMethod<XmReporteResponse>(
                service: "xmLecturas",
                action: "reporteLecturas",
                model: multipart,
                method: HttpMethod.Post,
                typeBody: RequestHttp.TypeBody.Multipart,
                token: null
            );

            return result;
        }

        #endregion

        #region Consulta de proceso XM

        // -----------------------------------------------------------------------
        // Consulta proceso XM
        // -----------------------------------------------------------------------
        public async Task<Response<string>> ConsultarProcceso(IProgress<ResultadoLectura> progress = null)
        {
            var response = new Response<string>();
            try
            {
                var dataProccess = await _objTransaction.GetProccessXM();
                if (dataProccess != null && dataProccess.Result != null)
                {
                    foreach (var item in dataProccess.Result)
                    {
                        var msg = $"Consulta en base de datos de id proceso XM: {item.ProccessIdXM}";
                        _responseHelper.Info(msg);
                        progress?.Report(new ResultadoLectura { Exito = true, Mensaje = msg, DatosSolicitud = item.ProccessIdXM });

                        await GetDataXM(item, progress);
                    }
                    response.Ok();
                }
                else
                {
                    _responseHelper.Info("sin datos en consulta en base de datos para procesar");
                    progress?.Report(new ResultadoLectura { Exito = true, Mensaje = "Sin procesos pendientes", DatosSolicitud = null });
                }
            }
            catch (ResultException rex)
            {
                _responseHelper.Error(rex, "Error obteniendo datos de proceso en base de datos");
                progress?.Report(new ResultadoLectura { Exito = false, Mensaje = rex.Message, DatosSolicitud = null });
            }
            catch (Exception ex)
            {
                _responseHelper.Error(ex, "Excepción al consultar proceso en base de datos");
                progress?.Report(new ResultadoLectura { Exito = false, Mensaje = ex.Message, DatosSolicitud = null });
            }
            return response;
        }

        public async Task<XmEstadoResponse> ConsultarEstadoXmAsync(string idMensaje)
        {
            var query = new Dictionary<string, string> { ["idmensaje"] = idMensaje };

            return await _requestHttp.CallMethod<XmEstadoResponse>(
                service: "xmLecturas",
                action: "consultaEstado",
                model: query,
                method: HttpMethod.Get,
                typeBody: RequestHttp.TypeBody.Query,
                token: null
            );
        }

        private async Task<Response<string>> GetDataXM(ProccessXM request, IProgress<ResultadoLectura> progress)
        {
            var response = new Response<string>();
            try
            {
                var result = await ConsultarEstadoXmAsync(request.ProccessIdXM);
                var msg = $"respuesta proceso XM: {result.IdMensaje}";
                _responseHelper.Info(msg);
                progress?.Report(new ResultadoLectura { Exito = true, Mensaje = $"XM responded for id {request.ProccessIdXM}", DatosSolicitud = request.ProccessIdXM, DatosRespuesta = JsonSerializer.Serialize(result) });

                request.Respuesta = result.DetallesSolicitud?.FirstOrDefault()?.Estado;
                request.EstadoConsulta = 1;
                var dataDb = await _objTransaction.UpdateProccessXM(request);
                var msgDb = $"registro proceso XM en base de datos: {dataDb.StatusMessage}";
                _responseHelper.Info(msgDb);
                progress?.Report(new ResultadoLectura { Exito = true, Mensaje = $"Proceso actualizado en BD: {request.ProccessIdXM}", DatosSolicitud = request.ProccessIdXM, DatosRespuesta = dataDb.StatusMessage });

                response.Ok();
            }
            catch (ResultException rex)
            {
                _responseHelper.Error(rex, "Error registrando en base de datos proceso consulta de XM");
                progress?.Report(new ResultadoLectura { Exito = false, Mensaje = rex.Message, DatosSolicitud = request.ProccessIdXM });
            }
            catch (Exception ex)
            {
                _responseHelper.Error(ex, "Excepcion registrando consulta de proceso Xm en base de datos");
                progress?.Report(new ResultadoLectura { Exito = false, Mensaje = ex.Message, DatosSolicitud = request.ProccessIdXM });
            }

            return response;
        }

        #endregion
    }
}