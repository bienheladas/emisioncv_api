using System.Collections.Concurrent;

namespace Minedu.VC.Issuer.Services.Auth
{
    public record PreAuthRecord(int IdSolicitud, DateTime CreatedUtc, bool Used);
    public record AccessTokenRecord(string Token, int IdSolicitud, DateTime ExpiresUtc);

    public interface ITokenStore
    {
        void SavePreAuth(string code, PreAuthRecord record);
        PreAuthRecord? GetPreAuth(string code);
        void MarkPreAuthUsed(string code);

        void SaveAccessToken(AccessTokenRecord token);
        AccessTokenRecord? GetAccessToken(string token);
    }

    public class InMemoryTokenStore : ITokenStore
    {
        private readonly ConcurrentDictionary<string, PreAuthRecord> _pre = new();
        private readonly ConcurrentDictionary<string, AccessTokenRecord> _tok = new();

        public void SavePreAuth(string code, PreAuthRecord record) => _pre[code] = record;
        public PreAuthRecord? GetPreAuth(string code) => _pre.TryGetValue(code, out var r) ? r : null;
        public void MarkPreAuthUsed(string code)
        {
            if (_pre.TryGetValue(code, out var r))
                _pre[code] = r with { Used = true };
        }

        public void SaveAccessToken(AccessTokenRecord token) => _tok[token.Token] = token;
        public AccessTokenRecord? GetAccessToken(string token) => _tok.TryGetValue(token, out var r) ? r : null;
    }
}
