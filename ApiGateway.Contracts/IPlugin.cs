namespace ApiGateway.Contracts;

/// <summary>
/// Contract that every gateway plugin must implement.
/// </summary>
/// <remarks>
/// A plugin is also expected to derive from <see cref="System.Net.Http.DelegatingHandler"/>
/// so that Ocelot can insert it into the request pipeline. This interface cannot enforce
/// that inheritance (C# interfaces cannot derive from classes), so the host validates
/// both conditions when it discovers plugin types.
/// <para>
/// <see cref="InitializeAsync"/> and <see cref="ShutdownAsync"/> have default
/// implementations, so a plugin that needs no setup only declares
/// <see cref="Name"/> and <see cref="IsGlobal"/>.
/// </para>
/// <para>
/// Minimal plugin:
/// <code>
/// public class MyPlugin : DelegatingHandler, IPlugin
/// {
///     public string Name => "My plugin";
///     public bool IsGlobal => false;
/// }
/// </code>
/// Plugin that needs setup:
/// <code>
/// public class CachePlugin : DelegatingHandler, IPlugin
/// {
///     public string Name => "Cache";
///     public bool IsGlobal => true;
///
///     public async Task InitializeAsync(PluginMetadata metadata, CancellationToken ct)
///     {
///         var cs = metadata.GetRequired("ConnectionString")
///             ?? throw new InvalidOperationException("ConnectionString is required.");
///         // create tables, open connections, etc.
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IPlugin
{
    /// <summary>
    /// Human-readable plugin name. Displayed in the admin panel and used to locate
    /// this plugin's configuration section under <c>PluginMetadata</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// When <see langword="true"/>, the delegating handler runs for every route.
    /// When <see langword="false"/>, it only runs for routes that reference it explicitly.
    /// </summary>
    bool IsGlobal { get; }

    /// <summary>
    /// Called once at startup, after the host is fully built and its database is ready.
    /// Override it only when the plugin needs setup: creating tables in its own database,
    /// opening connections, warming caches, authenticating against a remote API, and so on.
    /// </summary>
    /// <param name="metadata">
    /// Configuration for this plugin, read from
    /// <c>PluginMetadata:&lt;plugin name&gt;</c>. Never contains host configuration,
    /// and gives no access to the host service provider or host services.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the startup sequence.</param>
    /// <remarks>
    /// Throwing from this method aborts application startup. This is intentional:
    /// a plugin that cannot initialize (for example, an unreachable cache database)
    /// would otherwise fail on every request.
    /// </remarks>
    Task InitializeAsync(PluginMetadata metadata, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called once during graceful shutdown, in reverse initialization order.
    /// Use it to dispose connections or flush buffers.
    /// The default implementation does nothing.
    /// </summary>
    Task ShutdownAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
