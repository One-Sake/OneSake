using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OneSake.Persistence.Entities;
using OneSake.Server.Auth;

namespace OneSake.Tests.Auth;

public class AccountServiceTests
{
    private const string Password = "Gomu-Gomu-no-Pistol";
    private const string GuestId = "6f1c1b4e-2a3d-4c5e-8f90-1a2b3c4d5e6f";

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class MemoryAccountStore : IAccountStore
    {
        public List<Account> Accounts { get; } = [];

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.FirstOrDefault(account => account.Id == id));

        public Task<Account?> FindByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.FirstOrDefault(account => account.NormalizedUsername == normalizedUsername));

        public Task<bool> IsPlayerIdTakenAsync(string playerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.Any(account => account.PlayerId == playerId));

        public Task<bool> AddAsync(Account account, CancellationToken cancellationToken = default)
        {
            if (Accounts.Any(other => other.NormalizedUsername == account.NormalizedUsername || other.PlayerId == account.PlayerId))
            {
                return Task.FromResult(false);
            }

            Accounts.Add(account);
            return Task.FromResult(true);
        }

        public Task SaveAsync(Account account, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private readonly MemoryAccountStore _store = new();
    private readonly ManualTime _time = new();
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        // Fewer PBKDF2 rounds only to keep the tests fast; production uses the default.
        var hasher = new PasswordHasher<Account>(Options.Create(new PasswordHasherOptions { IterationCount = 1000 }));
        _service = new AccountService(_store, _time, hasher);
    }

    private async Task<AuthResult> RegisterRuffyAsync(string? guestId = null)
    {
        var result = await _service.RegisterAsync("Ruffy", Password, guestId);
        Assert.True(result.Succeeded, result.Error.ToString());
        return result;
    }

    [Fact]
    public async Task Register_StoresOnlyHashes_AndReturnsARecoveryCode()
    {
        var result = await RegisterRuffyAsync();
        var account = Assert.Single(_store.Accounts);

        Assert.Equal("Ruffy", account.Username);
        Assert.Equal("RUFFY", account.NormalizedUsername);
        Assert.DoesNotContain(Password, account.PasswordHash);
        Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", result.RecoveryCode!);
        Assert.DoesNotContain(RecoveryCodes.Normalize(result.RecoveryCode), account.RecoveryCodeHash);
        Assert.False(string.IsNullOrEmpty(account.SecurityStamp));
    }

    [Fact]
    public async Task Register_RejectsATakenUsername_IgnoringCase()
    {
        await RegisterRuffyAsync();

        var second = await _service.RegisterAsync("rUFFY", "Another-Long-Password", null);

        Assert.Equal(AuthError.UsernameTaken, second.Error);
        Assert.Single(_store.Accounts);
    }

    [Theory]
    [InlineData("ab", AuthError.UsernameInvalid)]
    [InlineData("Ruffy mit Leerzeichen", AuthError.UsernameInvalid)]
    [InlineData("Ruffy<script>", AuthError.UsernameInvalid)]
    public async Task Register_RejectsInvalidUsernames(string username, AuthError expected)
    {
        Assert.Equal(expected, (await _service.RegisterAsync(username, Password, null)).Error);
    }

    [Theory]
    [InlineData("kurz", AuthError.PasswordTooShort)]
    [InlineData("password123", AuthError.PasswordTooCommon)]
    [InlineData("aaaaaaaaaaaa", AuthError.PasswordTooCommon)]
    [InlineData("ichbinruffy2026", AuthError.PasswordContainsUsername)]
    public async Task Register_RejectsWeakPasswords(string password, AuthError expected)
    {
        Assert.Equal(expected, (await _service.RegisterAsync("Ruffy", password, null)).Error);
        Assert.Empty(_store.Accounts);
    }

    [Fact]
    public async Task Register_TakesOverTheGuestId_OnlyForTheFirstAccount()
    {
        var first = await RegisterRuffyAsync(GuestId);
        var second = await _service.RegisterAsync("Zorro", "Santoryu-Onigiri", GuestId);

        Assert.Equal(GuestId, first.Account!.PlayerId);
        Assert.True(second.Succeeded);
        Assert.NotEqual(GuestId, second.Account!.PlayerId);
    }

    [Fact]
    public async Task Login_WorksWithTheRightPassword_AndAnyCaseOfTheUsername()
    {
        await RegisterRuffyAsync();

        var result = await _service.LoginAsync("ruffy", Password);

        Assert.True(result.Succeeded);
        Assert.Equal("Ruffy", result.Account!.Username);
    }

    [Fact]
    public async Task Login_GivesTheSameError_ForAWrongPasswordAndAnUnknownUser()
    {
        await RegisterRuffyAsync();

        Assert.Equal(AuthError.InvalidCredentials, (await _service.LoginAsync("Ruffy", "falsches-passwort")).Error);
        Assert.Equal(AuthError.InvalidCredentials, (await _service.LoginAsync("Niemand", Password)).Error);
    }

    [Fact]
    public async Task Login_LocksTheAccount_AfterFiveFailures_EvenForTheRightPassword()
    {
        await RegisterRuffyAsync();
        for (var attempt = 0; attempt < AuthRules.MaxFailedAttempts; attempt += 1)
        {
            await _service.LoginAsync("Ruffy", "falsches-passwort");
        }

        var locked = await _service.LoginAsync("Ruffy", Password);
        Assert.Equal(AuthError.LockedOut, locked.Error);
        Assert.Equal(_time.Now.UtcDateTime + AuthRules.LockoutDuration, locked.LockedUntilUtc);

        _time.Now += AuthRules.LockoutDuration + TimeSpan.FromSeconds(1);
        Assert.True((await _service.LoginAsync("Ruffy", Password)).Succeeded);
    }

    [Fact]
    public async Task Login_ResetsTheFailureCount_AfterASuccess()
    {
        await RegisterRuffyAsync();
        for (var attempt = 0; attempt < AuthRules.MaxFailedAttempts - 1; attempt += 1)
        {
            await _service.LoginAsync("Ruffy", "falsches-passwort");
        }

        Assert.True((await _service.LoginAsync("Ruffy", Password)).Succeeded);
        await _service.LoginAsync("Ruffy", "falsches-passwort");

        Assert.True((await _service.LoginAsync("Ruffy", Password)).Succeeded);
    }

    [Fact]
    public async Task Reset_WithTheRecoveryCode_SetsANewPassword_AndLogsOutOtherSessions()
    {
        var registered = await RegisterRuffyAsync();
        var oldStamp = registered.Account!.SecurityStamp;

        // Lower case and without dashes also works.
        var reset = await _service.ResetPasswordAsync("Ruffy", registered.RecoveryCode!.Replace("-", "").ToLowerInvariant(), "Neues-Strohhut-Passwort");

        Assert.True(reset.Succeeded, reset.Error.ToString());
        Assert.NotEqual(registered.RecoveryCode, reset.RecoveryCode);
        Assert.False(await _service.IsSessionValidAsync(registered.Account.Id, oldStamp));
        Assert.Equal(AuthError.InvalidCredentials, (await _service.LoginAsync("Ruffy", Password)).Error);
        Assert.True((await _service.LoginAsync("Ruffy", "Neues-Strohhut-Passwort")).Succeeded);
    }

    [Fact]
    public async Task Reset_TheRecoveryCode_WorksOnlyOnce()
    {
        var registered = await RegisterRuffyAsync();
        await _service.ResetPasswordAsync("Ruffy", registered.RecoveryCode, "Neues-Strohhut-Passwort");

        var second = await _service.ResetPasswordAsync("Ruffy", registered.RecoveryCode, "Noch-ein-Passwort-123");

        Assert.Equal(AuthError.InvalidCredentials, second.Error);
    }

    [Fact]
    public async Task Reset_WrongCodes_CountAsFailedAttempts()
    {
        var registered = await RegisterRuffyAsync();
        for (var attempt = 0; attempt < AuthRules.MaxFailedAttempts; attempt += 1)
        {
            await _service.ResetPasswordAsync("Ruffy", "AAAA-BBBB-CCCC-DDDD", "Neues-Strohhut-Passwort");
        }

        // Locked: even the right code does not work now.
        var locked = await _service.ResetPasswordAsync("Ruffy", registered.RecoveryCode, "Neues-Strohhut-Passwort");
        Assert.Equal(AuthError.LockedOut, locked.Error);
    }

    [Fact]
    public async Task Reset_AWeakNewPassword_KeepsTheCodeValid()
    {
        var registered = await RegisterRuffyAsync();

        Assert.Equal(AuthError.PasswordTooShort, (await _service.ResetPasswordAsync("Ruffy", registered.RecoveryCode, "kurz")).Error);
        Assert.True((await _service.ResetPasswordAsync("Ruffy", registered.RecoveryCode, "Neues-Strohhut-Passwort")).Succeeded);
    }

    [Fact]
    public async Task ChangePassword_NeedsTheCurrentPassword_AndRotatesTheStamp()
    {
        var registered = await RegisterRuffyAsync();
        var account = registered.Account!;
        var oldStamp = account.SecurityStamp;

        Assert.Equal(AuthError.InvalidCredentials, (await _service.ChangePasswordAsync(account.Id, "falsch-falsch", "Neues-Strohhut-Passwort")).Error);
        Assert.True((await _service.ChangePasswordAsync(account.Id, Password, "Neues-Strohhut-Passwort")).Succeeded);

        Assert.False(await _service.IsSessionValidAsync(account.Id, oldStamp));
        Assert.True(await _service.IsSessionValidAsync(account.Id, account.SecurityStamp));
    }

    [Fact]
    public async Task LogoutEverywhere_MakesOldSessionsInvalid()
    {
        var account = (await RegisterRuffyAsync()).Account!;
        var oldStamp = account.SecurityStamp;

        await _service.RotateSecurityStampAsync(account.Id);

        Assert.False(await _service.IsSessionValidAsync(account.Id, oldStamp));
        Assert.False(await _service.IsSessionValidAsync(Guid.NewGuid(), account.SecurityStamp));
    }
}

