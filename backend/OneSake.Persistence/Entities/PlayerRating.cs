namespace OneSake.Persistence.Entities;

// ELO rating of one guest player (shown as One Piece bounty). Only ranked Quick Match games count.
public class PlayerRating
{
    public string GuestId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Rating { get; set; } = 1000;
    public int Wins { get; set; }
    public int Losses { get; set; }
    // Highest rating ever reached.
    public int Peak { get; set; } = 1000;
    public DateTime LastPlayedUtc { get; set; }
    public string? FavoriteLeaderCardId { get; set; }
}
