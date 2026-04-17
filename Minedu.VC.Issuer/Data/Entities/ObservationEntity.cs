using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minedu.VC.Issuer.Data.Entities
{
    [Table("cert_observacion_for_vc")]
    public class ObservationEntity
    {
        [Key]
        [Column("ID_CERTIFICADO_OBSERVACION")]
        public int Id { get; set; }

        [Column("ID_SOLICITUD")]
        public int SolicitudId { get; set; }

        [ForeignKey("SolicitudId")]
        public RequestEntity Solicitud { get; set; } = null!;

        [Column("RESOLUCION")]
        public string? Resolucion { get; set; }

        [Column("MOTIVO")]
        public string? Motivo { get; set; }

        [Column("TIPO_SOLICITUD")]
        public int? TipoSolicitud { get; set; }

        [Column("ID_ANIO")]
        public int? ID_ANIO { get; set; }
    }
}
