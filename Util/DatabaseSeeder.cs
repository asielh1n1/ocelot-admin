using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using System.IO;

namespace ApiGateway.Util
{
    public static class DatabaseSeeder
    {
        /// <summary>
        /// Ensures database is created and the Administrator role exists.
        /// User creation is handled by the /admin-api/register endpoint.
        /// </summary>
        public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
        {
            using var scope = services.CreateScope();
            var svc = scope.ServiceProvider;
            var logger = svc.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

            try
            {
                var db = svc.GetRequiredService<ApplicationDbContext>();
                // Ensure database and schema are created
                try
                {
                    db.Database.EnsureCreated();
                }
                catch (Exception ensureEx)
                {
                    logger.LogError(ensureEx, "EnsureCreated failed");
                }

                var roleManager = svc.GetRequiredService<RoleManager<IdentityRole>>();
                var roleName = "Administrator";
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                    logger.LogInformation("Created '{Role}' role", roleName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ensuring database schema");
            }
        }

        /// <summary>
        /// Ensures ocelot.json contains required top-level keys (e.g. SwaggerEndPoints)
        /// so that libraries like MMLib.SwaggerForOcelot always find the sections they expect.
        /// Must be called before loading ocelot.json into configuration.
        /// </summary>
        public static void EnsureOcelotDefaults(string contentRootPath)
        {
            var ocelotPath = Path.Combine(contentRootPath, "ocelot.json");

            try
            {
                if (!File.Exists(ocelotPath))
                {
                    var defaults = new
                    {
                        Routes = Array.Empty<object>(),
                        DynamicRoutes = Array.Empty<object>(),
                        Aggregates = Array.Empty<object>(),
                        SwaggerEndPoints = Array.Empty<object>(),
                        GlobalConfiguration = new
                        {
                            BaseUrl = "http://localhost:4000",
                            RequestIdKey = "RequestId"
                        }
                    };
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(ocelotPath, JsonSerializer.Serialize(defaults, options));
                    return;
                }

                var json = File.ReadAllText(ocelotPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("SwaggerEndPoints", out _))
                    return;

                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict == null)
                    return;

                dict["SwaggerEndPoints"] = JsonSerializer.Deserialize<JsonElement>("[]");
                var writeOptions = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(ocelotPath, JsonSerializer.Serialize(dict, writeOptions));
            }
            catch
            {
                // Silently ignore – the app will fail later with a clearer error from the library
            }
        }
    }
}
