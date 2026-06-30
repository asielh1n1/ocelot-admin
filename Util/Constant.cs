namespace ApiGateway.Util
{
    public class Constant
    {
        public static string AdministratorPolicy = "administrator";
        public static string CorsName = "Api-Gateway-Cors";
        public static class AuthenticationProviderValues
        {
            public static string Key = "Key";
            public static string Type = "Key";
            public static string IdentityUrl = "Url";
            public static string IdentityClientId = "ClientId";
            public static string IdentityClientSecret = "ClientSecret";
            public static string EnableCaching = "EnableCaching";
            public static string CacheDuration = "CacheDuration";
        }
        public static class AuthenticationProviderType
        {
            public static string IdentityServer = "identity_server";
            public static string JWT = "jwt";
        }
    }
}