public class PlayerIdentityTests
{
    private const string GuestId = "6f1c1b4e-2a3d-4c5e-8f90-1a2b3c4d5e6f";
    private const string OtherGuestId = "0b8d6a4c-1e2f-4a3b-9c8d-7e6f5a4b3c2d";

    private static ClaimsPrincipal LoggedIn(string playerId)
    {
        return AuthClaims.CreatePrincipal(new Account
        {
            Id = Guid.NewGuid(),
            Username = "Ruffy",
            PlayerId = playerId,
            SecurityStamp = "STAMP"
        });
    }

    private static Task<bool> NothingTaken(string _) => Task.FromResult(false);

    [Fact]
    public async Task LoggedIn_UsesTheAccount_AndIgnoresTheSentGuestId()
    {
        var playerId = await PlayerIdentity.ResolveAsync(LoggedIn(GuestId), OtherGuestId, NothingTaken);

        Assert.Equal(GuestId, playerId);
    }

    [Fact]
    public async Task Guest_GetsTheNormalizedGuestId()
    {
        var playerId = await PlayerIdentity.ResolveAsync(new ClaimsPrincipal(), GuestId.ToUpperInvariant(), NothingTaken);

        Assert.Equal(GuestId, playerId);
    }

    [Fact]
    public async Task Guest_CannotUseAGuestIdThatAnAccountTookOver()
    {
        var playerId = await PlayerIdentity.ResolveAsync(new ClaimsPrincipal(), GuestId, id => Task.FromResult(id == GuestId));

        Assert.Null(playerId);
    }

    [Fact]
    public async Task Guest_WithAnInvalidId_HasNoPlayerId()
    {
        Assert.Null(await PlayerIdentity.ResolveAsync(null, "not-a-guid", NothingTaken));
    }
}
