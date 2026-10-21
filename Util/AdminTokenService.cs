using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Util
{
    /// <summary>
    /// Manages admin panel JWT authentication.
    ///
    /// Signing key resolution:
    ///   - <c>AppSetting:AdminApi:SigningKey</c> when configured (required for multi-instance
    ///     deployments so every instance signs with the same key).
    ///   - Otherwise a cryptographically random 512-bit key generated at startup. It never
    ///     touches disk, but it is per-process: sessions are invalidated on restart and
    ///     tokens are NOT accepted across instances behind a load balancer.
    /// </summary>
    public sealed class AdminTokenService
    {
        private const string Issuer = "qvasoft-gateway-admin";
        private const string Audience = "qvasoft-gateway";
        private const int ExpirationSeconds = 3600;
        private const string RequiredRole = "Administrator";

        private readonly SymmetricSecurityKey _signingKey;

        /// <summary>
        /// True when <c>AppSetting:AdminApi:SigningKey</c> is in use (shared across instances).
        /// False when the random per-process fallback was used.
        /// </summary>
        public bool UsesConfiguredKey { get; }

        public AdminTokenService(IOptions<AppSetting> options)
        {
            var configured = options?.Value?.AdminApi?.SigningKey;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                // Derive a fixed-size key from the configured secret (>= 32 bytes required by HMAC-SHA256).
                var derived = SHA512.HashData(System.Text.Encoding.UTF8.GetBytes(configured));
                _signingKey = new SymmetricSecurityKey(derived);
                UsesConfiguredKey = true;
            }
            else
            {
                // Single-instance fallback: cryptographically random 512-bit key, never persisted.
                var keyBytes = RandomNumberGenerator.GetBytes(64);
                _signingKey = new SymmetricSecurityKey(keyBytes);
                UsesConfiguredKey = false;
            }
        }

        public TokenValidationParameters ValidationParameters => new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = _signingKey,
            RoleClaimType = "role"
        };

        public string GenerateToken(string username, IList<string> roles)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, username) };
            if (roles != null)
            {
                foreach (var role in roles)
                    claims.Add(new Claim("role", role));
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddSeconds(ExpirationSeconds),
                Issuer = Issuer,
                Audience = Audience,
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature)
            };

            var handler = new JwtSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        public static bool HasRequiredClaims(ClaimsPrincipal user)
        {
            return user.Claims.Any(c => c.Type == "role" && c.Value == RequiredRole);
        }
    }
}
