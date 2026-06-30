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
// Disable verbose DB logs when running in CLI mode
if (args.Length > 0)
{
    // remove default console/provider to keep CLI output clean
    builder.Logging.ClearProviders();
    builder.Logging.AddLog4Net();
    // raise EF Core logging to Warning to avoid SQL output
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
}
else
{
    // regular startup keeps log4net plus default providers
    builder.Logging.AddLog4Net();
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

// --- Admin token service (in-memory key, regenerated on each startup) ---
var adminTokenService = new AdminTokenService();
builder.Services.AddSingleton(adminTokenService);
builder.Services.AddAuthentication().AddJwtBearer("AdminApiScheme", options =>
{
    options.TokenValidationParameters = adminTokenService.ValidationParameters;
    options.MapInboundClaims = false;
});

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
        else
        {
            policy.AllowAnyOrigin();
        }
    });
});

builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerForOcelot(configuration);


// Registrar PluginService como singleton
var pluginsPath = Path.Combine(builder.Environment.ContentRootPath, "Plugins");
using var pluginProvider = builder.Services.BuildServiceProvider();
var pluginService = new PluginService(pluginsPath, pluginProvider);
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

app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets();
// Tracing Middleware
var pipeline = new OcelotPipelineConfiguration
{
    PreAuthenticationMiddleware = async(context, next) =>
    {
        Task.Run(async delegate { TracingHandler.SendAsync(context); });
        await next.Invoke();
    }
};
app.UseOcelot(pipeline).Wait();


app.Run();
