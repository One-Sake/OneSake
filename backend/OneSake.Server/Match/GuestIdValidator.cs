namespace OneSake.Server.Match;

// The guest ID is a random GUID that the browser stores in localStorage.
// It is NOT secure authentication: any client can send any ID. It only links
// match results to a browser until a real login exists (stage 2).
public static class GuestIdValidator
{
    // Returns the GUID in a normalized form ("D" format, lower case) or null when it is not a GUID.
    public static string? Normalize(string? guestId)
    {
        if (string.IsNullOrWhiteSpace(guestId) || !Guid.TryParse(guestId.Trim(), out var parsedGuid))
        {
            return null;
        }

        return parsedGuid == Guid.Empty ? null : parsedGuid.ToString("D");
    }
}
