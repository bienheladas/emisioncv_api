using System.ComponentModel.DataAnnotations;

namespace Minedu.VC.Issuer.Models
{
    public class Anchor
    {
        [Required]
        public string? TxHash { get; set; }
    }
}
