using System.Text.Json.Serialization;

namespace ApiMMC.Models.Entities
{
    public class LecturaFrontera
    {
        public string FronteraID { get; set; } // BorderIdXM
        public DateTime FechaLectura { get; set; }
        public List<decimal> Valores { get; set; } = [];
        public string CGM { get; set; }
    }

    public class XmLecturaJson
    {
        [JsonPropertyName("tipoReporte")]
        public string TipoReporte { get; set; } = "LecturaDiaria";

        [JsonPropertyName("frtID")]
        public string FrtID { get; set; } // BorderIdXM

        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = "SR";

        [JsonPropertyName("duracion")]
        public string Duracion { get; set; } = "D";

        [JsonPropertyName("duracionPeriodo")]
        public string DuracionPeriodo { get; set; } = "PT1H";

        [JsonPropertyName("inicio")]
        public DateTime Inicio { get; set; }

        [JsonPropertyName("fin")]
        public DateTime Fin { get; set; }

        [JsonPropertyName("valores")]
        public List<XmLecturaValor> Valores { get; set; } = [];
    }

    public class XmLecturaValor
    {
        [JsonPropertyName("periodo")]
        public int Periodo { get; set; }

        [JsonPropertyName("valor")]
        public decimal Valor { get; set; }
    }

    public class XmToken
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; set; }
    }

    public class XmReporteResponse
    {
        [JsonPropertyName("idMensaje")]
        public string IdMensaje { get; set; }

        [JsonPropertyName("mensaje")]
        public string Mensaje { get; set; }
    }

    public class XmEstadoResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("idMensaje")]
        public string IdMensaje { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; }

        [JsonPropertyName("cgm")]
        public string Cgm { get; set; }

        [JsonPropertyName("usuario")]
        public string Usuario { get; set; }

        [JsonPropertyName("correo")]
        public string Correo { get; set; }

        [JsonPropertyName("ruta")]
        public string Ruta { get; set; }

        [JsonPropertyName("detallesSolicitud")]
        public List<XmDetalleSolicitud> DetallesSolicitud { get; set; }

        [JsonPropertyName("fechaRegistro")]
        public DateTime FechaRegistro { get; set; }

        [JsonPropertyName("fechaUltimaActualizacion")]
        public DateTime FechaUltimaActualizacion { get; set; }
    }

    public class XmDetalleSolicitud
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; }

        [JsonPropertyName("estado")]
        public string Estado { get; set; }

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; }

        [JsonPropertyName("fechaRegistro")]
        public DateTime FechaRegistro { get; set; }

        [JsonPropertyName("fechaUltimaActualizacion")]
        public DateTime FechaUltimaActualizacion { get; set; }
    }
}
