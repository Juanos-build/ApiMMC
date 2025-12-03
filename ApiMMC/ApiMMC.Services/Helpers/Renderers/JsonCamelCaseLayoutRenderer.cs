using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiMMC.Services.Helpers.Renderers
{
    [LayoutRenderer("jsonCamelCase")]
    [ThreadAgnostic]
    public class JsonCamelCaseLayoutRenderer : LayoutRenderer
    {
        /// <summary>
        /// Si incluir todas las propiedades del log event
        /// </summary>
        [DefaultParameter]
        public bool IncludeAllProperties { get; set; } = true;

        /// <summary>
        /// Límite de recursión para evitar ciclos infinitos en objetos anidados
        /// </summary>
        public int MaxRecursionLimit { get; set; } = 10;

        private readonly JsonSerializerOptions _serializerOptions;

        public JsonCamelCaseLayoutRenderer()
        {
            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
                MaxDepth = MaxRecursionLimit
            };
        }

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            if (logEvent == null)
                return;

            object toSerialize;

            if (IncludeAllProperties && logEvent.HasProperties)
            {
                // Serializa todas las propiedades del evento (request, response, EventType, etc.)
                toSerialize = logEvent.Properties;
            }
            else
            {
                // Si no hay propiedades, serializamos datos básicos del evento
                toSerialize = new
                {
                    level = logEvent.Level.ToString(),
                    message = logEvent.Message,
                    exception = logEvent.Exception?.Message,
                    stackTrace = logEvent.Exception?.StackTrace
                };
            }

            try
            {
                var json = JsonSerializer.Serialize(toSerialize, _serializerOptions);
                builder.Append(json);
            }
            catch (Exception ex)
            {
                builder.Append($"{{\"serializationError\":\"{ex.Message}\"}}");
            }
        }
    }
}
