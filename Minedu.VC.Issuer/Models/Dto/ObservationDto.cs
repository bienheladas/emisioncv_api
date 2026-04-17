namespace Minedu.VC.Issuer.Models.Dto
{
    public class ObservationDto
    {
        public int Id { get; set; }          // ID_CERTIFICADO_OBSERVACION
        public int SolicitudId { get; set; } // ID_SOLICITUD
        public string? Resolucion { get; set; }
        public string? Motivo { get; set; }
        public int? TipoSolicitud { get; set; }
        public int? Anio { get; set; }        // ID_ANIO
    }
}
