namespace ApiMMC.Models.Entities
{
    public class Energy
    {
        public DateTime? ReadTime { get; set; }
        public string Status { get; set; }
        public decimal? ActiveExportEnergy { get; set; }
        public decimal? ReactiveExportEnergy { get; set; }
        public decimal? ActiveImportEnergy { get; set; }
        public decimal? ReactiveImportEnergy { get; set; }
        public string NameFile { get; set; }
    }

    public class EnergyInternal
    {
        public string ReadTime { get; set; }
        public string Status { get; set; }
        public string ActiveExportEnergy { get; set; }
        public string ReactiveExportEnergy { get; set; }
        public string ActiveImportEnergy { get; set; }
        public string ReactiveImportEnergy { get; set; }
        public string NameFile { get; set; }
    }

    public class EnergyConfig
    {
        public string MeasureId { get; set; }
        public string BorderId { get; set; }
        public string BorderIdXM { get; set; }
        public decimal KTE { get; set; }
        public string ApplyKTE { get; set; }
        public int MesaurerType { get; set; }
        public int CountReadHour { get; set; }
        public decimal EnergyReadding { get; set; }
        public string ReportDate { get; set; }
    }

    public class EnergyXmInternal
    {
        public string MeasureId { get; set; }
        public string BorderId { get; set; }
        public string BorderIdXM { get; set; }
        public string KTE { get; set; }
        public string ApplyKTE { get; set; }
        public int MesaurerType { get; set; }
        public int CountReadHour { get; set; }
        public string EnergyReadding { get; set; }
        public string ReportDate { get; set; }
    }

    public class EnergyConfigExtend : EnergyConfig
    {
        public bool ActiveKTE => ApplyKTE.Equals("S");
        public string NameFile { get; set; }
    }

    public class MeasureReadConfig
    {
        public int InitRow { get; set; }
        public int RowReadTime { get; set; }
        public int RowStatus { get; set; }
        public int RowActiveExportEnergy { get; set; }
        public int RowReactiveExportEnergy { get; set; }
        public int RowActiveImportEnergy { get; set; }
        public int RowReactiveImportEnergy { get; set; }
    }

    public class MeasureConfig
    {
        public EnergyConfigExtend EnergyConfig { get; set; }
        public MeasureReadConfig MeasureReadConfig { get; set; }
    }

    public class SetEnergy
    {
        public EnergyConfig Measure { get; set; }
        public List<EnergyInternal> Energies { get; set; }
    }

    public class ProccessXM
    {
        public int? IdRegistro { get; set; }
        public string BorderIdXM { get; set; }
        public string ProccessIdXM { get; set; }
        public string NameFile { get; set; }
        public int? EstadoConsulta { get; set; }
        public string Respuesta { get; set; }
        public string Date { get; set; }
        public string DatoEnviado { get; set; }
    }

    public class EnergyXm
    {
        public EnergyConfigExtend Config { get; set; }
        public List<EnergyConfig> Energies { get; set; }
    }
}
