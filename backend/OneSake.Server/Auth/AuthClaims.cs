using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;

namespace OneSake.Server.Auth;

// What the login cookie contains. The cookie is encrypted and signed by ASP.NET Core
// (Data Protection), so the client can neither read nor change it.
public static class AuthClaims
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string PlayerIdType = "onesake:player";
    public const string SecurityStampType = "onesake:stamp";

    public static ClaimsPrincipal CreatePrincipal(Account account)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString("D")),
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(PlayerIdType, account.PlayerId),
                new Claim(SecurityStampType, account.SecurityStamp)
            },
            Scheme);
        return new ClaimsPrincipal(identity);
    }

    public static Guid? GetAccountId(ClaimsPrincipal? user)
    {
        return user?.Identity?.IsAuthenticated == true
            && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;
    }

    public static string? GetPlayerId(ClaimsPrincipal? user)
    {
        return user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(PlayerIdType) : null;
    }

    // Runs for every request with a login cookie: after a password change, a reset or
    // "log out everywhere" the stamp in the database is new, so old cookies are rejected.
    public static async Task ValidateCookieAsync(CookieValidatePrincipalContext context)
    {
        var accountId = GetAccountId(context.Principal);
        var stamp = context.Principal?.FindFirstValue(SecurityStampType);
        var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
        if (accountId is null || !await accounts.IsSessionValidAsync(accountId.Value, stamp, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
        }
    }
}

// Which player ID a request or hub connection plays as.
public static class PlayerIdentity
{
    // Logged in: always the ID of the account (never a value sent by the client).
    // Guest: the guest ID from the browser, but NOT if an account has taken it over. Otherwise
    // anybody who knows an old guest ID could still play as that account.
    public static async Task<string?> ResolveAsync(ClaimsPrincipal? user, string? guestId, Func<string, Task<bool>> isTakenByAccount)
    {
        var accountPlayerId = AuthClaims.GetPlayerId(user);
        if (accountPlayerId is not null)
        {
            return accountPlayerId;
        }

        var normalizedGuestId = GuestIdValidator.Normalize(guestId);
        if (normalizedGuestId is null || await isTakenByAccount(normalizedGuestId))
        {
            return null;
        }

        return normalizedGuestId;
    }
}
