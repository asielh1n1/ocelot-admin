using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ApiGateway.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ocelot.DependencyInjection;

namespace ApiGateway.Util;

public interface IPluginService
{
    /// <summary>Plugins discovered at startup, in load order.</summary>
    IReadOnlyList<PluginInfo> GetPluginNames();

    /// <summary>Discovers plugin assemblies and registers them as Ocelot delegating handlers.</summary>
    void LoadPlugins(IOcelotBuilder ocelotBuilder);

    /// <summary>
    /// Runs <see cref="IPlugin.InitializeAsync"/> for every plugin. Must be called after
    /// the host is built. Plugins that need no setup inherit the default no-op.
    /// </summary>
    Task InitializePluginsAsync(IConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>Runs <see cref="IPlugin.ShutdownAsync"/> in reverse order.</summary>
    Task ShutdownPluginsAsync(CancellationToken cancellationToken = default);
}

public class PluginService : IPluginService
{
    /// <summary>
    /// Configuration section that holds per-plugin settings.
    /// Each plugin reads only its own subsection.
    /// </summary>
    public const string MetadataSectionName = "PluginMetadata";

    private static readonly string[] HostAssemblyNames = ["ApiGateway", "ApiGateway.Contracts"];

    private readonly List<PluginInfo> _plugins = new();
    private readonly List<(PluginInfo Info, Type Type)> _registered = new();
    private readonly List<(PluginInfo Info, IPlugin Instance)> _initialized = new();
    private readonly string _pluginsPath;
    private readonly ILogger<PluginService> _logger;

    public PluginService(string pluginsPath, ILogger<PluginService>? logger = null)
    {
        _pluginsPath = pluginsPath;
        _logger = logger ?? NullLogger<PluginService>.Instance;
    }

    public IReadOnlyList<PluginInfo> GetPluginNames() => _plugins;

    public void LoadPlugins(IOcelotBuilder ocelotBuilder)
    {
        if (!Directory.Exists(_pluginsPath))
        {
            _logger.LogDebug("Plugins directory {PluginsPath} does not exist; no plugins loaded", _pluginsPath);
            return;
        }

        foreach (var assembly in LoadPluginAssemblies(_pluginsPath))
        {
            foreach (var type in assembly.GetTypes())
            {
                // Legacy duck-typing support: a DelegatingHandler exposing Name/IsGlobal
                // properties but not implementing IPlugin is almost certainly a plugin
                // built against an older contract. Warn loudly instead of ignoring it.
                if (typeof(DelegatingHandler).IsAssignableFrom(type)
                    && !type.IsAbstract
                    && !type.IsInterface
                    && !typeof(IPlugin).IsAssignableFrom(type)
                    && HasLegacyPluginShape(type))
                {
                    _logger.LogWarning(
                        "Plugin {TypeName} in {Assembly} exposes Name/IsGlobal but does not implement IPlugin. " +
                        "It was skipped. Recompile it against ApiGateway.Contracts.",
                        type.FullName, assembly.GetName().Name);
                    continue;
                }

                if (!IsPluginType(type))
                    continue;

                RegisterPlugin(type, ocelotBuilder);
            }
        }

        _logger.LogInformation("Loaded {Count} plugin(s) from {PluginsPath}", _plugins.Count, _pluginsPath);
    }

    public async Task InitializePluginsAsync(IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        foreach (var (info, type) in _registered)
        {
            // Parameterless construction: the plugin gets no access to the host container.
            // This is a dedicated startup instance; Ocelot creates its own handler
            // instances later to serve requests.
            IPlugin plugin;
            try
            {
                plugin = (IPlugin)Activator.CreateInstance(type)!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Plugin {PluginName} could not be instantiated for initialization. " +
                    "Plugins must have a public parameterless constructor.", info.Name);
                throw;
            }

            var metadata = new PluginMetadata(info.Name, ReadMetadata(configuration, info));

            var stopwatch = Stopwatch.StartNew();
            try
            {
                _logger.LogDebug("Initializing plugin {PluginName} ({ClassName})...", info.Name, info.ClassName);
                await plugin.InitializeAsync(metadata, cancellationToken).ConfigureAwait(false);
                _initialized.Add((info, plugin));
                _logger.LogInformation("Plugin {PluginName} ready in {ElapsedMs} ms",
                    info.Name, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // Never log the metadata dictionary itself: it may contain secrets.
                _logger.LogError(ex, "Plugin {PluginName} failed to initialize", info.Name);
                throw;
            }
        }
    }

