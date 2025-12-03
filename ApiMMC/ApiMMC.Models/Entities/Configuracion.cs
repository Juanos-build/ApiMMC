namespace ApiMMC.Models.Entities
{
    public class ResultadoLectura
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public object DatosSolicitud { get; set; }
        public string DatosRespuesta { get; set; }
    }
}
