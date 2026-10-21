using ApiGateway.Util;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
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

var builder = WebApplication.CreateBuilder(args);

// Logging: built-in Microsoft.Extensions.Logging only (no external logging framework).
// Levels come from the "Logging:LogLevel" configuration section.
builder.Logging.ClearProviders();

if (args.Length > 0)
{
    // CLI mode: plain text, single line keeps interactive prompts readable.
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

// Ensure ocelot.json has all required keys before loading configuration
DatabaseSeeder.EnsureOcelotDefaults(builder.Environment.ContentRootPath);

// Load configuration files first
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
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

// Add EF Core and Identity (SQLite)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=identity.db");
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
    options.User.RequireUniqueEmail = true;
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

if (!adminTokenService.UsesConfiguredKey)
{
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
var pluginLogger = builder.Services.BuildServiceProvider()
    .GetService<ILoggerFactory>()?.CreateLogger<PluginService>();
var pluginService = new PluginService(pluginsPath, pluginLogger);
builder.Services.AddSingleton<IPluginService>(pluginService);

var ocelotBuilder = builder.Services.AddOcelot()
    .AddCacheManager(x => x.WithDictionaryHandle())
    .AddPolly()
    .AddOpenTracing();

// Cargar plugins antes de construir la app
pluginService.LoadPlugins(ocelotBuilder);

var app = builder.Build();

// If arguments provided, run CLI mode and exit without starting web server
if (args.Length > 0)
{
    await CliUserManager.RunAsync(args, app.Services, builder.Configuration);
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
// A plugin that fails to initialize aborts startup.
await app.Services.GetRequiredService<IPluginService>()
    .InitializePluginsAsync(builder.Configuration);

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
app.MapGet("/health", () => Results.Json(new
{
    status = "healthy",
    version = AdminMiddleware.AppVersion
})).AllowAnonymous();

app.UseWebSockets();
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
        await next.Invoke();
    }
};
app.UseOcelot(pipeline).Wait();


app.Run();
