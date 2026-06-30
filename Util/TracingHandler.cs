using Azure.Core;
using log4net;
using Microsoft.AspNetCore.Http.Extensions;
using Ocelot.Infrastructure.Extensions;
using Ocelot.Middleware;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ApiGateway.Util
{
    public class TracingHandler
    {
        private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization", "Cookie", "Set-Cookie", "X-Api-Key", "Proxy-Authorization"
        };

        public static async void SendAsync(HttpContext context)
        {
            string key = "TraceEndpointUrl";
            var downstreamRoute = context.Items.DownstreamRoute();
            if (downstreamRoute.HttpHandlerOptions == null || !downstreamRoute.HttpHandlerOptions.UseTracing || !downstreamRoute.MetadataOptions.Metadata.ContainsKey(key))
            {
                return;
            }

            downstreamRoute.MetadataOptions.Metadata.TryGetValue(key, out string url);
            if( url.IsNotEmpty() 
                && Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                // SSRF protection: block requests to private/internal IP ranges
                if (IsPrivateOrReservedHost(uriResult))
                    return;

                string bodyString = string.Empty;
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                try
                {
                    bodyString = await reader.ReadToEndAsync();
                    if (context.Request.Body.CanSeek)
                        context.Request.Body.Position = 0;
                }
                catch (Exception)
                {
                    bodyString = null;
                }

                // Filter out sensitive headers before sending to trace endpoint
                var safeHeaders = context.Request.Headers
                    .Where(h => !SensitiveHeaders.Contains(h.Key))
                    .ToDictionary(h => h.Key, h => h.Value.ToString());

                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        var content = new StringContent(JsonSerializer.Serialize(new
                        {
                            Url = context.Request.GetDisplayUrl(),
                            Method = context.Request.Method,
                            Query = context.Request.Query,
                            Header = safeHeaders,
                            Body = bodyString
                        }), Encoding.UTF8, "application/json");
                        await httpClient.PostAsync(url, content);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(string.Format("Error TracingHandler: {0}", e.Message));
                    }
                }
            }
        }

        private static bool IsPrivateOrReservedHost(Uri uri)
        {
            try
            {
                if (uri.IsLoopback)
                    return true;

                var host = uri.Host;

                // Block well-known cloud metadata endpoints
                if (host.Equals("169.254.169.254", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (IPAddress.TryParse(host, out var ip))
                {
                    // Block private ranges: 10.x, 172.16-31.x, 192.168.x, link-local 169.254.x
                    var bytes = ip.GetAddressBytes();
                    if (bytes.Length == 4)
                    {
                        if (bytes[0] == 10) return true;
                        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                        if (bytes[0] == 192 && bytes[1] == 168) return true;
                        if (bytes[0] == 169 && bytes[1] == 254) return true;
                        if (bytes[0] == 127) return true;
                    }
                }

                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
