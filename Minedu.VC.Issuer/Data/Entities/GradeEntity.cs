using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Minedu.VC.Issuer.Data.Entities
{
    [Table("cert_nota_for_vc")]
    public class GradeEntity
    {
        [Key]
        [Column("ID_CONSTANCIA_NOTA")]
        public int Id { get; set; }
        [Column("DSC_ASIGNATURA")]
        public string? Competencia { get; set; }
        [Column("DSC_AREA")]
        public string? Area { get; set; }
        [Column("DSC_TIPO_AREA")]
        public string? TipoArea { get; set; }
        [Column("NOTA_FINAL_AREA")]
        public string Calificacion { get; set; } = "-";
        [Column("ID_SOLICITUD")]
        public int SolicitudId { get; set; }   // FK to EnrollmentEntity (CERT_GRADO)
        [Column("ID_ANIO")]
        public int Anio { get; set; } // FK to EnrollmentEntity (CERT_GRADO)
        [Column("COD_MOD")]
        public string? CodigoModular { get; set; } // FK to EnrollmentEntity (CERT_GRADO)
        public EnrollmentEntity Grado { get; set; } = null!;
    }
}
