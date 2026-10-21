using System.Net;
using System.Text;
using ApiGateway.Contracts;

namespace SamplePlugin;

/// <summary>
/// Reference implementation of a gateway plugin.
/// </summary>
/// <remarks>
/// This sample demonstrates the full contract:
/// <list type="bullet">
///   <item>Derive from <see cref="DelegatingHandler"/> so Ocelot can insert it into the pipeline.</item>
///   <item>Implement <see cref="IPlugin"/> to declare its name and scope.</item>
///   <item>Override <see cref="IPlugin.InitializeAsync"/> only if setup is needed
///         (this sample does not need any).</item>
///   <item>Read configuration exclusively from <see cref="PluginMetadata"/>.</item>
/// </list>
/// <para>
/// Configuration (<c>appsettings.json</c>), keyed by the plugin <see cref="Name"/>:
/// <code>
/// {
///   "PluginMetadata": {
///     "Sample Header Plugin": {
///       "HeaderName": "X-Sample-Gateway",
///       "HeaderValue": "true"
///     }
///   }
/// }
/// </code>
/// In containers, inject through environment variables:
/// <code>
/// PluginMetadata__Sample_Header_Plugin__HeaderName=X-Sample-Gateway
/// </code>
/// </para>
/// </remarks>
public sealed class SampleHeaderPlugin : DelegatingHandler, IPlugin
{
    // These are mutable instance fields, set during initialization. The host creates this
    // type with a parameterless constructor and the plugin owns everything it needs.
    private string _headerName = "X-Sample-Gateway";
    private string _headerValue = "true";

    /// <inheritdoc />
    public string Name => "Sample Header Plugin";

    /// <inheritdoc />
    public bool IsGlobal => true;

    /// <summary>
    /// Optional: called once at startup. The metadata bag contains only this plugin's own
    /// configuration section; the host database and services are not reachable.
    /// </summary>
    /// <remarks>
    /// Note that this startup instance is NOT the one Ocelot uses to serve requests.
    /// Ocelot instantiates its own handler instances, so any state built here would not be
    /// shared. This sample therefore only reads simple configuration values.
    /// </remarks>
    public Task InitializeAsync(PluginMetadata metadata, CancellationToken cancellationToken)
    {
        // Missing values fall back to the defaults above rather than failing startup.
        _headerName = metadata.GetOrDefault("HeaderName", _headerName);
        _headerValue = metadata.GetOrDefault("HeaderValue", _headerValue);

        // A plugin that cannot work without a value would fail fast instead:
        // var connectionString = metadata.GetRequired("ConnectionString")
        //     ?? throw new InvalidOperationException(
        //         $"'{metadata.PluginName}' requires 'PluginMetadata:{metadata.PluginName}:ConnectionString'.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Optional: called on graceful shutdown, in reverse initialization order.
    /// </summary>
    public Task ShutdownAsync(CancellationToken cancellationToken)
    {
        // Nothing to release in this sample.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs for every request when <see cref="IsGlobal"/> is <see langword="true"/>.
    /// </summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(_headerName, _headerValue);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // Example of mutating the response instead of short-circuiting it.
        response.Headers.TryAddWithoutValidation("X-Sample-Plugin", "processed");

        return response;
    }
}

/// <summary>
/// Optional second sample: a plugin that short-circuits a request without calling the
/// downstream service. Useful for maintenance windows or internal rate limiting.
/// </summary>
/// <remarks>
/// Not global, so it only runs on routes whose <c>DelegatingHandlers</c> list includes
/// <c>ShortCircuitPlugin</c>.
/// </remarks>
public sealed class ShortCircuitPlugin : DelegatingHandler, IPlugin
{
    /// <inheritdoc />
    public string Name => "Short Circuit Plugin";

    /// <inheritdoc />
    public bool IsGlobal => false;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(
                """{"status":"error","message":"Temporarily unavailable"}""",
                Encoding.UTF8,
                "application/json"),
            RequestMessage = request
        };

        return Task.FromResult(response);
    }
}
