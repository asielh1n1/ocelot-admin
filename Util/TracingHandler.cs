using Azure.Core;
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
            var logger = context.RequestServices.GetService<ILogger<TracingHandler>>();
            string key = "TraceEndpointUrl";
            var downstreamRoute = context.Items.DownstreamRoute();
            if (downstreamRoute.HttpHandlerOptions == null || !downstreamRoute.HttpHandlerOptions.UseTracing || !downstreamRoute.MetadataOptions.Metadata.ContainsKey(key))
            {
                return;
            }

            downstreamRoute.MetadataOptions.Metadata.TryGetValue(key, out string? url);
            if (url.IsNotEmpty()
                && Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                // SSRF protection: block requests to private/internal IP ranges
                if (IsPrivateOrReservedHost(uriResult))
                {
                    logger?.LogWarning("Tracing skipped: target host {Host} is private or reserved", uriResult.Host);
                    return;
                }

                string? bodyString = string.Empty;
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                try
                {
                    bodyString = await reader.ReadToEndAsync();
                    if (context.Request.Body.CanSeek)
                        context.Request.Body.Position = 0;
                }
                catch (Exception ex)
                {
                    bodyString = null;
                    logger?.LogWarning(ex, "Could not read request body for tracing");
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
                        logger?.LogError(e, "Error sending trace to {TraceEndpointUrl}", url);
                    }
                }
            }
        }

        /// <summary>
        /// Immutable snapshot of everything the tracer needs from the request, captured
        /// while the HttpContext is still valid so it can be safely processed on a
        /// background task (never hold a reference to HttpContext across awaits/Task.Run).
        /// </summary>
        public sealed record TraceRequest(string Url, string Method, string Query, IDictionary<string, string> Header, string Body, string TraceEndpointUrl);

        public static TraceRequest? CaptureRequest(HttpContext context)
        {
            try
            {
                const string key = "TraceEndpointUrl";
                var downstreamRoute = context.Items.DownstreamRoute();
                if (downstreamRoute?.HttpHandlerOptions == null || !downstreamRoute.HttpHandlerOptions.UseTracing)
                    return null;

                if (downstreamRoute.MetadataOptions?.Metadata == null ||
                    !downstreamRoute.MetadataOptions.Metadata.TryGetValue(key, out string? url) ||
                    string.IsNullOrWhiteSpace(url))
                    return null;

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                    return null;

                if (IsPrivateOrReservedHost(uriResult))
                    return null;

                var safeHeaders = context.Request.Headers
                    .Where(h => !SensitiveHeaders.Contains(h.Key))
                    .ToDictionary(h => h.Key, h => h.Value.ToString());

                return new TraceRequest(
                    context.Request.GetDisplayUrl(),
                    context.Request.Method,
                    context.Request.QueryString.Value ?? string.Empty,
                    safeHeaders,
                    string.Empty, // body is not read here to avoid touching the request stream off-thread
                    url);
            }
            catch
            {
                return null;
            }
        }

        public static async Task SendAsync(TraceRequest trace, ILogger? logger = null)
        {
            try
            {
                if (!Uri.TryCreate(trace.TraceEndpointUrl, UriKind.Absolute, out var uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps) ||
                    IsPrivateOrReservedHost(uriResult))
                {
                    logger?.LogWarning("Tracing skipped: {TraceEndpointUrl} is invalid or points to a private/reserved host", trace.TraceEndpointUrl);
                    return;
                }

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var content = new StringContent(JsonSerializer.Serialize(new
                {
                    Url = trace.Url,
                    Method = trace.Method,
                    Query = trace.Query,
                    Header = trace.Header,
                    Body = trace.Body
                }), Encoding.UTF8, "application/json");

                await httpClient.PostAsync(trace.TraceEndpointUrl, content);
            }
            catch (Exception e)
            {
                logger?.LogError(e, "Error sending trace to {TraceEndpointUrl}", trace.TraceEndpointUrl);
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
