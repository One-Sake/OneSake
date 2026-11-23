using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OneSake.Server.Auth;

public enum AuthError
{
    None,
    UsernameInvalid,
    UsernameTaken,
    PasswordTooShort,
    PasswordTooLong,
    PasswordTooCommon,
    PasswordContainsUsername,
    // Wrong username, wrong password or wrong recovery code. Deliberately one error for all,
    // so nobody can find out which part was wrong.
    InvalidCredentials,
    LockedOut
}

// Rules for usernames and passwords. Follows NIST SP 800-63B: a minimum length and a check
// against common passwords instead of forced special characters.
public static partial class AuthRules
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 20;
    public const int PasswordMinLength = 10;
    // PBKDF2 works with any length; the limit only stops huge requests.
    public const int PasswordMaxLength = 128;

    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // A small list of very common passwords (from public leak statistics) that are at least 10 long.
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "1234567890", "0123456789", "12345678910", "123456789a", "qwertyuiop", "1q2w3e4r5t",
        "1qaz2wsx3edc", "qwertz1234", "qwerty1234", "qwerty123456", "asdfghjkl1", "password12",
        "password123", "password1234", "passwort12", "passwort123", "passwort1234", "iloveyou12",
        "abcdefghij", "abc1234567", "1111111111", "0000000000", "aaaaaaaaaa", "monkeyd.luffy",
        "onepiece123", "onepiece1234", "onesake123", "onesake1234", "luffy12345", "zoro123456",
        "letmein123", "welcome123", "football12", "dragonball", "starwars12", "minecraft1",
        "hallo12345", "geheim1234", "schalke04!", "sommer2026", "winter2026"
    };

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex UsernamePattern();

    public static string NormalizeUsername(string? username)
    {
        return (username ?? string.Empty).Trim().ToUpperInvariant();
    }

    public static AuthError ValidateUsername(string? username)
    {
        var trimmed = (username ?? string.Empty).Trim();
        if (trimmed.Length < UsernameMinLength || trimmed.Length > UsernameMaxLength || !UsernamePattern().IsMatch(trimmed))
        {
            return AuthError.UsernameInvalid;
        }

        return AuthError.None;
    }

    public static AuthError ValidatePassword(string? password, string? username)
    {
        password ??= string.Empty;
        if (password.Length < PasswordMinLength)
        {
            return AuthError.PasswordTooShort;
        }

        if (password.Length > PasswordMaxLength)
        {
            return AuthError.PasswordTooLong;
        }

        if (CommonPasswords.Contains(password) || password.Distinct().Count() <= 2)
        {
            return AuthError.PasswordTooCommon;
        }

        var trimmedUsername = (username ?? string.Empty).Trim();
        if (trimmedUsername.Length > 0 && password.Contains(trimmedUsername, StringComparison.OrdinalIgnoreCase))
        {
            return AuthError.PasswordContainsUsername;
        }

        return AuthError.None;
    }

    // German message for the frontend.
    public static string Describe(AuthError error)
    {
        return error switch
        {
            AuthError.UsernameInvalid => $"Der Benutzername muss {UsernameMinLength}-{UsernameMaxLength} Zeichen lang sein (Buchstaben, Ziffern, _ . -).",
            AuthError.UsernameTaken => "Dieser Benutzername ist schon vergeben.",
            AuthError.PasswordTooShort => $"Das Passwort muss mindestens {PasswordMinLength} Zeichen lang sein.",
            AuthError.PasswordTooLong => $"Das Passwort darf höchstens {PasswordMaxLength} Zeichen lang sein.",
            AuthError.PasswordTooCommon => "Dieses Passwort ist zu leicht zu erraten.",
            AuthError.PasswordContainsUsername => "Das Passwort darf den Benutzernamen nicht enthalten.",
            AuthError.InvalidCredentials => "Benutzername oder Passwort/Code falsch.",
            AuthError.LockedOut => "Zu viele Fehlversuche. Bitte versuche es später erneut.",
            _ => string.Empty
        };
    }

    public static string CreateSecurityStamp()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    }
}

// The one-time code for "forgot password": 16 characters in groups of four (about 80 bits).
// The alphabet leaves out characters that are easy to mix up (0/O, 1/I/L).
public static class RecoveryCodes
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Length = 16;

    public static string Generate()
    {
        var raw = RandomNumberGenerator.GetString(Alphabet, Length);
        return string.Join('-', Enumerable.Range(0, Length / 4).Select(group => raw.Substring(group * 4, 4)));
    }

    // Upper case without dashes and spaces, so "k7qf-92mx ..." also works.
    public static string Normalize(string? code)
    {
        return new string((code ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }
}
