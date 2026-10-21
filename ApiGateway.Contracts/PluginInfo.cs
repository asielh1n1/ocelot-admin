namespace ApiGateway.Contracts;

/// <summary>
/// Descriptive information about a loaded plugin, exposed to the admin panel.
/// </summary>
public sealed class PluginInfo
{
    /// <summary>Friendly name declared by the plugin (<see cref="IPlugin.Name"/>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Class name of the plugin type, used as a fallback configuration key.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Assembly version of the plugin, when available.</summary>
    public string Version { get; set; } = string.Empty;
}
