namespace OneSake.Persistence.Entities;

// A player account (username + password). Passwords and recovery codes are only stored as
// hashes (ASP.NET Core PasswordHasher, PBKDF2 with salt), never in plain text.
public class Account
{
    public Guid Id { get; set; }

    // As the player typed it (shown in the UI).
    public string Username { get; set; } = "";

    // Upper case, used for the unique index so "Ruffy" and "ruffy" are the same name.
    public string NormalizedUsername { get; set; } = "";

    public string PasswordHash { get; set; } = "";

    // The one-time recovery code for "forgot password" (hashed). A new code is created after every use.
    public string RecoveryCodeHash { get; set; } = "";

    // Changes on every password change or reset. Login cookies carry the stamp; an old stamp
    // means the cookie is no longer valid (logout on all devices).
    public string SecurityStamp { get; set; } = "";

    // The ID used in MatchResults and PlayerRatings (same format as the old guest ID).
    // When a guest registers, this is the old guest ID, so the match history stays.
    public string PlayerId { get; set; } = "";

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
}
