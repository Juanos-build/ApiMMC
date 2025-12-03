using ApiMMC.Models.Entities;
using ApiMMC.Services.Helpers.Integration;
using ApiMMC.Services.Helpers.Settings;
using SpreadsheetLight;
using System.IO.Compression;
using System.Text.Json;

namespace ApiMMC.Services.Helpers.Extension
{
    public static class ExtensionMethods
    {
        public static AppSettings Decrypt(this AppSettings appSettings)
        {
            if (appSettings.FileSettings != null)
            {
                appSettings.FileSettings.FileToProcess = @$"{appSettings.FileSettings.Directory}/{appSettings.FileSettings.FileToProcess}";
                appSettings.FileSettings.FileProcessed = @$"{appSettings.FileSettings.Directory}/{appSettings.FileSettings.FileProcessed}";
                appSettings.FileSettings.FileNoProcessed = @$"{appSettings.FileSettings.Directory}/{appSettings.FileSettings.FileNoProcessed}";
            }

            appSettings.Integration?.Services
                .ForEach(a => a.Methods.ForEach(m => m.Value = $"{a.Url}{m.Value}"));

            return appSettings;
        }

        public static Response<T> Ok<T>(this Response<T> data, string message = "OK")
        {
            data.StatusMessage = message;
            data.IsSuccess = true;

            return data;
        }

        public static Response<T> Fail<T>(string message)
        {
            return new Response<T>
            {
                Result = default,
                StatusMessage = message,
                StatusCode = -1,
                IsSuccess = false
            };
        }

        // Mueve archivo, si existe origen
        public static void MoveFileSafe(string source, string dest)
        {
            try
            {
                if (File.Exists(source))
                {
                    var destDir = Path.GetDirectoryName(dest);
                    if (!Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);
                    File.Move(source, dest, true);
                }
            }
            catch
            {
                // swallow (ya está loggeado por llamador)
            }
        }

        public static List<Energy> LeerArchivoEnergia(string filePath, MeasureReadConfig setting, string nombreArchivo)
        {
            var energies = new List<Energy>();
            using var sl = new SLDocument(filePath);
            int iRow = setting.InitRow;

            while (!string.IsNullOrEmpty(sl.GetCellValueAsString(iRow, 1)))
            {
                energies.Add(new Energy
                {
                    ReadTime = sl.GetCellValueAsDateTime(iRow, setting.RowReadTime),
                    Status = sl.GetCellValueAsString(iRow, setting.RowStatus),
                    ActiveExportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowActiveExportEnergy),
                    ReactiveExportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowReactiveExportEnergy),
                    ActiveImportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowActiveImportEnergy),
                    ReactiveImportEnergy = sl.GetCellValueAsDecimal(iRow, setting.RowReactiveImportEnergy),
                    NameFile = nombreArchivo
                });

                iRow++;
            }

            return energies;
        }
        public static void AplicarKTE(List<Energy> energies, EnergyConfigExtend config)
        {
            if (config == null) return;
            if (!config.ActiveKTE) return;

            for (int i = 0; i < energies.Count; i++)
                energies[i].ActiveExportEnergy *= config.KTE;
        }

        // Construye exactamente 24 lecturas respetando CountReadHour
        public static List<EnergyConfig> Construir24Lecturas(List<Energy> energies, EnergyConfigExtend config, DateTime fecha)
        {
            var result = new List<EnergyConfig>();
            int skip = 0;

            for (int hour = 1; hour <= 24; hour++)
            {
                var chunk = energies.Skip(skip).Take(config.CountReadHour).ToList();
                var read = chunk.Sum(x => x.ActiveExportEnergy);
                var firstReadDate = chunk.FirstOrDefault()?.ReadTime?.ToString("HH:mm:ss.fff") ?? "00:00:00.000";

                result.Add(new EnergyConfig
                {
                    EnergyReadding = read ?? 0,
                    MeasureId = config.MeasureId,
                    BorderIdXM = config.BorderIdXM,
                    MesaurerType = config.MesaurerType,
                    ReportDate = $"{fecha:yyyy-MM-dd} {firstReadDate}"
                });

                skip += config.CountReadHour;
            }

            return result;
        }

        public static string CrearJsonLecturas(string frtId, DateTime fecha, List<decimal> valores)
        {
            if (valores == null || valores.Count != 24)
                throw new Exception($"La frontera {frtId} debe tener exactamente 24 valores. Tiene {valores?.Count ?? 0}");

            var archivo = new XmLecturaJson
            {
                FrtID = frtId,
                Inicio = fecha.Date.AddHours(0),      // 00:00
                Fin = fecha.Date.AddDays(1).AddSeconds(-1), // 23:59:59
                Valores = [.. valores
                    .Select((v, i) => new XmLecturaValor
                    {
                        Periodo = i + 1,
                        Valor = v
                    })]
            };

            var lista = new List<XmLecturaJson> { archivo };
            return JsonSerializer.Serialize(lista, RequestHttp.JsonOptions);
        }

        public static byte[] CrearZipLecturasMultiple(Dictionary<string, string> jsonPorNombre)
        {
            using var memoria = new MemoryStream();

            using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, true))
            {
                foreach (var item in jsonPorNombre)
                {
                    string nombreArchivo = item.Key;       // ejemplo: "frt123.json"
                    string contenidoJson = item.Value;     // contenido del json

                    var entrada = zip.CreateEntry(nombreArchivo, CompressionLevel.Optimal);

                    using var streamEntrada = entrada.Open();
                    using var writer = new StreamWriter(streamEntrada);
                    writer.Write(contenidoJson);
                }
            }

            return memoria.ToArray();
        }
    }
}
