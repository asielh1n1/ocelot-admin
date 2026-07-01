using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Validator;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ApiGateway.Util
{
    public class AdminMiddleware
    {
        private const string AdminScheme = "AdminApiScheme";
        private const string BaseUrl = "/admin-api";
        private const int MaxBodySize = 50 * 1024 * 1024;

        private const string RegistrationServerUrl = "https://flow.qvasoft.net/webhook/api/gateway/register";

        private const string DefaultAppName = "QvaSoft Gateway";
        private const string DefaultPrimaryColor = "#03a9f4";
        private const string DefaultLogoUrl = "~/assets/logo.svg";

        private static readonly SemaphoreSlim _registerLock = new(1, 1);

        private readonly RequestDelegate _next;
        private readonly IOptionsMonitor<AppSetting> _settingsMonitor;
        private readonly IWebHostEnvironment _env;
        private readonly AdminTokenService _adminTokenService;

        public AdminMiddleware(RequestDelegate next, IOptionsMonitor<AppSetting> settingsMonitor, IWebHostEnvironment env, AdminTokenService adminTokenService)
        {
            _next = next;
            _settingsMonitor = settingsMonitor;
            _env = env;
            _adminTokenService = adminTokenService;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (context.Request.Method == "GET" &&
                (path.Equals("/admin", StringComparison.OrdinalIgnoreCase) ||
                 path.Equals("/admin/", StringComparison.OrdinalIgnoreCase) ||
                 path.Equals("/admin/index.html", StringComparison.OrdinalIgnoreCase)))
            {
                var filePath = Path.Combine(_env.WebRootPath, "admin", "index.html");
                if (File.Exists(filePath))
                {
                    var settings = _settingsMonitor.CurrentValue;

                    string appName, primaryColor, logoUrl;
                    appName = settings.AdminApi?.AppName ?? DefaultAppName;
                    primaryColor = settings.AdminApi?.PrimaryColor ?? DefaultPrimaryColor;
                    logoUrl = settings.AdminApi?.LogoUrl ?? DefaultLogoUrl;

                    var registrationCompleted = await IsRegistrationCompleted(context.RequestServices);
                    var html = await File.ReadAllTextAsync(filePath);

                    var configScript = "<script>\n" +
                        "  window.__APP_CONFIG__ = {\n" +
                        $"    appName: {System.Text.Json.JsonSerializer.Serialize(appName)},\n" +
                        $"    primaryColor: {System.Text.Json.JsonSerializer.Serialize(primaryColor)},\n" +
                        $"    logoUrl: {System.Text.Json.JsonSerializer.Serialize(logoUrl)},\n" +
                        $"    registrationCompleted: {(registrationCompleted ? "true" : "false")}\n" +
                        "  };\n" +
                        "</script>\n";

                    html = html.Replace("</head>", configScript + "</head>");
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(html);
                    return;
                }
            }

            if (path.StartsWith(BaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                var settings = _settingsMonitor.CurrentValue;

                if (settings.AdminApi?.Enable != true)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var logger = context.RequestServices.GetService<ILogger<AdminMiddleware>>();
                var loginPath = BaseUrl + "/login";
                var registerPath = BaseUrl + "/register";
                var dataPath = BaseUrl + "/data";

                if (context.Request.Path.Equals(loginPath, StringComparison.OrdinalIgnoreCase) && context.Request.Method == "POST")
                {
                    await HandleLogin(context, logger);
                    return;
                }

                if (context.Request.Path.Equals(registerPath, StringComparison.OrdinalIgnoreCase) && context.Request.Method == "POST")
                {
                    await HandleRegister(context, logger);
                    return;
                }

                if (!await AuthenticateAdmin(context))
                    return;

                if (context.Request.Path.Equals(dataPath, StringComparison.OrdinalIgnoreCase) && context.Request.Method == "GET")
                {
                    await HandleGetData(context, settings);
                    return;
                }

                if (context.Request.Path.Equals(dataPath, StringComparison.OrdinalIgnoreCase) && context.Request.Method == "POST")
                {
                    await HandlePostData(context, logger);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await _next(context);
        }

        #region Body reading

        private static async Task<string?> ReadBodyAsync(HttpContext context)
        {
            var request = context.Request;
            if (request.ContentLength is null or <= 0)
                return null;
            if (request.ContentLength > MaxBodySize)
                return null;

            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }

        private static async Task<bool> RejectIfBodyTooLarge(HttpContext context)
        {
            if (context.Request.ContentLength > MaxBodySize)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "payload_too_large", message = "Request body exceeds maximum allowed size." }));
                return true;
            }
            return false;
        }

        #endregion

        #region Registration

        private async Task HandleRegister(HttpContext context, ILogger? logger)
        {
            context.Response.ContentType = "application/json";

            if (await RejectIfBodyTooLarge(context)) return;

            if (!await _registerLock.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "too_many_requests", message = "Please try again." }));
                return;
            }

            try
            {
                if (await IsRegistrationCompleted(context.RequestServices))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        code_error = "registration_disabled",
                        message = "Registration has been disabled. It is only available during initial setup."
                    }));
                    return;
                }

                var body = await ReadBodyAsync(context);
                if (body == null)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "bad_request", message = "Request body is required." }));
                    return;
                }

                var reg = JsonConvert.DeserializeObject<RegisterRequest>(body);

                if (string.IsNullOrWhiteSpace(reg?.Email))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = "Email is required." }));
                    return;
                }
                if (string.IsNullOrWhiteSpace(reg.FullName))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = "Full name is required." }));
                    return;
                }
                if (string.IsNullOrWhiteSpace(reg.Password))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = "Password is required." }));
                    return;
                }

                using var scope = context.RequestServices.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                var user = new ApplicationUser { UserName = reg.Email, Email = reg.Email, EmailConfirmed = true };

                var createResult = await userManager.CreateAsync(user, reg.Password);
                if (!createResult.Succeeded)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "error_creating_user", message = "Error creating user" }));
                    return;
                }

                var roleName = "Administrator";
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                await userManager.AddToRoleAsync(user, roleName);

                var roles = await userManager.GetRolesAsync(user);
                var token = _adminTokenService.GenerateToken(user.UserName, roles);

                await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                {
                    status = "success",
                    message = "Registration completed successfully.",
                    access_token = token,
                    username = user.UserName
                }));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing registration");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "internal_error", message = "An internal error occurred." }));
            }
            finally
            {
                _registerLock.Release();
            }
        }

        private static async Task<bool> IsRegistrationCompleted(IServiceProvider services)
        {
            try
            {
                using var scope = services.CreateScope();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                return await userManager.Users.AnyAsync();
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Login

        private async Task HandleLogin(HttpContext context, ILogger? logger)
        {
            context.Response.ContentType = "application/json";

            if (await RejectIfBodyTooLarge(context)) return;

            var body = await ReadBodyAsync(context);
            if (body == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "bad_request", message = "Bad Request" }));
                return;
            }

            try
            {
                var login = JsonConvert.DeserializeObject<LoginRequest>(body);

                using var scope = context.RequestServices.CreateScope();
                var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
                var signInManager = scope.ServiceProvider.GetService<SignInManager<ApplicationUser>>();

                if (userManager != null && signInManager != null)
                {
                    var identityUser = await userManager.FindByNameAsync(login.Username);
                    if (identityUser != null)
                    {
                        var result = await signInManager.CheckPasswordSignInAsync(identityUser, login.Password, lockoutOnFailure: true);
                        if (result.Succeeded)
                        {
                            var roles = await userManager.GetRolesAsync(identityUser);
                            var token = _adminTokenService.GenerateToken(identityUser.UserName, roles);
                            await context.Response.WriteAsync(JsonConvert.SerializeObject(new { access_token = token, username = identityUser.UserName }));
                            return;
                        }

                        if (result.IsLockedOut)
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "locked_out", message = "Account is locked" }));
                            return;
                        }
                    }
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "invalid_credentials", message = "Invalid credentials" }));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error processing login request");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "invalid_request", message = "Invalid request" }));
            }
        }

        #endregion

        #region Authentication

        private static async Task<bool> AuthenticateAdmin(HttpContext context)
        {
            var authResult = await context.AuthenticateAsync(AdminScheme);
            if (authResult == null || !authResult.Succeeded || authResult.Principal == null || !authResult.Principal.Identity.IsAuthenticated)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "unauthorized", message = "Unauthorized" }));
                return false;
            }

            context.User = authResult.Principal;

            if (!AdminTokenService.HasRequiredClaims(context.User))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "forbidden", message = "Forbidden" }));
                return false;
            }

            return true;
        }

        #endregion

        #region Data endpoints

        private static async Task HandleGetData(HttpContext context, AppSetting settings)
        {
            string ocelotConfig;
            try
            {
                ocelotConfig = File.Exists("ocelot.json")
                    ? await File.ReadAllTextAsync("ocelot.json")
                    : null;
            }
            catch
            {
                ocelotConfig = null;
            }

            var defaultConfig = new
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

            var ocelotData = ocelotConfig != null
                ? JsonConvert.DeserializeObject(ocelotConfig)
                : defaultConfig;

            using var scope = context.RequestServices.CreateScope();
            var pluginService = scope.ServiceProvider.GetService<IPluginService>();
            var names = pluginService?.GetPluginNames();
            var pluginNames = names?.Select(x => new { value = x.ClassName, label = x.Name });

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                status = "success",
                ocelot = ocelotData,
                settings = new
                {
                    authenticationProviders = settings.AuthenticationProviders?.Select(x => x.Key).ToArray(),
                    delegatingHandlers = pluginNames
                }
            }));
        }

        private static async Task HandlePostData(HttpContext context, ILogger? logger)
        {
            context.Response.ContentType = "application/json";

            if (await RejectIfBodyTooLarge(context)) return;

            var body = await ReadBodyAsync(context);
            if (body == null)
            {
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "success" }));
                return;
            }

            var fileConfiguration = JsonConvert.DeserializeObject<FileConfiguration>(body);
            if (fileConfiguration == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", message = "Invalid JSON configuration." }));
                return;
            }

            using var scope = context.RequestServices.CreateScope();
            var validator = scope.ServiceProvider.GetService<IConfigurationValidator>();
            if (validator != null)
            {
                try
                {
                    var valType = validator.GetType();
                    MethodInfo validateMethod = valType.GetMethod("ValidateAsync", new Type[] { typeof(FileConfiguration) })
                        ?? valType.GetMethod("ValidateAsync", new Type[] { typeof(FileConfiguration), typeof(CancellationToken) });

                    if (validateMethod != null)
                    {
                        object[] args = validateMethod.GetParameters().Length == 1
                            ? new object[] { fileConfiguration }
                            : new object[] { fileConfiguration, CancellationToken.None };

                        var invokeResult = validateMethod.Invoke(validator, args);
                        if (invokeResult is Task task)
                        {
                            await task.ConfigureAwait(false);
                            var result = task.GetType().GetProperty("Result")?.GetValue(task);
                            var isValidProp = result?.GetType().GetProperty("IsValid");
                            if (isValidProp != null && !(bool)isValidProp.GetValue(result))
                            {
                                var errorsProp = result.GetType().GetProperty("Errors");
                                var errorsEnumerable = errorsProp?.GetValue(result) as System.Collections.IEnumerable;
                                var messages = new List<string>();
                                if (errorsEnumerable != null)
                                {
                                    foreach (var e in errorsEnumerable)
                                    {
                                        var msg = e?.GetType().GetProperty("ErrorMessage")?.GetValue(e)?.ToString();
                                        if (!string.IsNullOrEmpty(msg)) messages.Add(msg);
                                    }
                                }

                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                                await context.Response.WriteAsync(JsonConvert.SerializeObject(messages));
                                return;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error validating configuration");
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Invalid configuration");
                    return;
                }
            }

            var sanitizedJson = JsonConvert.SerializeObject(fileConfiguration, Formatting.Indented);
            await File.WriteAllTextAsync("ocelot.json", sanitizedJson);
            await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "success" }));
        }

        #endregion

        #region Helpers

        private class LoginRequest { public string Username { get; set; } public string Password { get; set; } }

        private class RegisterRequest
        {
            public string Email { get; set; }
            public string FullName { get; set; }
            public string ClientType { get; set; }
            public string Password { get; set; }
        }

        #endregion
    }
}
