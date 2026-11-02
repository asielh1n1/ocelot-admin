using ApiGateway.Contracts;
using ApiGateway.Util;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ocelot.Cache.CacheManager;
using Ocelot.Configuration.Validator;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;
using Ocelot.Tracing.OpenTracing;
using Ocelot.Values;
using OpenTracing.Util;
using Polly;
using System;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

// The first argument selects the run mode. Anything other than an explicit CLI
// command is forwarded to the host instead, so the gateway can be started with
// regular arguments such as `--urls http://0.0.0.0:4000`. Without this, any
// argument (including --urls) made the process run the CLI user manager, print
// "Unknown command" and exit before the web server ever started.
var cliArguments = CliUserManager.TryParseCommandLine(args, out var cliCommand) ? cliCommand : null;

var builder = WebApplication.CreateBuilder(args);

// Logging: built-in Microsoft.Extensions.Logging only (no external logging framework).
// Levels come from the "Logging:LogLevel" configuration section.
builder.Logging.ClearProviders();

// CLI mode: plain text, single line keeps interactive prompts readable.
if (cliArguments != null)
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
    // raise EF Core logging to Warning to avoid SQL output
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
}
else
{
    // Server mode: JSON to stdout so container runtimes / aggregators can parse fields.
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    });
}

IConfiguration configuration = builder.Configuration;

// Anchor the content root to the directory that holds the application binaries, not to
// the caller's working directory. `dotnet /path/to/ApiGateway.dll` otherwise resolves
// ContentRootPath to the current directory, and every runtime file (ocelot.json,
// identity.db, Plugins/) would be created in the wrong place.
var contentRoot = AppContext.BaseDirectory;
builder.Environment.ContentRootPath = contentRoot;

// Ensure ocelot.json has all required keys before loading configuration
DatabaseSeeder.EnsureOcelotDefaults(contentRoot);

// Load configuration files first
builder.Configuration
    .SetBasePath(contentRoot)
    .AddJsonFile("appsettings.json", true, true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", true, true)
    .AddJsonFile("ocelot.json", true, true)
    .AddEnvironmentVariables();

// Now load AppSetting after configuration files are loaded
builder.Services.AddOptions<AppSetting>()
            .Bind(builder.Configuration.GetSection(AppSetting.ConfigurationKey))
            .ValidateDataAnnotations()
            .ValidateOnStart();

var settings = builder.Configuration.GetSection(AppSetting.ConfigurationKey).Get<AppSetting>();
if (settings == null)
{
    throw new InvalidOperationException($"AppSetting configuration section '{AppSetting.ConfigurationKey}' not found or is invalid.");
}
AppSettingsValidator.Validate(settings);

// Add EF Core and Identity (SQLite).
//
// A relative "Data Source=identity.db" is resolved by SQLite against the *process
// working directory*, not the content root. Launching the published binary from a
// shortcut, as a service, or from another folder therefore silently created a
// second, empty database somewhere else: the gateway looked "stuck" on the setup
// screen (or reported registration as already completed) while the real database
// sat next to the binaries. Anchor relative paths to the content root so there is
// exactly one identity.db, next to the application files.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=identity.db";

var sqliteConnectionString = new SqliteConnectionStringBuilder(connectionString);
if (!string.IsNullOrWhiteSpace(sqliteConnectionString.DataSource)
    && !Path.IsPathRooted(sqliteConnectionString.DataSource)
    && !sqliteConnectionString.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
{
    sqliteConnectionString.DataSource = Path.Combine(contentRoot, sqliteConnectionString.DataSource);
    connectionString = sqliteConnectionString.ConnectionString;
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    // Lockout
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.AllowedForNewUsers = true;
    // User
    // The username is free-form ("admin", "ops-team", ...). Email is optional and is
    // only used as a unique contact when supplied; it is no longer the login identifier
    // (the old "activate your account by email" flow was removed).
    // When RequireUniqueEmail is true, Identity rejects any user without an email with
    // "Email '<user name>' is invalid.", so it must stay false.
    options.User.RequireUniqueEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// --- Dynamic authentication providers registration ---
AuthenticationProvidersRegistry.RegisterAuthenticationProviders(builder, settings);

// --- Admin token service ---
// Signing key comes from AppSetting:AdminApi:SigningKey when configured (required for
// multi-instance deployments). Otherwise a random per-process key is generated.
var adminTokenService = new AdminTokenService(Options.Create(settings));
builder.Services.AddSingleton(adminTokenService);
builder.Services.AddAuthentication().AddJwtBearer("AdminApiScheme", options =>
{
    options.TokenValidationParameters = adminTokenService.ValidationParameters;
    options.MapInboundClaims = false;
});

if (!adminTokenService.UsesConfiguredKey && cliArguments == null)
{
    // CLI mode never starts the web server, so the warning is noise there: the random
    // key is created and discarded without any admin session being issued.
    Console.WriteLine(
        "[WARN] AppSetting:AdminApi:SigningKey is not configured. A random per-process key will be used: " +
        "admin sessions are invalidated on restart and will NOT be shared across instances. " +
        "Set 'AppSetting__AdminApi__SigningKey' for multi-instance deployments.");
}

builder.Services.AddAuthorization();

// Configuro los CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(Constant.CorsName, policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod();

        if (settings.AllowedCorsOrigins != null && settings.AllowedCorsOrigins.Count > 0)
        {
            policy.WithOrigins(settings.AllowedCorsOrigins.ToArray());
        }
        else if (builder.Environment.IsDevelopment())
        {
            // Only allow any origin in Development; production must configure AllowedCorsOrigins.
            policy.AllowAnyOrigin();
        }
        // In production with no configured origins, no cross-origin requests are allowed.
    });
});

builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerForOcelot(configuration);


// Registrar PluginService como singleton
var pluginsPath = Path.Combine(builder.Environment.ContentRootPath, "Plugins");

// NOTE: PluginService only needs a logger here. Plugins are instantiated with a
// parameterless constructor (no host services are injected) and receive their
// configuration through PluginMetadata at initialization time.
// The PluginService logger is resolved later from the real container, so no
// temporary service provider (and no duplicated singleton graph) is needed here.
var pluginService = new PluginService(pluginsPath, logger: null);
builder.Services.AddSingleton<IPluginService>(pluginService);

var ocelotBuilder = builder.Services.AddOcelot()
    .AddCacheManager(x => x.WithDictionaryHandle())
    .AddPolly()
    .AddOpenTracing();

// Cargar plugins antes de construir la app
pluginService.LoadPlugins(ocelotBuilder);

var app = builder.Build();

// If a CLI command was provided, run it and exit without starting the web server
if (cliArguments != null)
{
    await CliUserManager.RunAsync(cliArguments, app.Services, builder.Configuration);
    return;
}

app.UseCors(Constant.CorsName);
app.UseDefaultFiles();

// Security headers: mitigate clickjacking, MIME sniffing and enforce HTTPS when available.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-XSS-Protection"] = "0";
    if (context.Request.IsHttps)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    await next();
});

// AdminMiddleware handles:
// 1. /admin/index.html injection (AppName, PrimaryColor, LogoUrl)
// 2. /admin-api/* routes (login, data, auth)
app.UseMiddleware<AdminMiddleware>();

app.UseStaticFiles();

// Only activate Swagger UI when SwaggerEndPoints are configured (the library throws if the list is empty)
if (configuration.GetSection("SwaggerEndPoints").GetChildren().Any())
{
    app.UseSwaggerForOcelotUI(opt =>{}, uiOpt => {
        //swaggerUI options
        uiOpt.DocumentTitle = "Gateway documentation";
    });
}

// Apply pending migrations (create DB) and seed default admin if DB empty
await DatabaseSeeder.SeedAsync(app.Services, builder.Configuration);

// Initialize plugins. Runs after the host is built and the database is ready.
// A plugin that fails to initialize is disabled with an error in the log; the
// gateway keeps serving so the admin panel stays reachable.
await app.Services.GetRequiredService<IPluginService>()
    .InitializePluginsAsync(app.Configuration);

app.Lifetime.ApplicationStopping.Register(() =>
{
    app.Services.GetRequiredService<IPluginService>()
        .ShutdownPluginsAsync()
        .GetAwaiter()
        .GetResult();
});

app.UseAuthentication();
app.UseAuthorization();

