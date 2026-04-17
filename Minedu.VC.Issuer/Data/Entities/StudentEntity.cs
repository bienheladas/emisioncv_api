using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minedu.VC.Issuer.Data.Entities
{
    [Table("cert_estudiante_for_vc")]
    public class StudentEntity
    {
        [Key]
        [Column("ID_ESTUDIANTE")]
        public int Id { get; set; }

        [Column("ID_TIPO_DOCUMENTO")]
        public string TipoDocumento { get; set; } = string.Empty;

        [Column("NUMERO_DOCUMENTO")]
        public string NumeroDocumento { get; set; } = string.Empty;

        [Column("APELLIDO_PATERNO")]
        public string ApellidoPaterno { get; set; } = string.Empty;

        [Column("APELLIDO_MATERNO")]
        public string ApellidoMaterno { get; set; } = string.Empty;

        [Column("NOMBRES")]
        public string Nombres { get; set; } = string.Empty;

        // Relaciones
        public ICollection<RequestEntity> Solicitudes { get; set; } = new List<RequestEntity>();
    }
}
