using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using NSec.Cryptography;
using Minedu.VC.Issuer.Models;
using System.Security.Cryptography;

namespace Minedu.VC.Issuer.Services
{
    public sealed class SignatureService
    {
        private readonly string _keyPath;
        private readonly DidWebResolver _resolver;
        private readonly string _verificationMethodId; // did:web:...#keys-1

        public SignatureService(IConfiguration config, DidWebResolver resolver)
        {
            _keyPath = config["Issuer:KeyPath"]
                ?? throw new InvalidOperationException("Missing Issuer:KeyPath in configuration.");
            _resolver = resolver;
            _verificationMethodId = config["Issuer:VerificationMethodId"] 
                ?? throw new InvalidOperationException("Missing Issuer:VerificationMethodId in configuration.");
        }

        public VerifiableCredential Sign(VerifiableCredential vc)
        {
            // 1) Load JWK (Ed25519, x + d base64url)
            var keyJson = File.ReadAllText(_keyPath);
            var jwk = JsonSerializer.Deserialize<OkpJwk>(keyJson)
                      ?? throw new InvalidOperationException("Invalid JWK file.");

            byte[] d = Base64UrlDecode(jwk.D); // 32 bytes (seed privada)
            byte[] x = Base64UrlDecode(jwk.X); // 32 bytes (clave pública)
            if (d.Length != 32) throw new InvalidOperationException("Ed25519 'd' must be 32 bytes.");
            if (x.Length != 32) throw new InvalidOperationException("Ed25519 'x' must be 32 bytes.");

            // 2) Build unsigned payload (serialize VC WITHOUT 'proof', preserving @context key)
            var unsignedPayloadUtf8 = SerializeVcWithoutProof(vc);

            // 3) Create JWS compact with Ed25519 (alg: EdDSA)
            // kid is optional; if you have one in JWK, pass it. Otherwise omit.
            var kid = jwk.Kid ?? _resolver.GetVerificationMethod();
            var jws = CreateJwsCompact(unsignedPayloadUtf8, jwk.D, kid);

            // 4) Fill the proof
            vc.Proof = new Proof
            {
                Created = DateTime.UtcNow.ToString("o"),
                VerificationMethod = _resolver.GetVerificationMethod(),
                Jws = jws
            };

            return vc;
        }
        
        /// <summary>
        /// Signs any VC-like object (BitstringStatusListCredential or others) that follow the W3C structure.
        /// </summary>
        public T SignGeneric<T>(T vcLike)
        {
            // Serialize object (without proof)
            var json = JsonSerializer.Serialize(vcLike, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            // Parse to JsonDocument to temporarily remove "proof" if exists
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.Clone();

            // Remove 'proof' node (if any)
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.NameEquals("proof")) continue;
                    prop.WriteTo(writer);
                }
                writer.WriteEndObject();
            }

            var unsignedPayload = output.ToArray();

            // Sign the payload using Ed25519
            var keyJson = File.ReadAllText(_keyPath);
            var jwk = JsonSerializer.Deserialize<OkpJwk>(keyJson)
                      ?? throw new InvalidOperationException("Invalid JWK file.");
            var kid = jwk.Kid ?? _resolver.GetVerificationMethod();

            string jws = CreateJwsCompact(unsignedPayload, jwk.D, kid);

            // Attach proof
            var proof = new Proof
            {
                Type = "JsonWebSignature2020",
                Created = DateTime.UtcNow.ToString("o"),
                ProofPurpose = "assertionMethod",
                VerificationMethod = _resolver.GetVerificationMethod(),
                Jws = jws
            };

            // Merge back proof into object dynamically
            var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(json)!;
            obj["Proof"] = proof;

