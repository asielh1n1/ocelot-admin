namespace ApiGateway.Contracts;

/// <summary>
/// Read-only configuration handed to a plugin during initialization.
/// </summary>
/// <remarks>
/// Deliberately minimal: it exposes no service provider, no host database context and
/// no host services, so a plugin stays fully independent from the gateway internals.
/// <para>
/// Values are read from the <c>PluginMetadata:&lt;plugin name&gt;</c> configuration
/// section. Other plugins' sections and the rest of the host configuration are not
/// accessible from here.
/// </para>
/// <para>
/// Example <c>appsettings.json</c>:
/// <code>
/// {
///   "PluginMetadata": {
///     "PostgreSQL Cache": {
///       "ConnectionString": "",
///       "TableName": "gateway_cache",
///       "AutoMigrate": "true"
///     }
///   }
/// }
/// </code>
/// In containers or CI, inject through environment variables:
/// <code>
/// PluginMetadata__PostgreSQL_Cache__ConnectionString=Host=pg;Database=cache;...
/// </code>
/// </para>
/// </remarks>
public sealed class PluginMetadata
{
    /// <summary>
    /// Creates a new metadata bag for a plugin.
    /// </summary>
    /// <param name="pluginName">Name of the plugin these values belong to.</param>
    /// <param name="values">Configuration values for this plugin only.</param>
    public PluginMetadata(string pluginName, IReadOnlyDictionary<string, string?> values)
    {
        PluginName = pluginName;
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    /// <summary>Name of the plugin these values belong to.</summary>
    public string PluginName { get; }

    /// <summary>
    /// Configuration values for this plugin, keyed case-insensitively.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Values { get; }

    /// <summary>
    /// Returns the raw value for <paramref name="key"/>, or <see langword="null"/>
    /// when the key is not present.
    /// </summary>
    public string? Get(string key) =>
        Values.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Returns the value for <paramref name="key"/>, or <see langword="null"/> when it is
    /// missing, empty, or whitespace. Useful for values that must be supplied.
    /// </summary>
    public string? GetRequired(string key)
    {
        var value = Get(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Returns the value for <paramref name="key"/>, or <paramref name="fallback"/>
    /// when it is missing or empty.
    /// </summary>
    public string GetOrDefault(string key, string fallback) =>
        string.IsNullOrWhiteSpace(Get(key)) ? fallback : Get(key)!;

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
        int.TryParse(Get(key), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;

    /// <summary>
    /// Returns <see langword="true"/> when the key exists and has a non-empty value.
    /// </summary>
    public bool Has(string key) => !string.IsNullOrWhiteSpace(Get(key));

    /// <summary>Returns the keys configured for this plugin.</summary>
    public IEnumerable<string> Keys => Values.Keys;
}
