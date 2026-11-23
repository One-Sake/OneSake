using Microsoft.AspNetCore.Identity;
using OneSake.Persistence.Entities;
using OneSake.Server.Match;

namespace OneSake.Server.Auth;

public sealed record AuthResult(AuthError Error, Account? Account = null, string? RecoveryCode = null, DateTime? LockedUntilUtc = null)
{
    public bool Succeeded => Error == AuthError.None;
}

// Register, login, password change and "forgot password" with the recovery code.
public sealed class AccountService
{
    private readonly IAccountStore _store;
    private readonly TimeProvider _time;
    private readonly IPasswordHasher<Account> _hasher;

    // Used when the username does not exist, so a wrong name takes as long as a wrong password
    // (otherwise the response time would tell which usernames exist).
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<Account>().HashPassword(new Account(), AuthRules.CreateSecurityStamp()));

    public AccountService(IAccountStore store, TimeProvider time, IPasswordHasher<Account>? hasher = null)
    {
        _store = store;
        _time = time;
        _hasher = hasher ?? new PasswordHasher<Account>();
    }

    private DateTime UtcNow => _time.GetUtcNow().UtcDateTime;

    // guestId: the old guest ID of this browser. Its match history moves to the account, but only if
    // no other account has taken it yet (the guest ID is not secret, so only the first one wins).
    public async Task<AuthResult> RegisterAsync(string? username, string? password, string? guestId, CancellationToken cancellationToken = default)
    {
        var usernameError = AuthRules.ValidateUsername(username);
        if (usernameError != AuthError.None)
        {
            return new AuthResult(usernameError);
        }

        var passwordError = AuthRules.ValidatePassword(password, username);
        if (passwordError != AuthError.None)
        {
            return new AuthResult(passwordError);
        }

        var normalizedUsername = AuthRules.NormalizeUsername(username);
        if (await _store.FindByNormalizedUsernameAsync(normalizedUsername, cancellationToken) is not null)
        {
            return new AuthResult(AuthError.UsernameTaken);
        }

        var playerId = GuestIdValidator.Normalize(guestId);
        if (playerId is null || await _store.IsPlayerIdTakenAsync(playerId, cancellationToken))
        {
            playerId = Guid.NewGuid().ToString("D");
        }

        var recoveryCode = RecoveryCodes.Generate();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Username = username!.Trim(),
            NormalizedUsername = normalizedUsername,
            SecurityStamp = AuthRules.CreateSecurityStamp(),
            PlayerId = playerId,
            CreatedAtUtc = UtcNow,
            LastLoginUtc = UtcNow
        };
        account.PasswordHash = _hasher.HashPassword(account, password!);
        account.RecoveryCodeHash = _hasher.HashPassword(account, RecoveryCodes.Normalize(recoveryCode));

        if (!await _store.AddAsync(account, cancellationToken))
        {
            return new AuthResult(AuthError.UsernameTaken);
        }

        // The code is returned only this once. The server cannot show it again.
        return new AuthResult(AuthError.None, account, recoveryCode);
    }

    public async Task<AuthResult> LoginAsync(string? username, string? password, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByNormalizedUsernameAsync(AuthRules.NormalizeUsername(username), cancellationToken);
        if (account is null)
        {
            _hasher.VerifyHashedPassword(new Account(), DummyHash.Value, password ?? string.Empty);
            return new AuthResult(AuthError.InvalidCredentials);
        }

        if (IsLockedOut(account))
        {
            return new AuthResult(AuthError.LockedOut, LockedUntilUtc: account.LockoutEndUtc);
        }

        var verification = _hasher.VerifyHashedPassword(account, account.PasswordHash, password ?? string.Empty);
        if (verification == PasswordVerificationResult.Failed)
        {
            return await RegisterFailureAsync(account, cancellationToken);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.PasswordHash = _hasher.HashPassword(account, password!);
        }

        account.FailedLoginCount = 0;
        account.LockoutEndUtc = null;
        account.LastLoginUtc = UtcNow;
        await _store.SaveAsync(account, cancellationToken);
        return new AuthResult(AuthError.None, account);
    }

    // "Forgot password": username + recovery code + new password. Returns a NEW recovery code,
    // the old one is used up. All other sessions are logged out.
    public async Task<AuthResult> ResetPasswordAsync(string? username, string? recoveryCode, string? newPassword, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByNormalizedUsernameAsync(AuthRules.NormalizeUsername(username), cancellationToken);
        var normalizedCode = RecoveryCodes.Normalize(recoveryCode);
        if (account is null)
        {
            _hasher.VerifyHashedPassword(new Account(), DummyHash.Value, normalizedCode);
            return new AuthResult(AuthError.InvalidCredentials);
        }

        if (IsLockedOut(account))
        {
            return new AuthResult(AuthError.LockedOut, LockedUntilUtc: account.LockoutEndUtc);
        }

        // Wrong codes count as failed attempts, so the code cannot be guessed either.
        if (_hasher.VerifyHashedPassword(account, account.RecoveryCodeHash, normalizedCode) == PasswordVerificationResult.Failed)
        {
            return await RegisterFailureAsync(account, cancellationToken);
        }

        var passwordError = AuthRules.ValidatePassword(newPassword, account.Username);
        if (passwordError != AuthError.None)
        {
            // The code was right, so this is not a failed attempt. The code stays valid.
            return new AuthResult(passwordError);
        }

        var newCode = RecoveryCodes.Generate();
        account.PasswordHash = _hasher.HashPassword(account, newPassword!);
        account.RecoveryCodeHash = _hasher.HashPassword(account, RecoveryCodes.Normalize(newCode));
        account.SecurityStamp = AuthRules.CreateSecurityStamp();
        account.FailedLoginCount = 0;
        account.LockoutEndUtc = null;
        account.LastLoginUtc = UtcNow;
        await _store.SaveAsync(account, cancellationToken);
        return new AuthResult(AuthError.None, account, newCode);
    }

    // Needs the current password, so somebody at an unlocked device cannot simply take over the account.
    public async Task<AuthResult> ChangePasswordAsync(Guid accountId, string? currentPassword, string? newPassword, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return new AuthResult(AuthError.InvalidCredentials);
        }

        if (IsLockedOut(account))
        {
            return new AuthResult(AuthError.LockedOut, LockedUntilUtc: account.LockoutEndUtc);
        }

        if (_hasher.VerifyHashedPassword(account, account.PasswordHash, currentPassword ?? string.Empty) == PasswordVerificationResult.Failed)
        {
            return await RegisterFailureAsync(account, cancellationToken);
        }

        var passwordError = AuthRules.ValidatePassword(newPassword, account.Username);
        if (passwordError != AuthError.None)
        {
            return new AuthResult(passwordError);
        }

        account.PasswordHash = _hasher.HashPassword(account, newPassword!);
        account.SecurityStamp = AuthRules.CreateSecurityStamp();
        account.FailedLoginCount = 0;
        await _store.SaveAsync(account, cancellationToken);
        return new AuthResult(AuthError.None, account);
    }

    // "Log out on all devices": every cookie with the old stamp becomes invalid.
    public async Task<Account?> RotateSecurityStampAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        account.SecurityStamp = AuthRules.CreateSecurityStamp();
        await _store.SaveAsync(account, cancellationToken);
        return account;
    }

    public async Task<bool> IsSessionValidAsync(Guid accountId, string? securityStamp, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByIdAsync(accountId, cancellationToken);
        return account is not null
            && !string.IsNullOrEmpty(securityStamp)
            && string.Equals(account.SecurityStamp, securityStamp, StringComparison.Ordinal);
    }

    private bool IsLockedOut(Account account)
    {
        return account.LockoutEndUtc is { } end && end > UtcNow;
    }

    private async Task<AuthResult> RegisterFailureAsync(Account account, CancellationToken cancellationToken)
    {
        account.FailedLoginCount += 1;
        if (account.FailedLoginCount >= AuthRules.MaxFailedAttempts)
        {
            account.FailedLoginCount = 0;
            account.LockoutEndUtc = UtcNow + AuthRules.LockoutDuration;
        }

        await _store.SaveAsync(account, cancellationToken);
        return new AuthResult(AuthError.InvalidCredentials);
    }
}
