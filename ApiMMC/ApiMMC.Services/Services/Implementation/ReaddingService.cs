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
    public class ReaddingService(
        AppSettings settings,
        ITransactionDao transactionDao,
        RequestHttp requestHttp,
        ResponseHelper responseHelper,
        IMapper mapper) : IReaddingService
    {
        private readonly ITransactionDao _objTransaction = transactionDao;
        private readonly AppSettings _appSettings = settings;
        private readonly ResponseHelper _responseHelper = responseHelper;
        private readonly RequestHttp _requestHttp = requestHttp;
        private readonly IMapper _mapper = mapper;
        private MeasureConfig _energyConfig = new();
        private readonly List<EnergyXm> _energyXM = [];

        #region Lectura y Envio de Valores
        public async Task<Response<string>> SetEnergyRead(IProgress<ResultadoLectura> progress = null)
        {
            var response = new Response<string>();
            string file = default;
            string nameFile = default;
            try
            {
                var files = Directory.GetFiles(_appSettings.FileSettings.FileToProcess, "*.xlsx");
                if (files != null & files.Length > 0)
                {
                    foreach (var itemFile in files)
                    {
                        var energies = new List<Energy>();
                        var settingEnergy = new EnergyConfigExtend();
                        string FileProcessed = string.Empty;
                        string FileNoProcessed = string.Empty;
                        var resultDb = new Response<string>();
                        try
                        {
                            file = itemFile;
                            nameFile = Path.GetFileName(file);

                            using var sl = new SLDocument(file);
                            {
                                var energyMeasure = new EnergyConfig
                                {
                                    MeasureId = sl.GetCurrentWorksheetName()?.Split('-')?[1]?.Trim()?.ToUpper()
                                };
                                var dataConfig = await _objTransaction.GetEnergyParameter(energyMeasure);
                                _energyConfig = dataConfig.Result;

                                if (_energyConfig.MeasureReadConfig != null & _energyConfig.EnergyConfig != null)
                                {
                                    var setting = _energyConfig.MeasureReadConfig;
                                    int iRow = setting.InitRow;
                                    while (!string.IsNullOrEmpty(sl.GetCellValueAsString(iRow, 1)))
                                    {
                                        var energy = new Energy
                                        {
                                            ReadTime = sl.GetCellValueAsDateTime(iRow, setting.RowReadTime),
                                            Status = sl.GetCellValueAsString(iRow, setting.RowStatus),
                                            ActiveExportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowActiveExportEnergy),
                                            ReactiveExportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowReactiveExportEnergy),
                                            ActiveImportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowActiveImportEnergy),
                                            ReactiveImportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowReactiveImportEnergy),
                                            NameFile = nameFile
                                        };
                                        energies.Add(energy);
                                        iRow++;
                                    }
                                }
                                else
                                {
                                    FileProcessed = $"{_appSettings.FileSettings.FileNoProcessed}/{nameFile}";
                                    throw new Exception($"No se encuentra configuración de lectura de archivo para el serial {energyMeasure.MeasureId}. Archivo: {FileProcessed}.");
                                }
                            }
                            settingEnergy = _energyConfig.EnergyConfig;
                            var setEnergy = new SetEnergy
                            {
                                Measure = settingEnergy,
                                Energies = _mapper.Map<List<EnergyInternal>>(energies)
                            };
                            resultDb = await _objTransaction.SetEnergyRead(setEnergy);
                            _responseHelper.Info($"Lecturas de archivo registradas. {resultDb.StatusMessage}.");

                            FileProcessed = $"{_appSettings.FileSettings.FileProcessed}/{nameFile}";
                            File.Move(file, FileProcessed, true);

                            _responseHelper.Info($"Archivo procesado con éxito. directorio {FileProcessed}.");
                        }
                        catch (ResultException ex)
                        {
                            FileNoProcessed = $"{_appSettings.FileSettings.FileNoProcessed}/{nameFile}";
                            File.Move(file, FileNoProcessed, true);
                            _responseHelper.Error(ex, "Error registrando lecturas de archivo en base de datos.");
                        }
                        catch (Exception ex)
                        {
                            if (!string.IsNullOrEmpty(file))
                            {
                                FileProcessed = $"{_appSettings.FileSettings.FileNoProcessed}/{nameFile}";
                                File.Move(file, FileNoProcessed, true);
                            }
                            _responseHelper.Error(ex, "Error procesando archivo de lecturas.");
                        }

                        try
                        {
                            if (settingEnergy != null)
                            {
                                if (settingEnergy.ActiveKTE)
                                    energies.ForEach(e => e.ActiveExportEnergy *= settingEnergy.KTE);

                                settingEnergy.NameFile = FileProcessed;
                                var energyRead = new List<EnergyConfig>();
                                int skip = 0;
                                //se toma fecha del dia igual para todas las lecturas por lo que el ultimo registro llega del dia posterior
                                var date = energies?.FirstOrDefault()?.ReadTime?.Date;
                                for (var e = 1; e <= 24; e++)
                                {
                                    var read = energies.Skip(skip).Take(settingEnergy.CountReadHour).Sum(s => s.ActiveExportEnergy);
                                    var readDate = energies.Skip(skip).Take(settingEnergy.CountReadHour).FirstOrDefault().ReadTime?.ToString("HH:mm:ss.fff");
                                    var readMX = new EnergyConfig
                                    {
                                        EnergyReadding = read ?? 0,
                                        MeasureId = settingEnergy.MeasureId,
                                        BorderIdXM = settingEnergy.BorderIdXM,
                                        MesaurerType = settingEnergy.MesaurerType,
                                        ReportDate = $"{date?.ToString("yyyy-MM-dd")} {readDate}"
                                    };
                                    energyRead.Add(readMX);
                                    skip += settingEnergy.CountReadHour;
                                }
                                var energyXmInternal = _mapper.Map<List<EnergyXmInternal>>(energyRead);
                                resultDb = await _objTransaction.SetEnergyXM(energyXmInternal);
                                _responseHelper.Info($"Reporte energia XM registradas. {resultDb.StatusMessage}");

                                var energyXM = new EnergyXm
                                {
                                    Config = settingEnergy,
                                    Energies = energyRead
                                };
                                _energyXM.Add(energyXM);
                            }
                            else
                                _responseHelper.Warn("No se encuentra configuracion para procesar a XM");
                        }
                        catch (ResultException ex)
                        {
                            _responseHelper.Error(ex, "Error registrando cálculo de lecturas en base de datos");
                        }
                        catch (Exception ex)
                        {
                            _responseHelper.Error(ex, "Error procesando cálculo de lecturas");
                        }
                    }
                    if (_energyXM.Count > 0)
                    {
                        response = await ProcesarLecturasXmAsync();
                        _responseHelper.Success(response);
                    }
                }
                else
                    _responseHelper.Warn("No se encuentra archivo para procesar");
            }
            catch (Exception ex)
            {
                _responseHelper.Error(ex, "Error procesando lecturas de archivo");
            }
            return response;
        }

        public async Task<Response<string>> ProcesarLecturasXmAsync()
        {
            var response = new Response<string>();

            var dateRead = DateTime.Today.AddDays(-1).Date;

            var fronteras = _energyXM.Select(d => d.Config.BorderIdXM).Distinct().ToList();

            foreach (var frontera in fronteras)
            {
                var energies = _energyXM.Where(e => e.Config.BorderIdXM == frontera).ToList();

                // JSON por archivo
                var archivosJson = new Dictionary<string, string>();

                foreach (var energy in energies)
                {
                    if (string.IsNullOrEmpty(energy.Config.BorderIdXM))
                    {
                        _responseHelper.Warn($"Id Frontera [{energy.Config.BorderIdXM}] y Id Medidor [{energy.Config.MeasureId}]. No aplicar envío a XM [BorderIdXM no configurado en base de datos]");
                        continue;
                    }

                    _responseHelper.Info($"Id Frontera [{energy.Config.BorderIdXM}] y Id Medidor [{energy.Config.MeasureId}]. Configurado para enviar a XM - OK");

                    // nombre archivo basado en medidor
                    string nombreJson = $"{energy.Config.MeasureId}_{dateRead:yyyyMMdd}.json";

                    // generar JSON
                    var readItems = energy.Energies.Select(e => e.EnergyReadding).ToList();
                    var json = ExtensionMethods.CrearJsonLecturas(
                        frtId: energy.Config.BorderIdXM,
                        fecha: dateRead,
                        valores: readItems
                    );

                    archivosJson.Add(nombreJson, json);
                }

                // no mandar ZIP vacío
                if (archivosJson.Count == 0)
                    continue;

                // VALIDACIÓN XM → Máximo 5 archivos por ZIP
                if (archivosJson.Count > 5)
                {
                    _responseHelper.Warn($"Frontera {frontera} tiene {archivosJson.Count} archivos JSON. XM permite máximo 5.");
                    continue;
                }

                // enviar ZIP a XM
                XmReporteResponse result = default;
                try
                {
                    result = await ReportarLecturasXmAsync(
                        archivosJson,
                        cgm: frontera
                    );

                    _responseHelper.Info($"Lecturas enviadas con exito: id XM, {result.IdMensaje}");

                    var proccessXM = new ProccessXM
                    {
                        BorderIdXM = frontera,
                        ProccessIdXM = result.IdMensaje,
                        Date = dateRead.ToString("yyyy-MM-dd HH:mm:ss"),
                        NameFile = string.Join('|', archivosJson.Keys),
                        DatoEnviado = JsonSerializer.Serialize(archivosJson)
                    };
                    await _objTransaction.SetProccessXM(proccessXM);
                    _responseHelper.Info($"Proceso XM registrado en Base Datos");
                    _responseHelper.Success(response);
                }
                catch (ResultException ex)
                {
                    _responseHelper.Error(ex, "Error registarndo resultado en Base Datos");
                }
                catch (Exception ex)
                {
                    _responseHelper.Error(ex, $"Excepción enviando lecturas a XM");
                }
            }
            return response;
        }

        public async Task<XmReporteResponse> ReportarLecturasXmAsync(Dictionary<string, string> archivosJson, string cgm)
        {
            // Crear ZIP
            var zipBytes = ExtensionMethods.CrearZipLecturasMultiple(archivosJson);

            // VALIDACIÓN XM → máximo 25MB
            if (zipBytes.Length > (25 * 1024 * 1024))
            {
                throw new Exception($"El ZIP excede los 25 MB permitidos ({zipBytes.Length / 1024 / 1024} MB).");
            }

            // Construir form-data
            var multipart = new RequestHttp.MultipartBody
            {
                Fields = new Dictionary<string, string>
                {
                    { "cgm", cgm }
                },
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

            // Llamar a XM usando RequestHttp
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

        public async Task<Response<string>> ConsultarProcceso(IProgress<ResultadoLectura> progress = null)
        {
            var response = new Response<string>();
            try
            {
                var dataProccess = _objTransaction.GetProccessXM().Result;
                if (dataProccess != null)
                {
                    foreach (var item in dataProccess.Result)
                    {
                        _responseHelper.Info($"consulta en base de datos de id proceso XM: {item.ProccessIdXM}");
                        await GetDataXM(item);
                    }
                    _responseHelper.Success(response);
                }
                else
                    _responseHelper.Info($"sin datos en consulta en base de datos para procesar");
            }
            catch (ResultException ex)
            {
                _responseHelper.Error(ex, "Error obteniendo datos de proceso en base de datos");
            }
            catch (Exception ex)
            {
                _responseHelper.Error(ex, "Exceción al consultar proceso en base de datos");
            }
            return response;
        }

        public async Task<XmEstadoResponse> ConsultarEstadoXmAsync(string idMensaje)
        {
            var query = new Dictionary<string, string>
            {
                ["idmensaje"] = idMensaje
            };

            return await _requestHttp.CallMethod<XmEstadoResponse>(
                service: "xmLecturas",
                action: "consultaEstado",
                model: query,
                method: HttpMethod.Get,
                typeBody: RequestHttp.TypeBody.Query,
                token: null
            );
        }

        private async Task<Response<string>> GetDataXM(ProccessXM request)
        {
            var response = new Response<string>();
            try
            {
                var result = await ConsultarEstadoXmAsync(request.ProccessIdXM);
                _responseHelper.Info($"respuesta proceso XM: {result.IdMensaje}");

                try
                {
                    request.Respuesta = result.DetallesSolicitud?.FirstOrDefault()?.Estado;
                    request.EstadoConsulta = 1;
                    var dataDb = await _objTransaction.UpdateProccessXM(request);
                    _responseHelper.Info($"registro proceso XM en base de datos: {dataDb.StatusMessage}");
                    _responseHelper.Success(response);
                }
                catch (ResultException ex)
                {
                    _responseHelper.Error(ex, "Error registrando en base de datos proceso consulta de XM");
                }
                catch (Exception ex)
                {
                    _responseHelper.Error(ex, "Excepcion registrando consulta de proceso Xm en base de datos");
                }
            }
            catch
            {
                throw;
            }
            return response;
        }
        #endregion
    }
}
