namespace Minedu.VC.Issuer.Models.Dto
{
    public class RequestAggregateDto
    {
        // From CERT_SOLICITUD
        public int SolicitudId { get; set; }  // ID_SOLICITUD
        public int EstudianteId { get; set; } // ID_ESTUDIANTE
        public string? IdModalidad { get; set; }
        public string? Modalidad { get; set; }
        public string? IdNivel { get; set; }
        public string? Nivel { get; set; }
        public string? IdGrado { get; set; }
        public string? GradoDescripcion { get; set; }
        public string? CodigoVirtual { get; set; }
        public string? Director { get; set; }

        // Related aggregates
        public StudentDto? Student { get; set; }
        public List<EnrollmentDto> Enrollments { get; set; }   
        public List<ObservationDto> Observations { get; set; } = new();
    }
}
