# Building plugins for Ocelot Admin

Complete guide to writing, configuring and shipping a gateway plugin, including the
runtime **route metadata** feature added in `ocelot-admin-plugin` **1.1.0**.

---

## Contents

1. [What a plugin is](#1-what-a-plugin-is)
2. [Prerequisites](#2-prerequisites)
3. [Project setup](#3-project-setup)
4. [Minimal plugin](#4-minimal-plugin)
5. [Global vs route-specific plugins](#5-global-vs-route-specific-plugins)
6. [Where a plugin runs in the pipeline](#6-where-a-plugin-runs-in-the-pipeline)
7. [Startup configuration: `PluginMetadata`](#7-startup-configuration-pluginmetadata)
8. [Per-request route metadata: `RouteMetadata`](#8-per-request-route-metadata-routemetadata)
9. [`PluginMetadata` or `RouteMetadata`?](#9-pluginmetadata-or-routemetadata)
10. [State shared between handler instances](#10-state-shared-between-handler-instances)
11. [Building the plugin](#11-Building-the-plugin)
12. [Deployment](#12-deployment)
13. [Testing locally](#13-testing-locally)
14. [Troubleshooting](#14-troubleshooting)

---

## 1. What a plugin is

A plugin is a .NET class that:

- derives from `System.Net.Http.DelegatingHandler`, so Ocelot can insert it into the
  outgoing (downstream) HTTP pipeline, and
- implements `ApiGateway.Contracts.IPlugin`, so the host can discover it, name it, and
  decide whether it runs on every route or only on the routes that reference it.

```csharp
public sealed class MyPlugin : DelegatingHandler, IPlugin
{
    public string Name => "My plugin";     // shown in the admin panel, config key
    public bool IsGlobal => true;          // run on every route
}
```

Everything else (`InitializeAsync`, `ShutdownAsync`) has a default implementation, so a
plugin that needs nothing more than "do something with the request" is three members long.

The plugin assembly is copied into the host's `Plugins/` directory and loaded at startup.
The host never injects its own services into a plugin: no `IServiceProvider`, no
`DbContext`, no host configuration. A plugin is self-contained.

---

## 2. Prerequisites

- .NET 10.0 SDK to build, .NET 10.0 runtime (or the published self-contained host) to run.
- The contract package `ocelot-admin-plugin` **1.1.0 or later** for route metadata support.

---

## 3. Project setup

```bash
dotnet new classlib -n MyGatewayPlugin -f net10.0
cd MyGatewayPlugin
dotnet add package ocelot-admin-plugin --version 1.1.0
```

The reference **must not** copy the contract assembly into your output. The host already
ships `ApiGateway.Contracts.dll`, and a second copy breaks type identity: the host would
load your plugin and then fail (or silently skip it) because `IPlugin` in your assembly is
not the same type as `IPlugin` in the host.

`MyGatewayPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- The host loads this assembly by path; the name is what appears in logs. -->
    <AssemblyName>MyGatewayPlugin</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ocelot-admin-plugin" Version="1.1.0">
      <ExcludeAssets>runtime</ExcludeAssets>
      <Private>false</Private>
    </PackageReference>
  </ItemGroup>

</Project>
```

> When developing inside this repository, use a `ProjectReference` to
> `ApiGateway.Contracts\ApiGateway.Contracts.csproj` and set `Private="false"` so the DLL
> is not copied into the plugin's output folder either.

---

## 4. Minimal plugin

```csharp
using System.Net.Http;
using ApiGateway.Contracts;

namespace MyGatewayPlugin;

public sealed class TenantHeaderPlugin : DelegatingHandler, IPlugin
{
    public string Name => "Tenant Header";
    public bool IsGlobal => true;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("X-Gateway", "ocelot-admin");
        return base.SendAsync(request, cancellationToken);
    }
}
```

That is enough: build the DLL, drop it in `Plugins/`, restart the gateway, and the plugin
appears in the admin panel (`/admin` → Delegating handlers).

---

## 5. Global vs route-specific plugins

`IsGlobal` decides **where** the handler is inserted:

| `IsGlobal` | Behaviour | Route configuration |
|---|---|---|
| `true` | Runs for every request, before any route-specific handler | nothing to do |
| `false` | Runs only on routes that reference it by **class name** | `"DelegatingHandlers": [ "MyHandler" ]` |

Route-specific plugin (`ocelot.json` → `Routes[]`):

```json
{
  "UpstreamPathTemplate": "/orders/{id}",
  "DownstreamPathTemplate": "/api/orders/{id}",
  "DelegatingHandlers": [ "TenantHeaderPlugin" ]
}
```

The name in `DelegatingHandlers` is the **class name**, not `IPlugin.Name`.

Execution order inside one route:

1. Global handlers, in the order they were loaded from `Plugins/`.
2. Route handlers, in the order they appear in `DelegatingHandlers`.
3. Ocelot's own tracing handler (if `HttpHandlerOptions.UseTracing` is on).
4. Ocelot's QoS/circuit-breaker handler (if the route uses `QoSOptions`).
5. The real `HttpClientHandler`, which sends the request downstream.

The **first** handler of that list is the outermost one (closest to the gateway), the
last one is closest to the network.

---

## 6. Where a plugin runs in the pipeline

Ocelot's pipeline (abridged, in execution order):

| # | Middleware | Notes |
|---|---|---|
| 1 | `ConfigurationMiddleware` | loads the current Ocelot configuration |
| 2 | `ExceptionHandlerMiddleware` | global error handling |
| 3 | `PreErrorResponderMiddleware` | user hook |
| 4 | `ResponderMiddleware` | writes the response back to the client |
| 5 | `DownstreamRouteFinderMiddleware` | **matches the route and stores it in `HttpContext.Items`** |
| 6 | `MultiplexingMiddleware` | aggregates |
| 7 | `SecurityMiddleware` | IP allow/deny lists |
| 8 | `HttpHeadersTransformationMiddleware` | `AddHeadersToRequest` / find-and-replace |
| 9 | `DownstreamRequestInitialiserMiddleware` | builds the downstream `HttpRequestMessage` |
| 10 | `RateLimitingMiddleware` | |
| 11 | `RequestIdMiddleware` | |
| 12 | `PreAuthenticationMiddleware` | user hook — **the host publishes route metadata here** |
| 13 | `AuthenticationMiddleware` | |
| ... | | |
| 22 | `HttpRequesterMiddleware` | **your `DelegatingHandler.SendAsync` runs here** |

Two consequences worth remembering:

- By the time a plugin handler runs, the route metadata is already available.
- Everything the plugin adds to `HttpRequestMessage.Headers` **is sent downstream**. Use
  `RouteMetadataAccessor` instead of inventing private headers (see section 8).

---

## 7. Startup configuration: `PluginMetadata`

`PluginMetadata` is read **once at startup** from the `PluginMetadata:<plugin name>`
section of `appsettings.json` (the class name works as a fallback key). It is meant for
settings that do not change per request: connection strings, credentials, table names,
feature switches.

```json
{
  "PluginMetadata": {
    "PostgreSQL Cache": {
      "ConnectionString": "Host=pg;Database=cache;Username=gw;Password=***",
      "TableName": "gateway_cache",
      "AutoMigrate": "true",
      "TtlSeconds": "60"
    }
  }
}
```

In containers, never commit secrets — use environment variables:

```bash
PluginMetadata__PostgreSQL_Cache__ConnectionString="Host=pg;Database=cache;Username=gw;Password=***"
```

### API

| Member | Description |
|---|---|
| `PluginName` | Name of the plugin these values belong to |
| `Values` | Raw read-only dictionary |
| `Get(key)` | Value or `null` |
| `GetRequired(key)` | Value, or `null` when missing/empty (for mandatory settings) |
| `GetOrDefault(key, fallback)` | Value or fallback |
| `GetBool(key, fallback)` | Parsed boolean |
| `GetInt(key, fallback)` | Parsed integer (invariant culture) |
| `Has(key)` | `true` when the key has a non-empty value |
| `Keys` | All configured keys |

### Example

```csharp
public sealed class CachePlugin : DelegatingHandler, IPlugin
{
    public string Name => "PostgreSQL Cache";
    public bool IsGlobal => true;

    // Request handlers are created by Ocelot, not by the host: keep startup state static.
    private static string? _connectionString;
    private static int _ttlSeconds = 60;

    public Task InitializeAsync(PluginMetadata metadata, CancellationToken cancellationToken)
    {
        _connectionString = metadata.GetRequired("ConnectionString")
            ?? throw new InvalidOperationException(
                $"'{metadata.PluginName}' requires 'PluginMetadata:{metadata.PluginName}:ConnectionString'.");

        _ttlSeconds = metadata.GetInt("TtlSeconds", 60);
        return Task.CompletedTask;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Use _connectionString / _ttlSeconds here.
        return base.SendAsync(request, cancellationToken);
    }
}
```

Throwing from `InitializeAsync` disables that plugin and logs the error; the gateway keeps
serving so the admin panel stays reachable.

---

## 8. Per-request route metadata: `RouteMetadata`

Ocelot lets every route declare an arbitrary JSON dictionary:

```json
{
  "GlobalConfiguration": {
    "Metadata": {
      "region": "eu",
      "cache.enabled": "true"
    }
  },
  "Routes": [
    {
      "UpstreamPathTemplate": "/api/orders",
      "DownstreamPathTemplate": "/api/orders",
      "Metadata": {
        "region": "us",
        "cache.ttl": "30"
      }
    }
  ]
}
```

Ocelot **merges** `GlobalConfiguration:Metadata` with the route `Metadata` when the
configuration is loaded, and the **route wins**. For `/api/orders` a plugin sees:

| Key | Value | Origin |
|---|---|---|
| `region` | `us` | route overrides global |
| `cache.enabled` | `true` | global |
| `cache.ttl` | `30` | route |

### How the plugin reads it

The host publishes the merged metadata before any delegating handler runs, through an
`AsyncLocal` scope (`RouteMetadataAccessor`). A plugin simply reads it:

```csharp
using System.Net.Http;
using ApiGateway.Contracts;

namespace MyGatewayPlugin;

public sealed class CachePlugin : DelegatingHandler, IPlugin
{
    public string Name => "Route Cache";
    public bool IsGlobal => true;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var metadata = RouteMetadataAccessor.Current;

        if (metadata?.GetBool("cache.enabled") == true)
        {
            var ttl = metadata.GetInt("cache.ttl", 60);
            var region = metadata.GetOrDefault("region", "unknown");

            // ... use ttl / region (cache key, header, telemetry, ...)
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
```

No headers are involved, so **nothing extra reaches the downstream service** and there is
no cleanup step. Rejected requests, short-circuited requests and cached responses behave
exactly as before.

### API

| Member | Description |
|---|---|
| `UpstreamPath` | Upstream path of the current request (for logs/telemetry) |
| `Values` | Read-only dictionary of the merged metadata |
| `Count` | Number of keys; `0` when the route has no metadata |
| `Get(key)` | Value or `null` |
| `GetOrDefault(key, fallback)` | Value or fallback |
| `GetBool(key, fallback)` | Parsed boolean |
| `GetInt(key, fallback)` | Parsed integer (invariant culture) |
| `Has(key)` | `true` when the key has a non-empty value |

`RouteMetadataAccessor.Current` is `null` only when the call happens outside a gateway
request. Inside `SendAsync` it is always set: a route without `Metadata` produces an
instance with `Count == 0`.

### Rules

- **Do not mutate** `Values`. Ocelot caches one `DownstreamRoute` (and therefore one
  metadata dictionary) per route, shared by every request. `RouteMetadata` copies the
  dictionary, but treat it as immutable anyway.
- **Do not capture** `Current` in fire-and-forget work (`Task.Run`, timers, static caches):
  copy the values you need while you are inside the request.
- **Do not copy the metadata into headers** unless you really want the downstream service
  to receive it. If a third-party handler needs it on the `HttpRequestMessage`, put it in
  `request.Options`, which is never serialized.
- Metadata values are **strings**. Parse them with `GetBool`/`GetInt`, or deserialize JSON
  manually if you need a complex object.

### Full example: per-route behaviour switch

```json
{
  "Routes": [
    {
      "UpstreamPathTemplate": "/api/reports/{everything}",
      "DownstreamPathTemplate": "/reports/{everything}",
      "Metadata": {
        "timeout.ms": "15000",
        "audit.enabled": "true"
      }
    }
  ]
}
```

```csharp
public sealed class AuditPlugin : DelegatingHandler, IPlugin
{
    public string Name => "Audit";
    public bool IsGlobal => true;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var metadata = RouteMetadataAccessor.Current;
        if (metadata?.GetBool("audit.enabled") != true)
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        // Log method + route + status + elapsed; the audit header never leaves the gateway.
        return response;
    }
}
```

---

## 9. `PluginMetadata` or `RouteMetadata`?

Both are still used, for different things. Keep them separate:

| | `PluginMetadata` | `RouteMetadata` |
|---|---|---|
| Source | `appsettings.json` → `PluginMetadata:<plugin name>` | `ocelot.json` → route `Metadata` + `GlobalConfiguration:Metadata` |
| When | Once, at startup | On every request |
| Scope | The plugin, for the whole process | The matched route |
| Typical content | Connection strings, credentials, table names, defaults | Feature flags, TTLs, tenant/region, per-endpoint tuning |
| Who edits it | Operator, in `appsettings` / env vars | Operator, in the admin panel or `ocelot.json` |
| Change requires restart | Yes | Only an Ocelot configuration reload |
| Access | `InitializeAsync(PluginMetadata metadata, ...)` | `RouteMetadataAccessor.Current` |

A plugin commonly needs both: `PluginMetadata` for "where is my database", and
`RouteMetadata` for "should this particular endpoint be cached, and for how long".

---

## 10. State shared between handler instances

`InitializeAsync` is called on a **dedicated startup instance**. Ocelot creates its own
transient handler instances to serve requests (it resolves them from the DI container), so
anything you store in instance fields during initialization is invisible to the request
handlers.

Use `static` fields, a `static readonly Lazy<T>`, or a static singleton helper:

```csharp
private static HttpClient? _client;

public Task InitializeAsync(PluginMetadata metadata, CancellationToken ct)
{
    _client = new HttpClient { BaseAddress = new Uri(metadata.GetRequired("BaseUrl")!) };
    return Task.CompletedTask;
}
```

Route metadata does **not** have this problem: it is read per request from the ambient
accessor, so no state is kept between requests.

---

---

## 11. Building the plugin

The host loads a **DLL copied into `Plugins/`**; it never compiles anything for you. You
build the plugin on your machine and ship only the resulting files.

### 11.1 Standard build

```bash
dotnet build -c Release
```

Output:

```
MyGatewayPlugin/
└── bin/
    └── Release/
        └── net10.0/
            ├── MyGatewayPlugin.dll        ← this is what you copy
            ├── MyGatewayPlugin.pdb        ← optional, only for stack traces
            └── (your private dependencies)
```

### 11.2 Publish to a clean folder (recommended)

`dotnet build` leaves the whole dependency graph in `bin/`; `dotnet publish` produces a
folder containing exactly what the plugin needs at runtime, which makes it obvious which
files belong in `Plugins/`.

```bash
dotnet publish -c Release -o ./dist
```

If your plugin has **no third-party dependencies**, `./dist` usually contains a single file:

```
dist/
└── MyGatewayPlugin.dll
```

If it does have dependencies (a database driver, a JSON library, ...), copy the whole
`dist/` folder contents **except** `ApiGateway.Contracts.dll`:

```bash
rm -f ./dist/ApiGateway.Contracts.dll
```

`ExcludeAssets="runtime"` + `Private="false"` from section 3 already prevent that file from
being emitted; the `rm` is a safety net, for example after switching the reference from a
`ProjectReference` to a `PackageReference`.

### 11.3 Copy it into `Plugins/`

Local run (`dotnet run` or the published binaries — the folder next to `ApiGateway.dll`):

```powershell
# Windows PowerShell
New-Item -ItemType Directory -Force -Path .\bin\Release\net10.0\Plugins | Out-Null
Copy-Item .\dist\MyGatewayPlugin.dll .\bin\Release\net10.0\Plugins\
```

```bash
# Linux / macOS
mkdir -p bin/Release/net10.0/Plugins
cp dist/MyGatewayPlugin.dll bin/Release/net10.0/Plugins/
```

Docker Compose — the `Plugins/` folder next to `docker-compose.yml` is mounted at
`/app/Plugins`:

```bash
mkdir -p Plugins
cp dist/MyGatewayPlugin.dll Plugins/
docker compose restart gateway
```

No image rebuild is needed: the folder is mounted, so a restart is enough for the host to
scan it again.

## 12. Deployment

```
<gateway directory>/
├── ApiGateway.dll
├── Plugins/
│   ├── MyGatewayPlugin.dll          ← your plugin
│   └── MyGatewayPlugin.deps.json    ← only if you have extra dependencies
└── ...
```

Rules:

- Copy **only your own assemblies** (plus their private dependencies) into `Plugins/`.
- Never copy `ApiGateway.dll` or `ApiGateway.Contracts.dll` there — the host skips them
  with a warning to protect type identity.
- Restart the gateway (or use the container's restart policy). Plugins are loaded once at
  startup; a plugin that fails to load is reported in the logs and in the admin panel.

In Docker, mount the folder:

```yaml
services:
  gateway:
    image: ghcr.io/asielh1n1/ocelot-admin:latest
    volumes:
      - ./plugins:/app/Plugins:ro
```

---

## 13. Testing locally

1. Build the plugin.
2. Copy the DLL to `<gateway>/Plugins/`.
3. Start the gateway in `Development` (`dotnet run` from the repository root).
4. Open `http://localhost:4000/admin`, log in, and check **Delegating handlers** — the
   plugin must be listed there together with its global/route scope.
5. Edit the route in the admin panel and add `Metadata` (key/value) and, for
   route-specific plugins, the class name in `DelegatingHandlers`.
6. Send a request through the gateway and verify the behaviour.

A complete, compiling example lives in `samples/SamplePlugin`:

- `SampleHeaderPlugin` — global handler; shows startup metadata and reading
  `RouteMetadataAccessor.Current`.
- `ShortCircuitPlugin` — route-specific handler; returns `503` without calling downstream.

---

## 14. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Plugin not listed in the admin panel | DLL not in `Plugins/`, or type does not derive from `DelegatingHandler`/`IPlugin` | Check the logs for `Loaded {Count} plugin(s)` |
| Warning "plugins must not include the host contract" | `ApiGateway.Contracts.dll` was copied next to the plugin | Add `ExcludeAssets="runtime"` and `Private="false"` to the reference |
| Warning "exposes Name/IsGlobal but does not implement IPlugin" | Plugin compiled against the legacy duck-typed contract | Recompile against `ocelot-admin-plugin` |
| `RouteMetadataAccessor.Current` is `null` | Reading it outside a request (e.g. in `InitializeAsync`) | Read it only inside `SendAsync` |
| Metadata is empty (`Count == 0`) | No `Metadata` on the route and none in `GlobalConfiguration` | Add metadata to the route or globally |
| Route-specific plugin never runs | Class name not in `DelegatingHandlers` | Add the **class name** to the array |
| Initialization data is `null` inside `SendAsync` | Stored in instance fields instead of `static` | Use static fields, see section 10 |

---