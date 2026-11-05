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

    /// <summary>Directory the host loads plugin assemblies from.</summary>
    string PluginsPath { get; }

    /// <summary>
    /// Inspects a plugin assembly that is not loaded yet, using the exact rules applied at
    /// startup, so the admin panel can reject an upload that would never be loaded.
    /// Returns its errors instead of throwing.
    /// </summary>
    /// <param name="assemblyPath">File to inspect.</param>
    /// <param name="configuration">Host configuration, used to look for the plugin metadata section.</param>
    /// <param name="existingNames">Plugin names already loaded; a clash is an error.</param>
    /// <param name="replacedFileNames">
    /// Files the operation is about to replace, so the names they declare are not treated as
    /// a clash with themselves.
    /// </param>
    PluginValidationResult ValidatePluginAssembly(
        string assemblyPath,
        IConfiguration configuration,
        IReadOnlyCollection<string> existingNames,
        IReadOnlyCollection<string>? replacedFileNames = null);


    /// <summary>Discovers plugin assemblies and registers them as Ocelot delegating handlers.</summary>
    void LoadPlugins(IOcelotBuilder ocelotBuilder);

    /// <summary>
    /// Runs <see cref="IPlugin.InitializeAsync"/> for every plugin. Must be called after
    /// the host is built. Plugins that need no setup inherit the default no-op.
    /// </summary>
    Task InitializePluginsAsync(IConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>Runs <see cref="IPlugin.ShutdownAsync"/> in reverse order.</summary>
    Task ShutdownPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts <see cref="InitializeAsync"/> for a graceful shutdown: the handler itself is
    /// still in the request pipeline, so it is reported as loaded, but its configuration may
    /// come from another configuration file since the last process start.
    /// </summary>
    void MarkAllUninitialized();
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

    public string PluginsPath => _pluginsPath;

    public void MarkAllUninitialized() => _initialized.Clear();

    public void LoadPlugins(IOcelotBuilder ocelotBuilder)
    {
        if (!Directory.Exists(_pluginsPath))
        {
            _logger.LogDebug("Plugins directory {PluginsPath} does not exist; no plugins loaded", _pluginsPath);
            return;
        }

        foreach (var assembly in LoadPluginAssemblies(_pluginsPath))
        {
            var fileName = string.IsNullOrEmpty(assembly.Location) ? null : Path.GetFileName(assembly.Location);

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

                RegisterPlugin(
                    type,
                    inspected: new PluginInfo
                    {
                        Name = string.Empty,
                        ClassName = type.Name,
                        Version = type.Assembly.GetName().Version?.ToString() ?? string.Empty,
                        FileName = fileName
                    },
                    ocelotBuilder);
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
                // A plugin that cannot initialize is a misconfiguration, not a fatal host error:
                // the gateway must still serve traffic (and /health) so operators can reach the
                // admin panel to fix it. Never log the metadata dictionary itself: it may
                // contain secrets. Only the keys that were supplied are reported.
                _logger.LogError(ex,
                    "Plugin {PluginName} failed to initialize and was disabled. " +
                    "Check the '{Section}:{PluginName}' configuration section.",
                    info.Name, MetadataSectionName, info.Name);
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

    /// <inheritdoc />
    public PluginValidationResult ValidatePluginAssembly(
        string assemblyPath,
        IConfiguration configuration,
        IReadOnlyCollection<string> existingNames,
        IReadOnlyCollection<string>? replacedFileNames = null)
    {
        var result = new PluginValidationResult();
        Assembly assembly;

        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
        }
        catch (BadImageFormatException)
        {
            result.Errors.Add("The file is not a managed .NET assembly.");
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"The assembly could not be loaded: {ex.GetBaseException().Message}");
            return result;
        }

        // Names that belong to the files being replaced are removed from the clash pool: the
        // new version of a plugin necessarily declares the same name.
        var replaced = replacedFileNames ?? [];
        var keptNames = _plugins
            .Where(p => p.FileName == null || !replaced.Contains(p.FileName, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .Concat(existingNames.Where(n => !_plugins.Any(p => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))));

        var seenNames = new HashSet<string>(keptNames, StringComparer.OrdinalIgnoreCase);

        foreach (var type in GetLoadableTypes(assembly))
        {
            try
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (typeof(DelegatingHandler).IsAssignableFrom(type) && !typeof(IPlugin).IsAssignableFrom(type))
                {
                    // Same legacy guard as LoadPlugins, reported to the operator instead of only logged.
                    if (HasLegacyPluginShape(type))
                    {
                        result.Warnings.Add(
                            $"{type.FullName} exposes Name/IsGlobal but does not implement {nameof(IPlugin)}; " +
                            "it will be skipped. Recompile it against ApiGateway.Contracts.");
                    }

                    continue;
                }

                if (!IsPluginType(type))
                    continue;

                if (Activator.CreateInstance(type) is not IPlugin probe)
                {
                    result.Errors.Add($"{type.FullName} does not implement {nameof(IPlugin)}.");
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(probe.Name) ? type.Name : probe.Name;

                if (!seenNames.Add(name))
                {
                    result.Errors.Add(
                        $"A plugin named '{name}' is already loaded or declared by this assembly. " +
                        "Plugin names must be unique: they identify the 'PluginMetadata' configuration section.");
                    continue;
                }

                result.Plugins.Add(new PluginInfo
                {
                    Name = name,
                    ClassName = type.Name,
                    Version = type.Assembly.GetName().Version?.ToString() ?? string.Empty,
                    FileName = replacedFileNames?.FirstOrDefault(),
                    IsGlobal = probe.IsGlobal
                });
            }
            catch (Exception ex)
            {
                result.Errors.Add(
                    $"{type.FullName} could not be instantiated for inspection: {ex.GetBaseException().Message}. " +
                    "Plugins must have a public parameterless constructor.");
            }
        }

        if (result.Errors.Count == 0 && result.Plugins.Count == 0)
        {
            result.Errors.Add(
                "The assembly does not contain any usable plugin. A plugin must be a non-abstract " +
                $"{nameof(DelegatingHandler)} implementing {nameof(IPlugin)} with a public parameterless constructor.");
        }

        if (assembly.Location is not null && !PathEquals(assembly.Location, assemblyPath))
        {
            // The CLR keys loaded assemblies by name: a plugin already present under this
            // name cannot be inspected from the upload folder in this process.
            result.Warnings.Add(
                $"An assembly named '{assembly.GetName().Name}' is already loaded in this process. " +
                "This file was validated as-is, but restarting the gateway is required for the new version " +
                "to take effect.");
        }

        return result;
    }

    private void RegisterPlugin(Type type, PluginInfo? inspected, IOcelotBuilder ocelotBuilder)
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

        ocelotBuilder.AddDelegatingHandler(type, probe.IsGlobal);

        var info = inspected ?? new PluginInfo();
        if (string.IsNullOrWhiteSpace(info.Name))
            info.Name = string.IsNullOrWhiteSpace(probe.Name) ? type.Name : probe.Name;
        if (string.IsNullOrWhiteSpace(info.ClassName))
            info.ClassName = type.Name;
        if (string.IsNullOrWhiteSpace(info.Version))
            info.Version = type.Assembly.GetName().Version?.ToString() ?? string.Empty;

        info.IsGlobal = probe.IsGlobal;

        _plugins.Add(info);
        _registered.Add((info, type));

        _logger.LogInformation(
            "Loaded plugin {PluginName} ({ClassName}) v{Version}, global: {IsGlobal}",
            info.Name, type.Name, info.Version, probe.IsGlobal);
    }

    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
        }
    }

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

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
            _logger.LogWarning(
                "No '{Section}:{PluginName}' configuration section found; the plugin will receive empty metadata. " +
                "Available sections: {AvailableKeys}",
                MetadataSectionName, info.Name, string.Join(", ", configuration.GetSection(MetadataSectionName).GetChildren().Select(c => c.Key)));
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

/// <summary>
/// Outcome of inspecting a plugin assembly before it is copied into the
/// <c>Plugins/</c> directory.
/// </summary>
public sealed class PluginValidationResult
{
    /// <summary>Plugins declared by the assembly, in declaration order.</summary>
    public List<PluginInfo> Plugins { get; } = new();

    /// <summary>Fatal problems: the assembly would not be loaded by the host.</summary>
    public List<string> Errors { get; } = new();

    /// <summary>Non-fatal observations, such as ignored legacy types.</summary>
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