// Health endpoint for container orchestrators (Docker HEALTHCHECK, Kubernetes probes).
// Deliberately minimal and anonymous: it reports liveness only, never configuration
// details or dependency state, so it is safe to expose.
//
// This is a terminal middleware rather than a mapped endpoint on purpose: UseOcelot
// below ends the pipeline for every route it handles, and an endpoint mapped before it
// was shadowed by Ocelot (which answered 404 for unmapped paths). A middleware is the
// only reliable point in front of Ocelot.
app.Use(async (context, next) =>
{
    if (!context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    context.Response.ContentType = "application/json; charset=utf-8";
    context.Response.Headers.CacheControl = "no-store";
    await context.Response.WriteAsJsonAsync(new
    {
        status = "healthy",
        version = AdminMiddleware.AppVersion
    });
});

app.UseWebSockets();

// Pre-flight: Ocelot refuses to start when a route references an authentication scheme
// that was never registered, and its message ("unsupported authentication provider")
// does not say how to fix it. Check first and name the exact route and config key.
//
// Both route properties must be inspected: Ocelot accepts the single
// "AuthenticationProviderKey" and the list "AuthenticationProviderKeys". Checking only
// the list let routes with the singular property reach Ocelot, which then aborted the
// whole gateway with an obscure FileValidationFailedError.
var configuredProviders = settings.AuthenticationProviders?
    .Select(p => p.Key)
    .Where(k => !string.IsNullOrWhiteSpace(k))
    .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

var declaredProviders = string.Join(", ",
    configuredProviders.Count > 0 ? configuredProviders : ["<none>"]);

var missingProviders = configuration.GetSection("Routes").GetChildren()
    .SelectMany(route =>
    {
        var upstream = route["UpstreamPathTemplate"] ?? route.Key;
        var keys = route.GetSection("AuthenticationOptions:AuthenticationProviderKeys").GetChildren()
            .Select(key => key.Value)
            .Append(route["AuthenticationOptions:AuthenticationProviderKey"]);

        return keys.Select(key => (Route: upstream, Provider: key));
    })
    .Where(x => !string.IsNullOrWhiteSpace(x.Provider) && !configuredProviders.Contains(x.Provider!))
    .GroupBy(x => x.Provider!, StringComparer.OrdinalIgnoreCase)
    .ToList();

if (missingProviders.Count > 0)
{
    foreach (var group in missingProviders)
    {
        var routes = string.Join(", ", group.Select(x => x.Route).Distinct());

        app.Logger.LogError(
            "Route(s) {Routes} require the authentication provider '{Provider}', which is not configured. " +
            "Configured providers: {Configured}. Add it under AppSetting:AuthenticationProviders, " +
            "remove it from those routes' AuthenticationOptions, or set AllowAnonymous on those routes.",
            routes, group.Key, declaredProviders);
    }

    throw new InvalidOperationException(
        "Ocelot routes reference authentication providers that are not configured: " +
        string.Join(", ", missingProviders.Select(g => g.Key)) +
        $" (configured: {declaredProviders}). " +
        "Configure them under AppSetting:AuthenticationProviders (see appsettings.json).");
}

// Tracing Middleware
var pipeline = new OcelotPipelineConfiguration
{
    PreAuthenticationMiddleware = async (context, next) =>
    {
        // Do NOT capture HttpContext inside a fire-and-forget Task: HttpContext is not
        // thread-safe and becomes invalid once the request completes. We capture only the
        // immutable data the tracer needs (method, url, headers, body) up-front.
        var trace = TracingHandler.CaptureRequest(context);
        if (trace != null)
        {
            var traceLogger = context.RequestServices.GetService<ILogger<TracingHandler>>();
            _ = Task.Run(() => TracingHandler.SendAsync(trace, traceLogger));
        }

        // Route metadata for plugins.
        //
        // DownstreamRouteFinderMiddleware runs earlier in the Ocelot pipeline, so by this
        // point the matched DownstreamRoute is already in HttpContext.Items. Its
        // MetadataOptions.Metadata holds the route "Metadata" merged with
        // "GlobalConfiguration:Metadata" (route values win), exactly what an operator
        // expects from "metadata of this endpoint".
        //
        // We publish it through an AsyncLocal (RouteMetadataAccessor) instead of injecting
        // HTTP headers: plugins read it in their SendAsync, nothing is forwarded to the
        // downstream service, and nothing has to be cleaned up afterwards. The scope is
        // disposed when the request finishes, so the value can never leak into another
        // request served by the same pooled handler instance.
        var route = context.Items.DownstreamRoute();
        if (route is null)
        {
            await next.Invoke();
            return;
        }

        using var metadataScope = RouteMetadataAccessor.Push(
            new RouteMetadata(context.Request.Path.Value, route.MetadataOptions?.Metadata));

        await next.Invoke();
    }
};
app.UseOcelot(pipeline).Wait();

app.Run();