    public async Task ShutdownPluginsAsync(CancellationToken cancellationToken = default)
    {
        // Reverse order: last initialized, first shut down.
        for (var i = _initialized.Count - 1; i >= 0; i--)
        {
            var (info, plugin) = _initialized[i];
            try
            {
                _logger.LogDebug("Shutting down plugin {PluginName}...", info.Name);
                await plugin.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin {PluginName} failed to shut down cleanly", info.Name);
            }
        }
    }

    private void RegisterPlugin(Type type, IOcelotBuilder ocelotBuilder)
    {
        // Ocelot instantiates the handler itself; we only need its metadata here.
        IPlugin? probe;
        try
        {
            probe = (IPlugin?)Activator.CreateInstance(type);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Plugin {TypeName} could not be inspected and was skipped. " +
                "Plugins must have a public parameterless constructor.", type.FullName);
            return;
        }

        if (probe == null)
        {
            _logger.LogWarning("Plugin {TypeName} could not be instantiated and was skipped", type.FullName);
            return;
        }

        var name = string.IsNullOrWhiteSpace(probe.Name) ? type.Name : probe.Name;

        ocelotBuilder.AddDelegatingHandler(type, probe.IsGlobal);

        var info = new PluginInfo
        {
            Name = name,
            ClassName = type.Name,
            Version = type.Assembly.GetName().Version?.ToString() ?? string.Empty
        };

        _plugins.Add(info);
        _registered.Add((info, type));

        _logger.LogInformation(
            "Loaded plugin {PluginName} ({ClassName}) v{Version}, global: {IsGlobal}",
            name, type.Name, info.Version, probe.IsGlobal);
    }

    private IReadOnlyDictionary<string, string?> ReadMetadata(IConfiguration configuration, PluginInfo info)
    {
        // Prefer the friendly name; fall back to the class name.
        var section = configuration.GetSection($"{MetadataSectionName}:{info.Name}");
        if (!section.Exists())
            section = configuration.GetSection($"{MetadataSectionName}:{info.ClassName}");

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
                values[child.Key] = child.Value;
        }
        else
        {
            _logger.LogDebug(
                "No {Section}:{PluginName} configuration section found; the plugin will receive empty metadata",
                MetadataSectionName, info.Name);
        }

        return values;
    }

    private static bool IsPluginType(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            return false;

        if (!typeof(DelegatingHandler).IsAssignableFrom(type))
            return false;

        return typeof(IPlugin).IsAssignableFrom(type);
    }

    /// <summary>
    /// Detects the pre-contract plugin shape (public Name/IsGlobal properties) so the host
    /// can emit an actionable warning instead of silently ignoring the plugin.
    /// </summary>
    private static bool HasLegacyPluginShape(Type type)
    {
        var nameProp = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
        var globalProp = type.GetProperty("IsGlobal", BindingFlags.Public | BindingFlags.Instance);

        return nameProp?.PropertyType == typeof(string)
            && globalProp?.PropertyType == typeof(bool);
    }

    private IEnumerable<Assembly> LoadPluginAssemblies(string path)
    {
        foreach (var dll in Directory.GetFiles(path, "*.dll"))
        {
            var fileName = Path.GetFileNameWithoutExtension(dll);

            // A plugin must never ship its own copy of the host contract: that produces two
            // distinct types with the same name and breaks every type check.
            if (HostAssemblyNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Skipping {AssemblyFile}: plugins must not include the host contract. " +
                    "Set Private=false and ExcludeAssets=runtime on the reference to {FileName}.",
                    Path.GetFileName(dll), fileName);
                continue;
            }

            Assembly? assembly = null;
            try
            {
                assembly = Assembly.LoadFrom(dll);
            }
            catch (BadImageFormatException)
            {
                // Not a managed assembly (e.g. a native dependency) - skip silently.
                continue;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Skipping plugin assembly {AssemblyFile}: could not be loaded", Path.GetFileName(dll));
                continue;
            }

            if (assembly != null)
                yield return assembly;
        }
    }
}
