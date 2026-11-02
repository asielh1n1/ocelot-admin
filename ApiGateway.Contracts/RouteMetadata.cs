using System.Globalization;

namespace ApiGateway.Contracts;

/// <summary>
/// Metadata of the Ocelot route that is currently being executed.
/// </summary>
/// <remarks>
/// This is the runtime, per-request counterpart of <see cref="PluginMetadata"/>:
/// <list type="bullet">
///   <item><see cref="PluginMetadata"/> is read once at startup from
///         <c>PluginMetadata:&lt;plugin name&gt;</c> and holds the plugin's own settings
///         (connection strings, credentials, feature switches).</item>
///   <item><see cref="RouteMetadata"/> is read on every request and holds the
///         <c>Metadata</c> dictionary of the matched route, already merged with
///         <c>GlobalConfiguration:Metadata</c> by Ocelot (route values win).</item>
/// </list>
/// <para>
/// Obtain the current instance from <see cref="RouteMetadataAccessor.Current"/> inside
/// the <c>SendAsync</c> of a plugin. It is never written to HTTP headers, so it cannot
/// leak to the downstream service.
/// </para>
/// <para>
/// Example <c>ocelot.json</c>:
/// <code>
/// {
///   "GlobalConfiguration": {
///     "Metadata": { "region": "eu", "cache.enabled": "true" }
///   },
///   "Routes": [
///     {
///       "UpstreamPathTemplate": "/api/orders",
///       "Metadata": { "region": "us", "cache.ttl": "30" }
///     }
///   ]
/// }
/// </code>
/// For <c>/api/orders</c> the plugin sees <c>region = "us"</c> (route wins),
/// <c>cache.enabled = "true"</c> and <c>cache.ttl = "30"</c>.
/// </para>
/// </remarks>
public sealed class RouteMetadata
{
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Creates a new instance. The values are copied, so the caller may keep mutating
    /// its own dictionary: Ocelot caches a single <c>DownstreamRoute</c> (and therefore a
    /// single metadata dictionary) per route, shared by every request.
    /// </summary>
    /// <param name="upstreamPath">Upstream path of the request, for logging only.</param>
    /// <param name="values">Route metadata already merged with the global metadata.</param>
    public RouteMetadata(string? upstreamPath, IEnumerable<KeyValuePair<string, string>>? values)
    {
        UpstreamPath = upstreamPath ?? string.Empty;
        _values = values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Upstream path of the request that produced this metadata.</summary>
    public string UpstreamPath { get; }

    /// <summary>Read-only view of the merged metadata.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Number of keys available. Zero means the route has no metadata.</summary>
    public int Count => _values.Count;

    /// <summary>Returns the raw value for <paramref name="key"/>, or <see langword="null"/>.</summary>
    public string? Get(string key) =>
        _values.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Returns <see langword="true"/> when the key exists and has a non-empty value.
    /// </summary>
    public bool Has(string key) => !string.IsNullOrWhiteSpace(Get(key));

    /// <summary>
    /// Returns the value for <paramref name="key"/>, or <paramref name="fallback"/> when
    /// it is missing or empty.
    /// </summary>
    public string GetOrDefault(string key, string fallback) =>
        Has(key) ? _values[key] : fallback;

    /// <summary>
    /// Parses a boolean value. Accepts the usual forms (<c>true</c>/<c>false</c>,
    /// case-insensitive). Returns <paramref name="fallback"/> when absent or invalid.
    /// </summary>
    public bool GetBool(string key, bool fallback = false) =>
        bool.TryParse(Get(key), out var value) ? value : fallback;

    /// <summary>
    /// Parses an integer value using the invariant culture.
    /// Returns <paramref name="fallback"/> when absent or invalid.
    /// </summary>
    public int GetInt(string key, int fallback = 0) =>
        int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
}

/// <summary>
/// Ambient access to the metadata of the route being executed.
/// </summary>
/// <remarks>
/// The host publishes the metadata before any delegating handler runs and clears it when
/// the request finishes, so a plugin only has to read <see cref="Current"/>:
/// <code>
/// protected override Task&lt;HttpResponseMessage&gt; SendAsync(
///     HttpRequestMessage request, CancellationToken cancellationToken)
/// {
///     var metadata = RouteMetadataAccessor.Current;
///     if (metadata?.GetBool("cache.enabled") == true)
///     {
///         // ... use metadata.GetInt("cache.ttl", 60)
///     }
///
///     return base.SendAsync(request, cancellationToken);
/// }
/// </code>
/// <para>
/// The value flows with the request's async context, so it is correct even though Ocelot
/// pools and reuses handler instances: no per-instance state is involved, no constructor
/// injection is required, and the plugin keeps its public parameterless constructor.
/// </para>
/// <para>
/// Do not capture <see cref="Current"/> in background work that outlives the request
/// (fire-and-forget tasks, timers, static caches): copy the values you need first.
/// </para>
/// </remarks>
public static class RouteMetadataAccessor
{
    private static readonly AsyncLocal<RouteMetadata?> _current = new();

    /// <summary>
    /// Metadata of the route being executed, or <see langword="null"/> when called
    /// outside a gateway request.
    /// </summary>
    public static RouteMetadata? Current => _current.Value;

    /// <summary>
    /// Publishes <paramref name="metadata"/> for the current async flow. Dispose the
    /// returned scope to restore the previous value. Intended for the host, not for plugins.
    /// </summary>
    /// <param name="metadata">Metadata to expose to the plugins of this request.</param>
    /// <returns>A scope that restores the previous value when disposed.</returns>
    public static IDisposable Push(RouteMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var previous = _current.Value;
        _current.Value = metadata;
        return new Scope(previous);
    }

    private sealed class Scope(RouteMetadata? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
