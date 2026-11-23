using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OneSake.Persistence.Entities;
using OneSake.Server.Auth;

namespace OneSake.Server.Controllers;

public sealed class RegisterRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    // Optional: the guest ID of this browser, so the match history moves to the account.
    public string? GuestId { get; set; }
}

public sealed class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class ResetPasswordRequest
{
    public string? Username { get; set; }
    public string? RecoveryCode { get; set; }
    public string? NewPassword { get; set; }
}

public sealed class ChangePasswordRequest
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

public sealed record AccountDto(string Username);

// RecoveryCode is only sent after register and reset, and only this once.
public sealed record AccountWithCodeDto(string Username, string RecoveryCode);

public sealed record AuthErrorDto(string Error, string Message, int? RetryAfterSeconds = null);

// Base Route -> /auth. Login with username + password, session in an HttpOnly cookie.
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AccountService _accounts;
    private readonly TimeProvider _time;

    public AuthController(AccountService accounts, TimeProvider time)
    {
        _accounts = accounts;
        _time = time;
    }

    // POST /auth/register
    [HttpPost("register")]
    [EnableRateLimiting(AuthRateLimits.PolicyName)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.RegisterAsync(request.Username, request.Password, request.GuestId, cancellationToken);
        if (!result.Succeeded)
        {
            return result.Error == AuthError.UsernameTaken ? Conflict(ToError(result)) : BadRequest(ToError(result));
        }

        await SignInAsync(result.Account!);
        return Ok(new AccountWithCodeDto(result.Account!.Username, result.RecoveryCode!));
    }

    // POST /auth/login
    [HttpPost("login")]
    [EnableRateLimiting(AuthRateLimits.PolicyName)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.LoginAsync(request.Username, request.Password, cancellationToken);
        if (!result.Succeeded)
        {
            return FailedLogin(result);
        }

        await SignInAsync(result.Account!);
        return Ok(new AccountDto(result.Account!.Username));
    }

    // POST /auth/reset-password - "Passwort vergessen" with the recovery code.
    [HttpPost("reset-password")]
    [EnableRateLimiting(AuthRateLimits.PolicyName)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _accounts.ResetPasswordAsync(request.Username, request.RecoveryCode, request.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            return FailedLogin(result);
        }

        // The new stamp logs out all other devices; this device gets a fresh cookie.
        await SignInAsync(result.Account!);
        return Ok(new AccountWithCodeDto(result.Account!.Username, result.RecoveryCode!));
    }

    // POST /auth/change-password
    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting(AuthRateLimits.PolicyName)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var accountId = AuthClaims.GetAccountId(User);
        if (accountId is null)
        {
            return Unauthorized();
        }

        var result = await _accounts.ChangePasswordAsync(accountId.Value, request.CurrentPassword, request.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            return FailedLogin(result);
        }

        await SignInAsync(result.Account!);
        return Ok(new AccountDto(result.Account!.Username));
    }

    // GET /auth/me - who is logged in (401 for guests).
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new AccountDto(User.Identity?.Name ?? string.Empty));
    }

    // POST /auth/logout - only this device.
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthClaims.Scheme);
        return NoContent();
    }

    // POST /auth/logout-all - all devices (for example when somebody else knew the password).
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var accountId = AuthClaims.GetAccountId(User);
        if (accountId is not null)
        {
            await _accounts.RotateSecurityStampAsync(accountId.Value, cancellationToken);
        }

        await HttpContext.SignOutAsync(AuthClaims.Scheme);
        return NoContent();
    }

    private Task SignInAsync(Account account)
    {
        return HttpContext.SignInAsync(
            AuthClaims.Scheme,
            AuthClaims.CreatePrincipal(account),
            new AuthenticationProperties { IsPersistent = true });
    }

    private IActionResult FailedLogin(AuthResult result)
    {
        return result.Error switch
        {
            AuthError.LockedOut => StatusCode(StatusCodes.Status429TooManyRequests, ToError(result)),
            AuthError.InvalidCredentials => Unauthorized(ToError(result)),
            _ => BadRequest(ToError(result))
        };
    }

    private AuthErrorDto ToError(AuthResult result)
    {
        int? retryAfter = result.LockedUntilUtc is { } until
            ? Math.Max(1, (int)Math.Ceiling((until - _time.GetUtcNow().UtcDateTime).TotalSeconds))
            : null;
        return new AuthErrorDto(ToCode(result.Error), AuthRules.Describe(result.Error), retryAfter);
    }

    private static string ToCode(AuthError error)
    {
        return error switch
        {
            AuthError.UsernameInvalid => "USERNAME_INVALID",
            AuthError.UsernameTaken => "USERNAME_TAKEN",
            AuthError.PasswordTooShort => "PASSWORD_TOO_SHORT",
            AuthError.PasswordTooLong => "PASSWORD_TOO_LONG",
            AuthError.PasswordTooCommon => "PASSWORD_TOO_COMMON",
            AuthError.PasswordContainsUsername => "PASSWORD_CONTAINS_USERNAME",
            AuthError.LockedOut => "LOCKED_OUT",
            _ => "INVALID_CREDENTIALS"
        };
    }
}
