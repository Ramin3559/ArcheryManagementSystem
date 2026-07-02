using EShooting.Web.Auth;
using EShooting.Web.BackgroundServices;
using EShooting.Web.Hubs;
using EShooting.Web.Realtime;
using EShooting.Web.Services;
using EShooting.Application;
using EShooting.Application.Common.Interfaces;
using EShooting.Infrastructure;
using EShooting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("az-Latn-AZ");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("az-AZ");

builder.Services.Configure<ReceptionAuthOptions>(builder.Configuration.GetSection(ReceptionAuthOptions.SectionName));
builder.Services.Configure<AdminAuthOptions>(builder.Configuration.GetSection(AdminAuthOptions.SectionName));
builder.Services.AddSingleton<IAdminCredentialStore, AdminCredentialStore>();

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("Target.EShooting");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, ConfigureReceptionCookie)
    .AddCookie(AdminAuthDefaults.Scheme, ConfigureAdminCookie)
    .AddCookie(PlansetAuthDefaults.Scheme, ConfigurePlansetCookie);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminAuthDefaults.Policy, policy =>
    {
        policy.AddAuthenticationSchemes(AdminAuthDefaults.Scheme);
        policy.RequireRole("Admin");
    })
    .AddPolicy("ReceptionPanel", policy =>
    {
        policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        policy.RequireRole(ReceptionStaffClaims.Role);
    })
    .AddPolicy(PlansetAuthDefaults.Policy, policy =>
    {
        policy.AddAuthenticationSchemes(PlansetAuthDefaults.Scheme);
        policy.RequireRole(PlansetStaffClaims.Role);
    });

builder.Services.AddMemoryCache();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddSingleton<ScoreDisplayState>();
builder.Services.AddScoped<CachedLaneDashboardService>();

var mvcBuilder = builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
if (builder.Environment.IsDevelopment())
{
    // Production/IIS-də runtime compilation 500.30 verə bilər.
    mvcBuilder.AddRazorRuntimeCompilation();
}

builder.Services.AddSignalR();
builder.Services.AddHostedService<SubscriptionAutoStartService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var initializer = scope.ServiceProvider.GetRequiredService<EShootingDbInitializer>();
        await initializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(
            ex,
            "Verilənlər bazası işə salınmadı. ConnectionStrings:DefaultConnection və SQL Server-i yoxlayın.");
        throw;
    }
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<AdminLegacyAuthMigrationMiddleware>();
app.UseMiddleware<ReceptionPermissionRefreshMiddleware>();
app.UseAuthorization();

// Köhnə əlfəcin: /admin/login → Target giriş (returnUrl saxlanılır)
app.MapGet("/admin/login", (HttpContext context) =>
{
    var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
    {
        returnUrl = "/admin";
    }

    return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
}).AllowAnonymous();

// Kiosk-friendly short URLs (TV / tablet bookmarks).
app.MapGet("/admin-panel", () => Results.Redirect("/admin"))
    .AllowAnonymous();

app.MapGet("/zolaq-monitor", () => Results.Redirect("/planset/zolaqlar"))
    .AllowAnonymous();

app.MapGet("/planset", () => Results.Redirect("/planset/zolaqlar"))
    .AllowAnonymous();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<LaneHub>("/hubs/lane");

app.Run();

static void ConfigureReceptionCookie(CookieAuthenticationOptions options)
{
    options.LoginPath = "/resepsiya/giris";
    options.LogoutPath = "/resepsiya/cixis";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/resepsiya/giris";
    options.Events.OnRedirectToLogin = context => HandleReceptionChallengeRedirect(context);
    options.Events.OnRedirectToAccessDenied = context => HandleReceptionChallengeRedirect(context);
}

static void ConfigureAdminCookie(CookieAuthenticationOptions options)
{
    options.Cookie.Name = AdminAuthDefaults.CookieName;
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/Account/Login";
    options.Events.OnRedirectToLogin = context => HandleAdminChallengeRedirect(context);
    options.Events.OnRedirectToAccessDenied = context => HandleAdminChallengeRedirect(context);
}

static void ConfigurePlansetCookie(CookieAuthenticationOptions options)
{
    options.Cookie.Name = PlansetAuthDefaults.CookieName;
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.IsEssential = true;
    options.LoginPath = "/planset/giris";
    options.LogoutPath = "/planset/cixis";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.AccessDeniedPath = "/planset/giris";
    options.Events.OnRedirectToLogin = context => HandlePlansetChallengeRedirect(context);
    options.Events.OnRedirectToAccessDenied = context => HandlePlansetChallengeRedirect(context);
}

static Task HandlePlansetChallengeRedirect(RedirectContext<CookieAuthenticationOptions> context)
{
    if (ShouldReturn401ForChallenge(context.Request))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    context.Response.Redirect("/planset/giris");
    return Task.CompletedTask;
}

static Task HandleAdminChallengeRedirect(RedirectContext<CookieAuthenticationOptions> context)
{
    if (ShouldReturn401ForChallenge(context.Request))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    if (IsReceptionAreaPath(context.Request.Path))
    {
        context.Response.Redirect("/resepsiya/giris");
        return Task.CompletedTask;
    }

    if (IsAdminAreaPath(context.Request.Path))
    {
        var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
        context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

static Task HandleReceptionChallengeRedirect(RedirectContext<CookieAuthenticationOptions> context)
{
    if (ShouldReturn401ForChallenge(context.Request))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    if (IsAdminAreaPath(context.Request.Path))
    {
        var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
        context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        return Task.CompletedTask;
    }

    if (IsReceptionAreaPath(context.Request.Path))
    {
        context.Response.Redirect("/resepsiya/giris");
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

static bool IsAdminAreaPath(PathString path) =>
    path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase)
    && !path.StartsWithSegments("/admin/login", StringComparison.OrdinalIgnoreCase)
    && !path.StartsWithSegments("/admin/logout", StringComparison.OrdinalIgnoreCase);

static bool IsReceptionAreaPath(PathString path) =>
    path.StartsWithSegments("/qeydiyyat", StringComparison.OrdinalIgnoreCase)
    || (path.StartsWithSegments("/resepsiya", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/resepsiya/giris", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/resepsiya/cixis", StringComparison.OrdinalIgnoreCase));

static bool ShouldReturn401ForChallenge(HttpRequest request)
{
    var path = request.Path;
    if (path.StartsWithSegments("/dashboard")
        || path.StartsWithSegments("/sessions")
        || path.StartsWithSegments("/subscriptions")
        || path.StartsWithSegments("/athletes")
        || path.StartsWithSegments("/monitor")
        || path.StartsWithSegments("/hubs")
        || path.StartsWithSegments("/admin/stats")
        || path.StartsWithSegments("/admin/lane-analytics")
        || path.StartsWithSegments("/admin/analytics/data")
        || path.StartsWithSegments("/equipment-sales"))
    {
        return true;
    }

    var accept = request.Headers.Accept.ToString();
    return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
}
