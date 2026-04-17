namespace Minedu.VC.Issuer.Models.Dto
{
    public class GradeDto
    {
        public int Id { get; set; }                 // ID_CONSTANCIA_NOTA
        public string? Competencia{ get; set; }        // DSC_ASIGNATURA
        public string? Area { get; set; }              // DSC_AREA (en entity estaba como "Competencia")
        public string? TipoArea { get; set; }          // DSC_TIPO_AREA
        public string Calificacion { get; set; } = "-"; // NOTA_FINAL_AREA
        public int SolicitudId { get; set; }       // FK (ID_SOLICITUD)

        // New: claves para colgarla del Enrollment correcto
        public int Anio { get; set; }               // ID_ANIO (NON-NULL en tu entity)
        public string? CodigoModular { get; set; }  // COD_MOD
        public string? IdGrado { get; set; }        // opcional: si lo usas en la unión
        public string? GradoDescripcion { get; set; } // útil para UI/depuración
    }
}