            // Re-serialize to target type
            var finalJson = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<T>(finalJson)!;
        }
        
        /// <summary>
        /// Build a JSON Web Signature 2020 proof with detached payload (b64:false).
        /// Returns an object with fields: type, created, proofPurpose, verificationMethod, jws
        /// </summary>
        public Dictionary<string, object> CreateJws2020DetachedProof(string credentialJsonUtc)
        {
            // 1) Load JWK (OKP/Ed25519) from issuer-key.json
            var jwk = LoadOkpJwk();

            if (jwk.Kty != "OKP" || jwk.Crv != "Ed25519")
                throw new InvalidOperationException("Only OKP/Ed25519 JWK is supported.");

            if (string.IsNullOrWhiteSpace(jwk.D))
                throw new InvalidOperationException("Private key (d) missing in issuer-key.json.");

            // Canonicalize JSON (to ensure deterministic serialization)
            // If you receive an object already serialized, parse and reserialize compactly
            string canonicalJson;
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(credentialJsonUtc);
                canonicalJson = System.Text.Json.JsonSerializer.Serialize(
                    node,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = false,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    });
            }
            catch
            {
                // fallback: assume already canonical
                canonicalJson = credentialJsonUtc;
            }

            // 2) Prepare protected header: EdDSA + detached payload
            //    IMPORTANT: b64:false and crit:["b64"], kid MUST equal your DID key id
            var kid = _verificationMethodId; // must match proof.verificationMethod
            var protectedHeaderObj = new Dictionary<string, object>
            {
                ["alg"] = "EdDSA",
                ["kid"] = kid,
                ["b64"] = false,
                ["crit"] = new[] { "b64" }
            };

            var protectedHeaderJson = JsonSerializer.Serialize(protectedHeaderObj);
            var protectedB64 = Base64Url(Encoding.UTF8.GetBytes(protectedHeaderJson));

            // 3) Detached signing input: ASCII(BASE64URL(protected)) + "." + raw payload (UTF-8)
            var payloadBytes = Encoding.UTF8.GetBytes(canonicalJson);
            var signingInput = Concat(
                Encoding.ASCII.GetBytes(protectedB64),
                Encoding.ASCII.GetBytes("."),      // dot
                payloadBytes                       // raw payload (not base64url)
            );

            var payloadRaw = Encoding.UTF8.GetString(payloadBytes);
            // Guarda en archivo para comparación posterior
            var dumpPath = Path.Combine(AppContext.BaseDirectory, "Data", "last_payload_signed.json");
            File.WriteAllTextAsync(dumpPath, payloadRaw, Encoding.UTF8);

            Console.WriteLine("HEADER_EMISOR: " + protectedB64);
            Console.WriteLine("PAYLOAD_HASH_EMISOR: " + Convert.ToHexString(SHA256.HashData(payloadBytes)));
            Console.WriteLine("PAYLOAD_LEN_EMISOR: " + payloadBytes.Length);
            Console.WriteLine("PUBKEY_EMISOR: " + jwk.X);
            Console.WriteLine("Payload guardado en: " + dumpPath);

            // 4) Sign with Ed25519
            var d = Base64UrlDecode(jwk.D!);
            using var key = Key.Import(SignatureAlgorithm.Ed25519, d, KeyBlobFormat.RawPrivateKey);
            var sig = SignatureAlgorithm.Ed25519.Sign(key, signingInput);
            var sigB64 = Base64Url(sig);

            // 5) Compact JWS (detached): <protected>.. <signature>
            var jwsCompact = $"{protectedB64}..{sigB64}";

            // 6) Build proof
            var created = DateTimeOffset.UtcNow.ToString("o");
            return new Dictionary<string, object>
            {
                ["type"] = "JsonWebSignature2020",
                ["created"] = created,
                ["proofPurpose"] = "assertionMethod",
                ["verificationMethod"] = kid,
                ["jws"] = jwsCompact
            };
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var len = 0;
            foreach (var p in parts) len += p.Length;
            var buf = new byte[len];
            var o = 0;
            foreach (var p in parts)
            {
                Buffer.BlockCopy(p, 0, buf, o, p.Length);
                o += p.Length;
            }
            return buf;
        }

        
        
        /// <summary>
        /// Creates a JWS compact string using Ed25519 (alg=EdDSA) over the given payload.
        /// 'd_b64url' is the 32-byte private seed in base64url (from JWK.d).
        /// </summary>
        private static string CreateJwsCompact(byte[] payloadUtf8, string d_b64url, string? kid = null)
        {
            // Protected header (minimal). Keep it stable; no pretty-print.
            byte[] headerUtf8 = kid == null
                ? JsonSerializer.SerializeToUtf8Bytes(new { alg = "EdDSA" })
                : JsonSerializer.SerializeToUtf8Bytes(new { alg = "EdDSA", kid });

            string headerB64 = Base64UrlEncode(headerUtf8);
            string payloadB64 = Base64UrlEncode(payloadUtf8);

            // Signing input = ASCII("BASE64URL(header).BASE64URL(payload)")
            var signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");

            // Ed25519 sign using NSec with raw private seed (32 bytes)
            using var key = Key.Import(SignatureAlgorithm.Ed25519, Base64UrlDecode(d_b64url), KeyBlobFormat.RawPrivateKey);
            byte[] sig = SignatureAlgorithm.Ed25519.Sign(key, signingInput);
            string sigB64 = Base64UrlEncode(sig);

            return $"{headerB64}.{payloadB64}.{sigB64}";
        }

        /// <summary>
        /// Serializes the same VC instance without the 'proof' property, then restores it.
        /// This preserves JsonPropertyName (e.g., "@context") and your exact model shape.
        /// </summary>
        private static byte[] SerializeVcWithoutProof(VerifiableCredential vc)
        {
            var savedProof = vc.Proof;
            vc.Proof = null;

            var json = JsonSerializer.SerializeToUtf8Bytes(
                vc,
                new JsonSerializerOptions
                {
                    WriteIndented = false,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

            vc.Proof = savedProof;
            return json;
        }

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace("-", "+").Replace("_", "/");
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Convert.FromBase64String(s);
        }

        private static string Base64UrlEncode(ReadOnlySpan<byte> data)
        {
            string s = Convert.ToBase64String(data)
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            return s;
        }

        private OkpJwk LoadOkpJwk()
        {
            var json = File.ReadAllText(_keyPath, Encoding.UTF8);
            var jwk = JsonSerializer.Deserialize<OkpJwk>(json)
                      ?? throw new InvalidOperationException("Invalid issuer-key.json");

            // Optional: hard assert kid equals verificationMethodId if present
            if (!string.IsNullOrWhiteSpace(jwk.Kid) && jwk.Kid != _verificationMethodId)
                throw new InvalidOperationException($"issuer-key.json kid mismatch. Expected {_verificationMethodId}.");

            return jwk;
        }
    }
    internal sealed class OkpJwk
    {
        [JsonPropertyName("kty")] public string Kty { get; set; } = "OKP";
        [JsonPropertyName("crv")] public string Crv { get; set; } = "Ed25519";
        [JsonPropertyName("x")] public string X { get; set; } = string.Empty; // base64url(32)
        [JsonPropertyName("d")] public string D { get; set; } = string.Empty; // base64url(32)
        [JsonPropertyName("kid")] public string? Kid { get; set; }              // optional
    }
}
