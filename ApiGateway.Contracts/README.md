# ocelot-admin-plugin

Contract library for building plugins for **Ocelot Admin** — the administration panel and
API Gateway built on [Ocelot](https://github.com/ThreeMammals/Ocelot).

A plugin is a `DelegatingHandler` that participates in the gateway request pipeline and can
also run one-time setup (create its own database tables, open connections, warm caches,
authenticate against a remote API).

## Install

```bash
dotnet add package ocelot-admin-plugin
```

> Reference this package with `ExcludeAssets="runtime"` and `Private="false"`.
> The host already ships the contract assembly; bundling a second copy breaks
> type identity and the plugin will be skipped.

```xml
<PackageReference Include="ocelot-admin-plugin" Version="1.0.0">
  <ExcludeAssets>runtime</ExcludeAssets>
  <Private>false</Private>
</PackageReference>
```

## Minimal plugin

```csharp
using System.Net.Http;
using ApiGateway.Contracts;

public sealed class AddHeaderPlugin : DelegatingHandler, IPlugin
{
    public string Name => "Add Header";
    public bool IsGlobal => true;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("X-Gateway", "true");
        return base.SendAsync(request, cancellationToken);
    }
}
```

## Plugin with initialization

Override `InitializeAsync` only when setup is required. It receives a `PluginMetadata`
instance containing **only this plugin's own configuration**.

```csharp
public sealed class CachePlugin : DelegatingHandler, IPlugin
{
    public string Name => "PostgreSQL Cache";
    public bool IsGlobal => true;

    // Shared by every handler instance Ocelot creates.
    private static string? _connectionString;

    public Task InitializeAsync(PluginMetadata metadata, CancellationToken cancellationToken)
    {
        _connectionString = metadata.GetRequired("ConnectionString")
            ?? throw new InvalidOperationException(
                $"'{metadata.PluginName}' requires 'PluginMetadata:{metadata.PluginName}:ConnectionString'.");

        return Task.CompletedTask;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Use _connectionString here.
        return base.SendAsync(request, cancellationToken);
    }
}
```

### Why `static`?

The instance that receives `InitializeAsync` is **not** the instance Ocelot uses to serve
requests — Ocelot creates its own handler instances from the DI container. Startup state
must therefore live in `static` fields (or in a `Lazy<T>` helper) so every handler
instance can reach it.

## Configuration

Configuration lives under `PluginMetadata`, keyed by the plugin's `Name` (the class name
works as a fallback):

```json
{
  "PluginMetadata": {
    "PostgreSQL Cache": {
      "ConnectionString": "",
      "TableName": "gateway_cache",
      "AutoMigrate": "true"
    }
  }
}
```

In containers, inject through environment variables — never commit secrets:

```bash
PluginMetadata__PostgreSQL_Cache__ConnectionString="Host=pg;Database=cache;Username=gw;Password=***"
```

## `PluginMetadata` API

| Member | Description |
|---|---|
| `PluginName` | Name of the plugin these values belong to |
| `Values` | Raw read-only dictionary |
| `Get(key)` | Value or `null` |
| `GetRequired(key)` | Value, or `null` when missing/empty (use for mandatory settings) |
| `GetOrDefault(key, fallback)` | Value or fallback |
| `GetBool(key, fallback)` | Parsed boolean |
| `GetInt(key, fallback)` | Parsed integer (invariant culture) |
| `Has(key)` | `true` when the key has a non-empty value |
| `Keys` | All configured keys |

## Contracts

| Member | Required | Description |
|---|---|---|
| `Name` | Yes | Display name in the admin panel; configuration key |
| `IsGlobal` | Yes | Run for every route (`true`) or only where explicitly referenced |
| `InitializeAsync` | No | One-time setup at startup. Default: no-op |
| `ShutdownAsync` | No | Graceful shutdown, reverse order. Default: no-op |

## Deployment

Copy the plugin DLL into the host's `Plugins/` directory:

```
Plugins/
└── MyPlugin.dll
```

Do **not** copy `ApiGateway.dll` or `ApiGateway.Contracts.dll` there. The host skips them
with a warning to protect type identity.

## Requirements

- .NET 10.0
- Host: Ocelot Admin

## License

MIT
