using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Validator;
using System.Text;
using System.Text.Encodings.Web;
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

        private static readonly System.Text.Json.JsonSerializerOptions SafeJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Default
        };

        private const string DefaultAppName = "Ocelot Admin";
        private const string DefaultPrimaryColor = "#03a9f4";
        private const string DefaultLogoUrl = "/admin/logo.svg";

        /// <summary>Path served by <see cref="InvokeAsync"/> with the bundled logo.</summary>
        private const string DefaultLogoPath = "/admin/logo.svg";

        /// <summary>
        /// Application version, taken from the &lt;Version&gt; property in ApiGateway.csproj.
        /// Reads InformationalVersion first (which may include build metadata such as
        /// "+&lt;commit&gt;"), falling back to the assembly version.
        /// </summary>
        public static readonly string AppVersion = ResolveAppVersion();

        private static string ResolveAppVersion()
        {
            var assembly = typeof(AdminMiddleware).Assembly;

            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // Strip SourceLink build metadata ("1.0.0+abc1234") for display.
                var plusIndex = informational.IndexOf('+');
                return plusIndex > 0 ? informational[..plusIndex] : informational;
            }

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        private static readonly SemaphoreSlim _registerLock = new(1, 1);

        // Simple per-IP sliding-window rate limiter for the login endpoint (brute-force / spray mitigation).
        private const int LoginMaxAttemptsPerWindow = 10;
        private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(1);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Queue<DateTime>> _loginAttempts = new();

        private static bool IsLoginRateLimited(string clientKey, out int retryAfterSeconds)
        {
            retryAfterSeconds = 0;
            var now = DateTime.UtcNow;
            var queue = _loginAttempts.GetOrAdd(clientKey, _ => new Queue<DateTime>());

            lock (queue)
            {
                while (queue.Count > 0 && now - queue.Peek() > LoginWindow)
                    queue.Dequeue();

                if (queue.Count >= LoginMaxAttemptsPerWindow)
                {
                    retryAfterSeconds = (int)Math.Ceiling((LoginWindow - (now - queue.Peek())).TotalSeconds);
                    return true;
                }

                queue.Enqueue(now);
                return false;
            }
        }

        private readonly RequestDelegate _next;
        private readonly IOptionsMonitor<AppSetting> _settingsMonitor;
        private readonly IWebHostEnvironment _env;
        private readonly AdminTokenService _adminTokenService;
        private readonly ILogger<AdminMiddleware> _logger;

        public AdminMiddleware(RequestDelegate next, IOptionsMonitor<AppSetting> settingsMonitor, IWebHostEnvironment env, AdminTokenService adminTokenService, ILogger<AdminMiddleware> logger)
        {
            _next = next;
            _settingsMonitor = settingsMonitor;
            _env = env;
            _adminTokenService = adminTokenService;
            _logger = logger;
        }

        private string OcelotConfigPath => Path.Combine(_env.ContentRootPath, "ocelot.json");

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // Stable URL for the bundled default logo.
            //
            // The front is built with publicPath "/admin", so `logo.svg` from the
            // frontend's public/ folder is published as wwwroot/admin/logo.svg. Serving
            // it through this middleware (instead of relying on the Ocelot pipeline or
            // on a hashed asset name) gives the default a URL that never changes between
            // builds, so DefaultLogoUrl can be a plain absolute path.
            if (context.Request.Method == "GET" &&
                path.Equals(DefaultLogoPath, StringComparison.OrdinalIgnoreCase))
            {
                var logoPath = Path.Combine(_env.WebRootPath, "admin", "logo.svg");
                if (!File.Exists(logoPath))
                {
                    // Not a 404 silently: a missing logo means the frontend build did not
                    // copy public/logo.svg, which is worth knowing about.
                    _logger.LogWarning("Default logo not found at {LogoPath}", logoPath);
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                context.Response.ContentType = "image/svg+xml";
                context.Response.Headers.CacheControl = "public, max-age=86400";
                await context.Response.SendFileAsync(logoPath);
                return;
            }

            if (context.Request.Method == "GET" &&
                (path.Equals("/admin", StringComparison.OrdinalIgnoreCase) ||
                 path.Equals("/admin/", StringComparison.OrdinalIgnoreCase) ||
                 path.Equals("/admin/index.html", StringComparison.OrdinalIgnoreCase)))
            {
                var filePath = Path.Combine(_env.WebRootPath, "admin", "index.html");
                if (File.Exists(filePath))
                {
                    var settings = _settingsMonitor.CurrentValue;

                    // `??` is not enough here: appsettings.json ships these keys as
                    // empty strings (not null), so the fallback never kicked in and the
                    // front received "logoUrl": "". q-img then requested an empty URL
                    // (the page itself) and showed nothing. Treat blank as "not set".
                    string appName, primaryColor, logoUrl;
                    appName = string.IsNullOrWhiteSpace(settings.AdminApi?.AppName)
                        ? DefaultAppName
                        : settings.AdminApi!.AppName;
                    primaryColor = string.IsNullOrWhiteSpace(settings.AdminApi?.PrimaryColor)
                        ? DefaultPrimaryColor
                        : settings.AdminApi!.PrimaryColor;
                    logoUrl = string.IsNullOrWhiteSpace(settings.AdminApi?.LogoUrl)
                        ? DefaultLogoUrl
                        : settings.AdminApi!.LogoUrl;

                    bool registrationCompleted;
                    try
                    {
                        registrationCompleted = await IsRegistrationCompleted(context.RequestServices);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not determine registration state; assuming completed");
                        registrationCompleted = true;
                    }
                    var html = await File.ReadAllTextAsync(filePath);

                    var configScript = "<script>\n" +
                        "  window.__APP_CONFIG__ = {\n" +
                        $"    appName: {System.Text.Json.JsonSerializer.Serialize(appName, SafeJsonOptions)},\n" +
                        $"    primaryColor: {System.Text.Json.JsonSerializer.Serialize(primaryColor, SafeJsonOptions)},\n" +
                        $"    logoUrl: {System.Text.Json.JsonSerializer.Serialize(logoUrl, SafeJsonOptions)},\n" +
                        $"    version: {System.Text.Json.JsonSerializer.Serialize(AppVersion, SafeJsonOptions)},\n" +
                        $"    registrationCompleted: {(registrationCompleted ? "true" : "false")},\n" +
                        $"    pluginUploadEnabled: {(PluginUploadEnabled(settings) ? "true" : "false")}\n" +
                        "  };\n" +
                        "</script>\n";
                    html = html.Replace("</head>", configScript + "</head>");
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.Headers["X-Frame-Options"] = "DENY";
                    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
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
                var pluginsPath = BaseUrl + "/plugins";

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

                if (context.Request.Path.Equals(pluginsPath, StringComparison.OrdinalIgnoreCase))
                {
                    // AllowPluginUpload=false disables the whole plugin API, reads included:
                    // the panel exposes nothing about plugins in that mode, and "write only
                    // is forbidden" is a weaker promise than the operator configured.
                    // Hiding the menu in the UI is not a control: the endpoint must refuse
                    // the request on its own.
                    if (!PluginUploadEnabled(settings))
                    {
                        WritePluginUploadDisabled(context);
                        return;
                    }

                    if (context.Request.Method == "GET")
                        await HandleGetPlugins(context, settings);
                    else if (context.Request.Method == "POST")
                        await HandleUploadPlugin(context, logger);
                    else if (context.Request.Method == "DELETE")
                        await HandleDeletePlugin(context, logger);
                    else
                        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;

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

            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, context.RequestAborted);
            if (ms.Length == 0)
                return null;

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static async Task<bool> RejectIfBodyTooLarge(HttpContext context)
        {
            // null ContentLength (e.g. chunked) cannot be size-validated up-front; reject to be safe.
            if (context.Request.ContentLength is null || context.Request.ContentLength > MaxBodySize)
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
                bool alreadyCompleted;
                try
                {
                    alreadyCompleted = await IsRegistrationCompleted(context.RequestServices);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not determine registration state; rejecting registration");
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "internal_error", message = "Registration is not available right now." }));
                    return;
                }

                if (alreadyCompleted)
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

                if (string.IsNullOrWhiteSpace(reg?.Username))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = "Username is required." }));
                    return;
                }
                // Email is optional: it is only stored as contact information and is
                // never used to authenticate (the old e-mail activation flow is gone).
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

                var user = new ApplicationUser
                {
                    UserName = reg.Username,
                    Email = string.IsNullOrWhiteSpace(reg.Email) ? null : reg.Email.Trim(),
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(user, reg.Password);
                if (!createResult.Succeeded)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    // Surface the actual Identity message: "Passwords must have at least
                    // one non alphanumeric character." is far more useful than a generic
                    // "Error creating user".
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        code_error = "error_creating_user",
                        message = string.Join(" ", createResult.Errors.Select(e => e.Description))
                    }));
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
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            // UserManager.Users.AnyAsync() will throw if the database is unavailable.
            // We deliberately do NOT swallow the exception here: a failure must NOT be
            // interpreted as "no users yet" (which would re-open public registration).
            return await userManager.Users.AnyAsync();
        }

        #endregion

        #region Login

        private async Task HandleLogin(HttpContext context, ILogger? logger)
        {
            context.Response.ContentType = "application/json";

            if (await RejectIfBodyTooLarge(context)) return;

            var clientKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (IsLoginRateLimited(clientKey, out var retryAfter))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "too_many_requests", message = "Too many login attempts. Please try again later." }));
                return;
            }

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

                if (userManager != null && signInManager != null && !string.IsNullOrWhiteSpace(login?.Username) && login.Password != null)
                {
                    var identityUser = await userManager.FindByNameAsync(login.Username);
                    if (identityUser != null)
                    {
                        var result = await signInManager.CheckPasswordSignInAsync(identityUser, login.Password, lockoutOnFailure: true);
                        if (result.Succeeded)
                        {
                            var roles = await userManager.GetRolesAsync(identityUser);
                            var token = _adminTokenService.GenerateToken(identityUser.UserName ?? string.Empty, roles);
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
            if (authResult == null || !authResult.Succeeded || authResult.Principal == null || authResult.Principal.Identity?.IsAuthenticated != true)
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

        private async Task HandleGetData(HttpContext context, AppSetting settings)
        {
            string? ocelotConfig;
            try
            {
                ocelotConfig = File.Exists(OcelotConfigPath)
                    ? await File.ReadAllTextAsync(OcelotConfigPath)
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading ocelot.json");
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

        private async Task HandlePostData(HttpContext context, ILogger? logger)
        {
            context.Response.ContentType = "application/json";

            if (await RejectIfBodyTooLarge(context)) return;

            var body = await ReadBodyAsync(context);
            if (body == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "bad_request", message = "Request body is required." }));
                return;
            }

            FileConfiguration? fileConfiguration;
            try
            {
                fileConfiguration = JsonConvert.DeserializeObject<FileConfiguration>(body);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Invalid JSON payload received for ocelot.json");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", message = "Invalid JSON configuration." }));
                return;
            }

            if (fileConfiguration == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", message = "Invalid JSON configuration." }));
                return;
            }

            // Basic guard: prevent dangerous downstream schemes in gateway configuration.
            var schemeError = ValidateDownstreamSchemes(fileConfiguration);
            if (schemeError != null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = schemeError }));
                return;
            }

            using var scope = context.RequestServices.CreateScope();
            var validator = scope.ServiceProvider.GetService<IConfigurationValidator>();
            if (validator != null)
            {
                try
                {
                    var valType = validator.GetType();
                    MethodInfo? validateMethod = valType.GetMethod("ValidateAsync", new Type[] { typeof(FileConfiguration) })
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
                            if (result == null)
                            {
                                // No result object to inspect; treat as valid (Ocelot returned void/unknown shape).
                            }
                            else
                            {
                                var isValidProp = result.GetType().GetProperty("IsValid");
                                var isValid = isValidProp?.GetValue(result) as bool?;
                                if (isValid == false)
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

            // Atomic write: write to a temp file then replace, so a crash cannot leave ocelot.json corrupted.
            var tempPath = OcelotConfigPath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tempPath, sanitizedJson);
                File.Move(tempPath, OcelotConfigPath, overwrite: true);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error writing ocelot.json");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore cleanup failure */ }
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "internal_error", message = "Could not persist configuration." }));
                return;
            }

            await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "success" }));
        }

        #endregion

        #region Validation helpers

        private static readonly HashSet<string> AllowedDownstreamSchemes =
            new(StringComparer.OrdinalIgnoreCase) { "http", "https" };

        private static string? ValidateDownstreamSchemes(FileConfiguration config)
        {
            static string? CheckRoute(string? scheme, string upstream)
            {
                if (string.IsNullOrWhiteSpace(scheme))
                    return null;

                if (!AllowedDownstreamSchemes.Contains(scheme))
                    return $"Downstream scheme '{scheme}' is not allowed for route '{upstream}'. Only http/https are permitted.";

                return null;
            }

            if (config.Routes != null)
            {
                foreach (var r in config.Routes)
                {
                    var err = CheckRoute(r.DownstreamScheme, r.UpstreamPathTemplate ?? "(unnamed)");
                    if (err != null) return err;

                    if (r.DangerousAcceptAnyServerCertificateValidator)
                        return $"Route '{r.UpstreamPathTemplate}' disables TLS certificate validation, which is not allowed.";
                }
            }

            if (config.Aggregates != null)
            {
                foreach (var a in config.Aggregates)
                {
                    if (a.RouteKeys == null) continue;
                    foreach (var routeKey in a.RouteKeys)
                    {
                        var match = config.Routes?.FirstOrDefault(r => r.Key == routeKey);
                        if (match == null) continue;
                        var err = CheckRoute(match.DownstreamScheme, match.UpstreamPathTemplate ?? "(aggregate)");
                        if (err != null) return err;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Plugins

        /// <summary>
        /// Lists the plugins on disk, their load state and the files present in the plugins
        /// directory (configured through Docker or copied by hand).
        /// </summary>
        private async Task HandleGetPlugins(HttpContext context, AppSetting settings)
        {
            using var scope = context.RequestServices.CreateScope();
            var pluginService = scope.ServiceProvider.GetRequiredService<IPluginService>();

            var files = new List<object>();

            // The absolute path is host internals (it reveals the install directory and user
            // name) and the admin panel never displays it, so it stays out of the response.
            var directory = pluginService.PluginsPath;

            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(file);
                    var info = new FileInfo(file);
                    var plugins = pluginService.GetPluginNames()
                        .Where(p => string.Equals(p.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    files.Add(new
                    {
                        fileName,
                        sizeBytes = info.Length,
                        lastWriteUtc = info.LastWriteTimeUtc,
                        // False for files placed in the directory after startup: they load on
                        // the next restart.
                        loaded = plugins.Count > 0,
                        // When the handler is loaded but its file is gone, the plugin was
                        // deleted from the panel and stays active until the next restart.
                        onDisk = true,
                        plugins = plugins.Select(p => new
                        {
                            name = p.Name,
                            className = p.ClassName,
                            version = p.Version,
                            isGlobal = p.IsGlobal
                        })
                    });
                }
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                status = "success",
                uploadEnabled = PluginUploadEnabled(settings),
                writable = IsPluginsDirectoryWritable(directory),
                plugins = pluginService.GetPluginNames().Select(p => new
                {
                    name = p.Name,
                    className = p.ClassName,
                    version = p.Version,
                    fileName = p.FileName,
                    isGlobal = p.IsGlobal
                }),
                files
            }));
        }

        /// <summary>
        /// Uploads a plugin assembly. The file is validated in a staging folder first and only
        /// moved into the plugins directory when the host would actually load it, so a typo
        /// cannot break the next startup. The handler is registered immediately; the plugin
        /// needs a restart to run <see cref="IPlugin.InitializeAsync"/>.
        /// </summary>
        private async Task HandleUploadPlugin(HttpContext context, ILogger? logger)
        {
            context.Response.ContentType = "application/json";

            if (await RejectIfBodyTooLarge(context)) return;

            if (context.Request.Form.Files.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = "A plugin file (.dll) is required." }));
                return;
            }

            using var scope = context.RequestServices.CreateScope();
            var pluginService = scope.ServiceProvider.GetRequiredService<IPluginService>();

            // Stage inside the plugins directory (same volume as the destination, so the final
            // move is cheap). The host only enumerates *.dll at startup, and the staged copy is
            // renamed to *.staged.dll, so it is never mistaken for a plugin — even after a crash.
            var pluginsDirectory = pluginService.PluginsPath;
            Directory.CreateDirectory(pluginsDirectory);

            // A previous attempt may have left a staging folder behind (e.g. the process was
            // restarted mid-upload); those files are never loaded.
            foreach (var stale in Directory.GetDirectories(pluginsDirectory, ".upload-*"))
            {
                try { Directory.Delete(stale, recursive: true); }
                catch (Exception ex) { logger?.LogDebug(ex, "Could not remove stale staging folder {Path}", stale); }
            }

            var stagingPath = Path.Combine(pluginsDirectory, $".upload-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingPath);

            try
            {
                var uploaded = context.Request.Form.Files[0];

                if (!string.Equals(Path.GetExtension(uploaded.FileName), ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = $"'{uploaded.FileName}' is not a .dll file." }));
                    return;
                }

                var fileName = GetSafeFileName(uploaded.FileName);
                if (fileName == null)
                {
                    var isHostAssembly = new[] { "ApiGateway", "ApiGateway.Contracts" }
                        .Contains(Path.GetFileNameWithoutExtension(uploaded.FileName), StringComparer.OrdinalIgnoreCase);

                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        code_error = "validation_error",
                        message = isHostAssembly
                            ? "A plugin must never ship the host or the host contract: reference 'ocelot-admin-plugin' with Private=false and ExcludeAssets=runtime instead."
                            : $"The file name '{uploaded.FileName}' is not allowed."
                    }));
                    return;
                }

                var stagedAssemblyPath = Path.Combine(stagingPath, Path.GetFileNameWithoutExtension(fileName) + ".staged.dll");
                await using (var target = File.Create(stagedAssemblyPath))
                {
                    await uploaded.CopyToAsync(target);
                }

                var validation = pluginService.ValidatePluginAssembly(
                    stagedAssemblyPath,
                    context.RequestServices.GetRequiredService<IConfiguration>(),
                    pluginService.GetPluginNames().Select(p => p.Name).ToList(),
                    [fileName]);

                if (!validation.IsValid)
                {
                    logger?.LogWarning("Rejected plugin upload {FileName}: {Errors}", fileName, string.Join(" | ", validation.Errors));
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        code_error = "validation_error",
                        message = string.Join(" ", validation.Errors),
                        errors = validation.Errors,
                        warnings = validation.Warnings
                    }));
                    return;
                }

                var destination = Path.Combine(pluginsDirectory, fileName);
                var isNewFile = !File.Exists(destination);
                // A .dll extension would make the CLR resolve this copy as an assembly in the
                // plugins folder, which blocks deleting it while the process runs.
                var backup = Path.Combine(stagingPath, fileName + ".previous.bak");

                if (!isNewFile)
                    File.Move(destination, backup);

                try
                {
                    File.Move(stagedAssemblyPath, destination);
                }
                catch
                {
                    // Put the previous version back: a failed upload must not delete a working plugin.
                    if (File.Exists(backup))
                        File.Move(backup, destination);

                    throw;
                }

                // The host queries PluginMetadata by plugin name; create the section (empty) so
                // the operator only has to fill in values instead of discovering the key.
                var sectionsAdded = await EnsurePluginMetadataSectionsAsync(validation.Plugins.Select(p => p.Name));

                logger?.LogInformation(
                    "Plugin file {FileName} uploaded ({Plugins}). A restart is required to load it.",
                    fileName, string.Join(", ", validation.Plugins.Select(p => p.Name)));

                context.Response.StatusCode = StatusCodes.Status201Created;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                {
                    status = "success",
                    fileName,
                    replaced = !isNewFile,
                    needsRestart = true,
                    metadataSectionsAdded = sectionsAdded,
                    plugins = validation.Plugins.Select(p => new
                    {
                        name = p.Name,
                        className = p.ClassName,
                        version = p.Version,
                        isGlobal = p.IsGlobal
                    }),
                    warnings = validation.Warnings
                }));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error uploading plugin");
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "internal_error", message = "The plugin could not be stored." }));
            }
            finally
            {
                try { Directory.Delete(stagingPath, recursive: true); }
                catch (Exception ex) { logger?.LogDebug(ex, "Could not clean staging directory {Path}", stagingPath); }
            }
        }

        /// <summary>
        /// Deletes a plugin file. The plugin stays registered until the next restart, because
        /// unloading an assembly from a running process is not supported.
        /// </summary>
        private async Task HandleDeletePlugin(HttpContext context, ILogger? logger)
        {
            context.Response.ContentType = "application/json";

            var fileName = context.Request.Query["fileName"].ToString();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = "Query parameter 'fileName' is required." }));
                return;
            }

            var safeName = GetSafeFileName(fileName);
            if (safeName == null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "validation_error", message = "The file name contains characters that are not allowed." }));
                return;
            }

            using var scope = context.RequestServices.CreateScope();
            var pluginService = scope.ServiceProvider.GetRequiredService<IPluginService>();
            var path = Path.Combine(pluginService.PluginsPath, safeName);

            if (!File.Exists(path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new { status = "error", code_error = "not_found", message = $"'{safeName}' does not exist in the plugins directory." }));
                return;
            }

            try
            {
                Directory.CreateDirectory(pluginService.PluginsPath);
                File.Delete(path);

                // On Windows the OS keeps the deleted entry visible (and every further
                // delete fails with "access denied") until the process that had the image
                // mapped closes its handle. Run ShutdownAsync now so the gateway behaves
                // as if the plugin had been removed, and say clearly that a restart is
                // what truly unloads the code.
                pluginService.MarkAllUninitialized();
            }
            catch (Exception ex)
            {
                // A directory mapped read-only (Docker bind mount with ":ro") reports the
                // same "access denied" as a locked file, so name both causes.
                logger?.LogError(ex, "Error deleting plugin file {FileName}", safeName);
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                {
                    status = "error",
                    code_error = "delete_failed",
                    message = $"{safeName} could not be deleted. The file is loaded by the running gateway " +
                              "or the plugins directory is read-only (check the bind mount). Stop the service, " +
                              "delete the file, and start it again."
                }));
                return;
            }

            logger?.LogInformation("Plugin file {FileName} deleted from the admin panel", safeName);

            await context.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                status = "success",
                fileName = safeName,
                needsRestart = true
            }));
        }

        /// <summary>
        /// Adds empty <c>PluginMetadata:&lt;name&gt;</c> sections to the runtime configuration
        /// files so the new plugin shows up where its settings are expected.
        /// Returns the names that did not have a section yet.
        /// </summary>
        private async Task<List<string>> EnsurePluginMetadataSectionsAsync(IEnumerable<string> pluginNames)
        {
            var added = new List<string>();
            var candidates = new[] { Path.Combine(_env.ContentRootPath, "appsettings.json") }
                .Concat(Directory.GetFiles(_env.ContentRootPath, "appsettings.*.json"))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in candidates)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var root = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                    if (root == null)
                        continue;

                    if (root[PluginService.MetadataSectionName] is not Newtonsoft.Json.Linq.JObject metadata)
                    {
                        metadata = new Newtonsoft.Json.Linq.JObject();
                        root[PluginService.MetadataSectionName] = metadata;
                    }

                    var changed = false;
                    foreach (var name in pluginNames)
                    {
                        if (metadata.Property(name) == null)
                        {
                            metadata[name] = new Newtonsoft.Json.Linq.JObject();
                            added.Add(name);
                            changed = true;
                        }
                    }

                    if (changed)
                        await File.WriteAllTextAsync(file, root.ToString(Formatting.Indented));
                }
                catch (Exception ex)
                {
                    // Runtime configuration is best effort: the plugin still loads without its
                    // section, and InitializeAsync reports the missing values itself.
                    _logger.LogWarning(ex, "Could not add the plugin metadata section to {File}", file);
                }
            }

            return added.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

        private static bool PluginUploadEnabled(AppSetting settings) =>
            settings.AdminApi?.AllowPluginUpload != false;

        private static void WritePluginUploadDisabled(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            _ = context.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                status = "error",
                code_error = "plugin_upload_disabled",
                message = "Plugin management is disabled by configuration (AppSetting:AdminApi:AllowPluginUpload = false)."
            }));
        }

        /// <summary>Rejects path traversal and wildcard characters in an uploaded file name.</summary>
        private static string? GetSafeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // A browser sends only the file name, but curl sends the whole path ("-F file=@C:\...\x.dll").
            var name = fileName.Trim().Replace('\\', '/');
            name = Path.GetFileName(name);
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;

            // Never let an upload overwrite the host contract or the gateway itself.
            if (new[] { "ApiGateway", "ApiGateway.Contracts" }.Contains(Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase))
                return null;

            return name;
        }

        private static bool IsPluginsDirectoryWritable(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
                using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Helpers

        private class LoginRequest
        {
            public string? Username { get; set; }
            public string? Password { get; set; }
        }

        private class RegisterRequest
        {
            /// <summary>Login name, free-form ("admin", "ops-team", ...). Required.</summary>
            public string? Username { get; set; }

            /// <summary>Optional contact address. Never used to log in.</summary>
            public string? Email { get; set; }

            public string? FullName { get; set; }
            public string? Password { get; set; }
        }

        #endregion
    }
}
