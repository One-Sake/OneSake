using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace OneSake.Server.Auth;

public static class AuthRateLimits
{
    public const string PolicyName = "auth";
    // Per IP address: enough for typing mistakes, too few for guessing passwords.
    public const int PermitsPerWindow = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}

// Settings in the section "Auth" (appsettings / environment variables Auth__...).
public sealed class AuthSettings
{
    public const string SectionName = "Auth";

    // Lax when frontend and backend are on the same site (onesake.world + api.onesake.world).
    // None only when they are on different sites; then the cookie must be Secure (HTTPS).
    public string CookieSameSite { get; set; } = "Lax";
    public int SessionDays { get; set; } = 30;

    // Folder for the keys that encrypt the login cookie. Without it the keys may be lost on a
    // restart (for example without a home folder), and every player is logged out.
    public string? KeysDirectory { get; set; }
}

public static class AuthSetup
{
    public static IServiceCollection AddOneSakeAuth(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>() ?? new AuthSettings();
        var sameSite = Enum.TryParse<SameSiteMode>(settings.CookieSameSite, ignoreCase: true, out var parsed)
            ? parsed
            : SameSiteMode.Lax;

        if (!string.IsNullOrWhiteSpace(settings.KeysDirectory))
        {
            services.AddDataProtection()
                .SetApplicationName("OneSake")
                .PersistKeysToFileSystem(new DirectoryInfo(settings.KeysDirectory));
        }

        services.AddScoped<IAccountStore, EfAccountStore>();
        services.AddScoped<AccountService>();

        services
            .AddAuthentication(AuthClaims.Scheme)
            .AddCookie(AuthClaims.Scheme, options =>
            {
                options.Cookie.Name = "onesake.auth";
                // JavaScript cannot read the cookie, so an XSS bug cannot steal the session.
                options.Cookie.HttpOnly = true;
                // Over plain http only in Development (localhost / LAN tests).
                options.Cookie.SecurePolicy = environment.IsDevelopment() && sameSite != SameSiteMode.None
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = sameSite;
                options.ExpireTimeSpan = TimeSpan.FromDays(Math.Clamp(settings.SessionDays, 1, 90));
                options.SlidingExpiration = true;

                // An API answers with status codes instead of redirecting to a login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = AuthClaims.ValidateCookieAsync;
            });

        services.AddAuthorization();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Behind a reverse proxy RemoteIpAddress is the proxy; then UseForwardedHeaders is needed
            // (see docs/deployment.md).
            options.AddPolicy(AuthRateLimits.PolicyName, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AuthRateLimits.PermitsPerWindow,
                    Window = AuthRateLimits.Window,
                    QueueLimit = 0
                }));
        });

        return services;
    }
}
