using EShooting.Web.Auth;
using EShooting.Web.BackgroundServices;
using EShooting.Web.Hubs;
using EShooting.Web.Realtime;
using EShooting.Web.Services;
using EShooting.Application;
using EShooting.Application.Common.Interfaces;
using EShooting.Infrastructure;
using EShooting.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReceptionAuthOptions>(builder.Configuration.GetSection(ReceptionAuthOptions.SectionName));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.AccessDeniedPath = "/Account/Login";
        options.Events.OnRedirectToLogin = context =>
        {
            if (ShouldReturn401ForChallenge(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            if (context.Request.Path.StartsWithSegments("/admin")
                && !context.Request.Path.StartsWithSegments("/admin/login")
                && !context.Request.Path.StartsWithSegments("/admin/logout"))
            {
                var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
                context.Response.Redirect($"/admin/login?returnUrl={returnUrl}");
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/admin")
                && !context.Request.Path.StartsWithSegments("/admin/login")
                && !context.Request.Path.StartsWithSegments("/admin/logout"))
            {
                var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
                context.Response.Redirect($"/admin/login?returnUrl={returnUrl}");
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Controllers/actions already use [Authorize]/[AllowAnonymous].
    // Avoid a global fallback policy here to prevent accidental login redirect loops.
});

builder.Services.AddMemoryCache();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddSingleton<ScoreDisplayState>();
builder.Services.AddScoped<CachedLaneDashboardService>();

var mvcBuilder = builder.Services.AddControllersWithViews();
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
app.UseAuthorization();

// Guarantee admin login URL never gets auth-challenged (prevents redirect loops).
app.MapGet("/admin/login", (HttpContext context) =>
    Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString("/admin")}"))
    .AllowAnonymous();

// Kiosk-friendly short URLs (TV / tablet bookmarks).
app.MapGet("/admin-panel", () => Results.Redirect("/admin"))
    .AllowAnonymous();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<LaneHub>("/hubs/lane");

app.Run();

static bool ShouldReturn401ForChallenge(HttpRequest request)
{
    var path = request.Path;
    if (path.StartsWithSegments("/dashboard")
        || path.StartsWithSegments("/sessions")
        || path.StartsWithSegments("/subscriptions")
        || path.StartsWithSegments("/athletes")
        || path.StartsWithSegments("/monitor")
        || path.StartsWithSegments("/hubs"))
    {
        return true;
    }

    var accept = request.Headers.Accept.ToString();
    return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
}
