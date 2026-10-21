using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

        /// <summary>
        /// Logger used during registration (startup) and inside key-resolution callbacks.
        /// Set through <see cref="RegisterAuthenticationProviders"/>; falls back to a no-op
        /// logger so the helpers can be called safely before logging is configured.
        /// </summary>
        internal static ILogger Logger { get; private set; } = NullLogger.Instance;

        public static void RegisterAuthenticationProviders(WebApplicationBuilder builder, AppSetting settings)
        {
            if (settings?.AuthenticationProviders == null || settings.AuthenticationProviders.Count == 0)
                return;

            // Build a temporary factory so certificate/JWKS problems are logged instead of
            // being lost on the console during startup.
            Logger = builder.Services.BuildServiceProvider()
                .GetService<ILoggerFactory>()?
                .CreateLogger("Auth.Providers") ?? NullLogger.Instance;

            var defaultProvider = settings.AuthenticationProviders[0];
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = defaultProvider.Key;
                options.DefaultChallengeScheme = defaultProvider.Key;
            });

            Logger.LogInformation(
                "Registering {Count} authentication provider(s); default scheme '{DefaultScheme}'",
                settings.AuthenticationProviders.Count, defaultProvider.Key);

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
                            if (string.IsNullOrWhiteSpace(p.SecretKey))
                            {
                                throw new InvalidOperationException(
                                    $"Authentication provider '{p.Key}' is configured for symmetric JWT validation but no SecretKey was provided. " +
                                    "Set it via appsettings (not committed) or the environment variable " +
                                    $"'AppSetting__AuthenticationProviders__{settings.AuthenticationProviders.IndexOf(p)}__SecretKey'.");
                            }

                            var signingKey = Encoding.UTF8.GetBytes(p.SecretKey);
                            options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(signingKey);
                        }

                        options.RequireHttpsMetadata = true;
                    });

                    Logger.LogInformation("Registered JWT provider '{Key}' (symmetric/certificate/JWKS)", p.Key);
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

                    Logger.LogInformation("Registered OIDC/OAuth2 provider '{Key}' (authority {Authority})", p.Key, p.Authority);
                }
                else
                {
                    Logger.LogWarning("Skipping authentication provider '{Key}': unsupported type '{Type}'", p.Key, p.Type);
                }
            }

            // add authorization after registering schemes
            builder.Services.AddAuthorization();
        }

        private static IEnumerable<SecurityKey> GetCachedSigningKeys(string jwksUri)
        {
            if (JwksCache.TryGetValue(jwksUri, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                return cached.Keys;

            Logger.LogDebug("Fetching signing keys from JWKS endpoint {JwksUri}", jwksUri);

            var fetched = GetSigningKeysFromJwks(jwksUri).GetAwaiter().GetResult();
            JwksCache[jwksUri] = (fetched, DateTime.UtcNow.Add(JwksCacheDuration));
            return fetched;
        }

        private static X509Certificate2? LoadCertificateFromFile(string path, string? password)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Logger.LogError("Certificate file not found: {Path}. JWT validation will fail closed.", path);
                    return null;
                }

                var raw = File.ReadAllBytes(path);
                var flags = X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet;
                var cert = string.IsNullOrEmpty(password)
                    ? new X509Certificate2(raw, (string?)null, flags)
                    : new X509Certificate2(raw, password, flags);

                Logger.LogInformation(
                    "Loaded signing certificate {Subject} (thumbprint {Thumbprint}) from file {Path}",
                    cert.Subject, cert.Thumbprint, path);
                return cert;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load certificate from file {Path}", path);
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
                if (certs.Count == 0)
                {
                    Logger.LogError("Certificate with thumbprint {Thumbprint} not found in LocalMachine/My.", thumbprint);
                    return null;
                }

                Logger.LogInformation(
                    "Loaded signing certificate {Subject} (thumbprint {Thumbprint}) from LocalMachine/My",
                    certs[0].Subject, certs[0].Thumbprint);
                return certs[0];
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load certificate from store {Thumbprint}", thumbprint);
                return null;
            }
        }

        private static async Task<IEnumerable<SecurityKey>> GetSigningKeysFromJwks(string jwksUri)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var json = await http.GetStringAsync(jwksUri).ConfigureAwait(false);
                var jwks = new JsonWebKeySet(json);
                var keys = jwks.GetSigningKeys();

                if (keys.Count == 0)
                {
                    Logger.LogWarning("JWKS endpoint {JwksUri} returned no usable signing keys", jwksUri);
                }
                else
                {
                    Logger.LogInformation("Retrieved {Count} signing key(s) from JWKS endpoint {JwksUri}", keys.Count, jwksUri);
                }

                return keys;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to fetch JWKS from {JwksUri}", jwksUri);
                return Array.Empty<SecurityKey>();
            }
        }
    }
}
