using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Util
{
    /// <summary>
    /// Manages admin panel JWT authentication entirely in memory.
    /// A cryptographically random 512-bit key is generated at startup and never
    /// persisted to disk. All admin sessions are automatically invalidated on restart.
    /// </summary>
    public sealed class AdminTokenService
    {
        private const string Issuer = "qvasoft-gateway-admin";
        private const string Audience = "qvasoft-gateway";
        private const int ExpirationSeconds = 3600;
        private const string RequiredRole = "Administrator";

        private readonly SymmetricSecurityKey _signingKey;

        public AdminTokenService()
        {
            var keyBytes = RandomNumberGenerator.GetBytes(64);
            _signingKey = new SymmetricSecurityKey(keyBytes);
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
