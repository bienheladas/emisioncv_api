using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minedu.VC.Issuer.Data.Entities
{
    [Table("cert_grado_for_vc")]
    public class EnrollmentEntity
    {
        [Key]
        [Column("ID_CONSTANCIA_GRADO")]
        public int IdConstanciaGrado { get; set; }

        [Column("ID_SOLICITUD")]
        public int IdSolicitud { get; set; }

        [Column("ID_NIVEL")]
        public string? IdNivel { get; set; }

        [Column("ID_GRADO")]
        public string? IdGrado { get; set; }

        [Column("DSC_GRADO")]
        public string? GradoDescripcion { get; set; }

        [Column("ID_ANIO")]
        public int? Anio { get; set; }

        [Column("COD_MOD")]
        public string? CodigoModular { get; set; }

        [Column("ANEXO")]
        public string? Anexo { get; set; }

        [Column("SITUACION_FINAL")]
        public string? SituacionFinal { get; set; }

        [ForeignKey("IdSolicitud")]
        public RequestEntity Solicitud { get; set; } = null!;

        public ICollection<GradeEntity> Notas { get; set; } = new List<GradeEntity>();
    }
}
