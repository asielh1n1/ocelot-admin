using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace ApiGateway.Util
{
    public static class AuthenticationProvidersRegistry
    {
        private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(1);

        private static readonly ConcurrentDictionary<string, (IEnumerable<SecurityKey> Keys, DateTime ExpiresAt)> JwksCache = new();

        public static void RegisterAuthenticationProviders(WebApplicationBuilder builder, AppSetting settings)
        {
            if (settings?.AuthenticationProviders == null || settings.AuthenticationProviders.Count == 0)
                return;

            var defaultProvider = settings.AuthenticationProviders[0];
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = defaultProvider.Key;
                options.DefaultChallengeScheme = defaultProvider.Key;
            });

            foreach (var p in settings.AuthenticationProviders)
            {
                var type = p.Type?.ToLowerInvariant();                
                if (type == "jwt")
                {
                    builder.Services.AddAuthentication().AddJwtBearer(p.Key, options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = !string.IsNullOrEmpty(p.Issuer),
                            ValidateAudience = !string.IsNullOrEmpty(p.Audience),
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = p.Issuer,
                            ValidAudience = p.Audience,
                            ValidateLifetime = p.ValidateLifetime,
                            RoleClaimType = "role"
                        };

                        options.MapInboundClaims = false;

                        if (!string.IsNullOrEmpty(p.CertificatePath))
                        {
                            var cert = LoadCertificateFromFile(p.CertificatePath, p.CertificatePassword);
                            if (cert != null)
                                options.TokenValidationParameters.IssuerSigningKey = new X509SecurityKey(cert);
                        }
                        else if (!string.IsNullOrEmpty(p.CertificateThumbprint))
                        {
                            var cert = LoadCertificateFromStore(p.CertificateThumbprint);
                            if (cert != null)
                                options.TokenValidationParameters.IssuerSigningKey = new X509SecurityKey(cert);
                        }
                        else if (!string.IsNullOrEmpty(p.JwksUri))
                        {
                            options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                            {
                                return GetCachedSigningKeys(p.JwksUri);
                            };
                        }
                        else
                        {
                            var signingKey = Encoding.UTF8.GetBytes(p.SecretKey ?? string.Empty);
                            options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(signingKey);
                        }

                        options.RequireHttpsMetadata = true;
                    });
                }
                else if (type == "oidc" || type == "oauth2")
                {
                    builder.Services.AddAuthentication().AddJwtBearer(p.Key, options =>
                    {
                        options.Authority = p.Authority;
                        options.Audience = p.Audience;
                        options.RequireHttpsMetadata = true;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = !string.IsNullOrEmpty(p.Issuer),
                            ValidateAudience = !string.IsNullOrEmpty(p.Audience),
                            ValidAudience = p.Audience,
                            ValidIssuer = p.Issuer,
                            ValidateLifetime = p.ValidateLifetime,
                            RoleClaimType = "role"
                        };
                        options.MapInboundClaims = false;
                    });
                }
            }

            // add authorization after registering schemes
            builder.Services.AddAuthorization();
        }

        private static IEnumerable<SecurityKey> GetCachedSigningKeys(string jwksUri)
        {
            if (JwksCache.TryGetValue(jwksUri, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                return cached.Keys;

            var fetched = GetSigningKeysFromJwks(jwksUri).GetAwaiter().GetResult();
            JwksCache[jwksUri] = (fetched, DateTime.UtcNow.Add(JwksCacheDuration));
            return fetched;
        }

        private static X509Certificate2? LoadCertificateFromFile(string path, string? password)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var raw = File.ReadAllBytes(path);
                var flags = X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet;
                return string.IsNullOrEmpty(password)
                    ? new X509Certificate2(raw, (string?)null, flags)
                    : new X509Certificate2(raw, password, flags);
            }
            catch
            {
                return null;
            }
        }

        private static X509Certificate2? LoadCertificateFromStore(string thumbprint)
        {
            try
            {
                using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
                return certs.Count > 0 ? certs[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<IEnumerable<SecurityKey>> GetSigningKeysFromJwks(string jwksUri)
        {
            try
            {
                using var http = new HttpClient();
                var json = await http.GetStringAsync(jwksUri).ConfigureAwait(false);
                var jwks = new JsonWebKeySet(json);
                return jwks.GetSigningKeys();
            }
            catch
            {
                return Array.Empty<SecurityKey>();
            }
        }
    }
}
