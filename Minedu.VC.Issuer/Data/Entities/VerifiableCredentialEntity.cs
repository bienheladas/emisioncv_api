using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minedu.VC.Issuer.Data.Entities
{
    [Table("cert_credencial_verificable")]
    public class VerifiableCredentialEntity
    {
        [Key]
        [Column("ID_SOLICITUD")]
        public int IdSolicitud { get; set; }

        // Identificación de la VC
        [Column("ID_VC")]
        public string VcId { get; set; } = default!;
        [Column("TIPO_CREDENCIAL")]
        public string CredentialType { get; set; } = default!;
        [Column("DID_EMISOR")]
        public string IssuerDid { get; set; } = default!;

        // Datos de blockchain
        [Column("RED")]
        public string Network { get; set; } = default!;
        [Column("TX_HASH")]
        public string TxHash { get; set; } = default!;
        [Column("COMISION")]
        public long Fee { get; set; }
        [Column("DIRECCION_EMISOR")]
        public string SenderAddress { get; set; } = default!;
        [Column("FECHA_ANCLAJE")]
        public DateTime AnchoredAt { get; set; }

        // Datos criptográficos
        [Column("TIPO_PRUEBA")]
        public string ProofType { get; set; } = default!;
        [Column("METODO_VERIFICACION")]
        public string VerificationMethod { get; set; } = default!;
        [Column("JWS")]
        public string Jws { get; set; } = default!;
        [Column("VC_HASH")]
        public string VcHash { get; set; } = default!;

        // VC completa (JSON firmada)
        [Column("VC_JSON")]
        public string VcJson { get; set; } = default!;

        // Estado
        [Column("ESTADO")]
        public string Status { get; set; } = "Anchored";
        [Column("FECHA_REGISTRO")]
        public DateTime? CreateTime { get; set; }
        [Column("INDICE_LISTA_ESTADOS")]
        public int? StatusListIndex { get; set; }
    }
}
