using System.Security.Cryptography;
using System.Text;

namespace ApiGateway.Util
{
    /// <summary>
    /// Generates HMAC-SHA256 signatures for outbound server-to-server requests.
    /// The API key identifies the caller; the signature proves possession of the secret
    /// without transmitting it. The timestamp prevents replay attacks.
    /// 
    /// Headers sent:
    ///   X-Gateway-Key:       static API key (identifies the gateway)
    ///   X-Gateway-Timestamp: Unix seconds (UTC)
    ///   X-Gateway-Signature: HMAC-SHA256(timestamp, secretKey) in hex
    /// 
    /// Validation on the server side:
    ///   1. Verify X-Gateway-Key matches the expected value
    ///   2. Verify timestamp is within ±5 minutes of current UTC time
    ///   3. Recompute HMAC-SHA256(timestamp, secretKey) and compare with X-Gateway-Signature
    /// </summary>
    public static class GatewaySignature
    {
        private const string ApiKey = "qvasoft-gw-community-2025";
        private const string SecretKey = "k8Tz!mR3@vLpX9sW#dQj7nYbC2gF6hUa";

        public const string HeaderKey = "X-Gateway-Key";
        public const string HeaderTimestamp = "X-Gateway-Timestamp";
        public const string HeaderSignature = "X-Gateway-Signature";

        /// <summary>
        /// Applies the gateway signature headers to an HttpClient's default headers.
        /// </summary>
        public static void Sign(HttpClient client)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = ComputeHmac(timestamp);

            client.DefaultRequestHeaders.Remove(HeaderKey);
            client.DefaultRequestHeaders.Remove(HeaderTimestamp);
            client.DefaultRequestHeaders.Remove(HeaderSignature);

            client.DefaultRequestHeaders.Add(HeaderKey, ApiKey);
            client.DefaultRequestHeaders.Add(HeaderTimestamp, timestamp);
            client.DefaultRequestHeaders.Add(HeaderSignature, signature);
        }

        private static string ComputeHmac(string timestamp)
        {
            var keyBytes = Encoding.UTF8.GetBytes(SecretKey);
            var messageBytes = Encoding.UTF8.GetBytes(timestamp);

            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(messageBytes);
            return Convert.ToHexStringLower(hash);
        }
    }
}
