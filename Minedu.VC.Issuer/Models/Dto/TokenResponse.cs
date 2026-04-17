namespace Minedu.VC.Issuer.Models.Dto
{
    public class TokenResponse
    {
        public string access_token { get; set; } = default!;
        public string token_type { get; set; } = "bearer";
        public int expires_in { get; set; }
    }
}
