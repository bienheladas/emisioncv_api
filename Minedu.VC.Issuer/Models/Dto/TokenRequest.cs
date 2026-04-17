using Microsoft.AspNetCore.Mvc;

namespace Minedu.VC.Issuer.Models.Dto
{
    public class TokenRequest
    {
        [FromForm(Name = "grant_type")]
        public string grant_type { get; set; } = default!;
        [FromForm(Name = "pre-authorized_code")]
        public string? pre_authorized_code { get; set; }
        [FromForm(Name = "user_pin")]
        public string? user_pin { get; set; }
    }
}
