using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ApiGateway.Util
{
    public class AppSetting
    {
        public const string ConfigurationKey = "AppSetting";
        // Support multiple authentication providers
        public List<AuthenticationProviderSetting> AuthenticationProviders { get; set; }

        [Required]
        public AdminApiSetting AdminApi { get; set; }

        /// <summary>
        /// Allowed CORS origins. If empty or null, all origins are allowed.
        /// For production, specify your domains e.g. ["https://yourdomain.com"]
        /// </summary>
        public List<string> AllowedCorsOrigins { get; set; } = [];
    }

    public class AppSettingsValidator
    {
        public static void Validate(AppSetting options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options), "AppSetting cannot be null");
            }

            if (options.AdminApi == null)
            {
                throw new InvalidOperationException("AdminApi configuration is required but was not found in appsettings.json");
            }

            // When explicitly configured, the admin signing key must be long enough to
            // derive a strong HMAC-SHA256 key. A weak secret allows token forgery.
            var signingKey = options.AdminApi.SigningKey;
            if (!string.IsNullOrWhiteSpace(signingKey) && signingKey.Length < AdminApiSetting.MinSigningKeyLength)
            {
                throw new InvalidOperationException(
                    $"AppSetting:AdminApi:SigningKey must be at least {AdminApiSetting.MinSigningKeyLength} characters long, " +
                    "or left empty to use a random per-process key.");
            }

            try
            {
                if (options.AuthenticationProviders != null && options.AuthenticationProviders.Count > 0)
                {
                    foreach (var p in options.AuthenticationProviders)
                    {
                        if (string.IsNullOrEmpty(p.Key))
                            throw new Exception($"In the configuration of the authentication providers the \"{Constant.AuthenticationProviderValues.Key}\" is required.");
                        if (string.IsNullOrEmpty(p.Type))
                            throw new Exception($"In the configuration of the authentication providers the \"{Constant.AuthenticationProviderValues.Type}\" is required.");
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"An error occurred while validating the appsetting settings. {e.Message}");
            }

        }
    }

    public class AuthenticationProviderSetting
    {
        public string Key { get; set; }
        public string Type { get; set; }

        // JWT fields
        public string SecretKey { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }

        // Certificate-based / JWKS fields (for asymmetric signing)
        // Path to a .pfx/.pem/.cer file containing the signing certificate (private key required to issue tokens)
        public string CertificatePath { get; set; }
        // Password for the certificate file if needed
        public string CertificatePassword { get; set; }
        // Thumbprint to load certificate from the LocalMachine My store
        public string CertificateThumbprint { get; set; }
        // JWKS endpoint URI to fetch public keys (for tokens issued by external providers)
        public string JwksUri { get; set; }

        // OIDC/IdentityServer fields
        public string Authority { get; set; }
        public string ClientId { get; set; }
        public bool ValidateLifetime { get; set; }
    }

    public class AdminApiSetting
    {
        /// <summary>
        /// Minimum accepted length for <see cref="SigningKey"/>.
        /// </summary>
        public const int MinSigningKeyLength = 32;

        public bool Enable { get; set; }

        /// <summary>
        /// Optional shared secret used to sign and validate admin panel JWTs.
        /// 
        /// Leave empty for a single-instance deployment: a cryptographically random
        /// 512-bit key is generated at startup and never persisted to disk (sessions
        /// are invalidated on restart).
        /// 
        /// Set this when running more than one instance behind a load balancer, so all
        /// instances share the same signing key. Prefer injecting it through an
        /// environment variable (e.g. AppSetting__AdminApi__SigningKey) instead of
        /// committing it to appsettings.json.
        /// </summary>
        public string SigningKey { get; set; }

        // UI customization
        public string AppName { get; set; } = "API Gateway";
        public string PrimaryColor { get; set; } = "#03a9f4";
        public string LogoUrl { get; set; } = "";
    }
}
