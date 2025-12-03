using System.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiMMC.Models.Entities
{
    public class Response<T>
    {
        public bool IsSuccess { get; set; }
        public int? StatusCode { set; get; }
        public string StatusMessage { set; get; }
        public string MessageDetail { set; get; }
        public T Result { set; get; }
    }

    public class ResponseProblem
    {
        [JsonPropertyName("type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Type { get; set; }
        [JsonPropertyName("title"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Title { get; set; }
        [JsonPropertyName("status"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Status { get; set; }
        [JsonPropertyName("detail"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Detail { get; set; }
        [JsonPropertyName("statusCode"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? StatusCode { set; get; }
        [JsonPropertyName("statusMessage"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string StatusMessage { set; get; }

        public override string ToString() => JsonSerializer.Serialize(this);
    }

    public class Utility
    {
        public static DataTable ConvertToDataTable<T>(T entity)
        {
            var properties = typeof(T).GetProperties();
            var table = new DataTable();
            foreach (var property in properties)
            {
                table.Columns.Add(property.Name, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType);
            }
            table.Rows.Add([.. properties.Select(p => p.GetValue(entity, null))]);
            return table;
        }

        public static DataTable ToDataTable<T>(List<T> items)
        {
            var dataTable = new DataTable(typeof(T).Name);
            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name);
            }
            foreach (T item in items)
            {
                var values = new object[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                {
                    //inserting property values to datatable rows
                    values[i] = Props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }
            //put a breakpoint here and check datatable
            return dataTable;
        }
    }

    // Base para todas las excepciones
    public abstract class AppException(string message, int? errorCode = -1, Exception inner = null) : Exception(message, inner)
    {
        public int? ErrorCode { get; } = errorCode;
    }

    // Excepción de la capa de datos
    public class DataAccessException(string message, Exception inner = null) : AppException(message, -100, inner)
    {
    }

    // Excepción de negocio
    public class BusinessException(string message, int? errorCode = -200, Exception inner = null) : AppException(message, errorCode, inner)
    {
    }

    public class TechnicalException(string message, Exception inner = null) : AppException(message, -300, inner)
    {
    }

    // Caso especial: resultado fallido del SP
    public class ResultException(int? errorCode, string message) : BusinessException(message, errorCode)
    {
    }

    // Excepción para errores inesperados
    public class UnexpectedException(string message, Exception inner = null) : AppException(message, -999, inner)
    {
    }
}
