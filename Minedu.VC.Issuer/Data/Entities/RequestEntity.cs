using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minedu.VC.Issuer.Data.Entities
{
    [Table("cert_solicitud_for_vc")]
    public class RequestEntity
    {
        [Key]
        [Column("ID_SOLICITUD")]
        public int Id { get; set; }

        [Column("ID_ESTUDIANTE")]
        public int EstudianteId { get; set; }

        [ForeignKey("EstudianteId")]
        public StudentEntity Estudiante { get; set; } = null!;

        [Column("ABR_MODALIDAD")]
        public string? IdModalidad { get; set; }

        [Column("DSC_MODALIDAD")]
        public string? Modalidad { get; set; }

        [Column("ID_NIVEL")]
        public string? IdNivel { get; set; }

        [Column("DSC_NIVEL")]
        public string? Nivel { get; set; }

        [Column("ID_GRADO")]
        public string? IdGrado { get; set; }

        [Column("DSC_GRADO")]
        public string? GradoDescripcion { get; set; }

        [Column("CODIGO_VIRTUAL")]
        public Guid? CodigoVirtual { get; set; }

        [Column("DIRECTOR")]
        public string? Director { get; set; }

        // Relaciones
        public ICollection<EnrollmentEntity> Grados { get; set; } = new List<EnrollmentEntity>();
        public ICollection<ObservationEntity> Observaciones { get; set; } = new List<ObservationEntity>();
    }
}
