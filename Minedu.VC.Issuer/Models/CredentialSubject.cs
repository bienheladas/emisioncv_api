using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Models
{
    public class CredentialSubject
    {
        [JsonPropertyName("id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Id { get; set; }

        [JsonPropertyName("modalidad")]
        public string Modalidad { get; set; } = string.Empty;         // EBR / EBA / EBE
        [JsonPropertyName("nivel")]
        public string Nivel { get; set; } = string.Empty;             // A1, B0, etc.
        [JsonPropertyName("codigoVirtual")]
        public Guid CodigoVirtual { get; set; } = Guid.NewGuid();
        [JsonPropertyName("titular")]
        public Titular Titular { get; set; } = new();
        [JsonPropertyName("gradosConcluidos")]
        public List<GradoConcluido> GradosConcluidos { get; set; } = new();
    }

    public class Titular
    {
        [JsonPropertyName("nombres")]
        public string Nombres { get; set; } = string.Empty;
        [JsonPropertyName("apellidos")]
        public string Apellidos { get; set; } = string.Empty;
        [JsonPropertyName("tipoDocumento")]
        public string TipoDocumento { get; set; } = string.Empty;
        [JsonPropertyName("numeroDocumento")]
        public string NumeroDocumento { get; set; } = string.Empty;
    }

    public class GradoConcluido
    {
        [JsonPropertyName("grado")]
        public int Grado { get; set; }
        [JsonPropertyName("anio")]
        public int Anio { get; set; }
        [JsonPropertyName("codigoModular")]
        public string CodigoModular { get; set; } = string.Empty;
        [JsonPropertyName("anexo")]
        public string Anexo { get; set; } = "0";
        [JsonPropertyName("situacionFinal")]
        public string SituacionFinal { get; set; } = string.Empty;
        [JsonPropertyName("notas")]
        public List<Nota> Notas { get; set; } = new();
    }

    public class Nota
    {
        [JsonPropertyName("area")]
        public string? Area { get; set; }
        [JsonPropertyName("competencia")]
        public string? Competencia { get; set; }
        [JsonPropertyName("competenciaTransversal")]
        public string? CompetenciaTransversal { get; set; }
        [JsonPropertyName("calificacion")]
        public string Calificacion { get; set; } = "-";
    }
}
