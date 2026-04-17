namespace Minedu.VC.Issuer.Models.Dto
{
    public class EnrollmentDto
    {
        public int IdSolicitud { get; set; }
        public string? IdNivel { get; set; }
        public string? IdGrado { get; set; }
        public string? GradoDescripcion { get; set; }
        public int? Anio { get; set; }           // matches ID_ANIO (int?)
        public string? CodigoModular { get; set; }
        public string? Anexo { get; set; }
        public string? SituacionFinal { get; set; }

        // New: parsed numeric grade (1..6, etc.) para ordenar y mostrar
        public int GradoNumero { get; set; }

        // New: the notes that belong ONLY to this (Año, CodMod[, IdGrado])
        public List<GradeDto> Notas { get; set; } = new();

    }
}
