using OneSake.Domain;
using OneSake.Server.Match;

namespace OneSake.Tests.Session;

// A clock the test moves forward by hand.
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    public void Advance(TimeSpan time) => UtcNow += time;

    public void Advance(double seconds) => Advance(TimeSpan.FromSeconds(seconds));
}

// A match between Alice (connection "conn-a") and Bob ("conn-b") with seats like on the real
// server (JoinConnection), a fake clock and a plain deck without effects.
public sealed class SessionMatch
{
    private SessionMatch(MatchRoom room, FakeClock clock, SeatAssignedDto alice, SeatAssignedDto bob)
    {
        Room = room;
        Clock = clock;
        Alice = alice;
        Bob = bob;
    }

    public MatchRoom Room { get; }
    public FakeClock Clock { get; }
    public SeatAssignedDto Alice { get; }
    public SeatAssignedDto Bob { get; }
    public List<MatchUpdate> Updates { get; } = [];

    // Both players in the lobby with decks, the match is not started.
    public static SessionMatch CreateLobby(MatchRoomOptions? options = null, bool isQuickMatch = false, ulong seed = 7)
    {
        var clock = new FakeClock();
        var room = new MatchRoom("SESS", isQuickMatch, seed: seed, options: options, clock: clock);
        var (_, alice) = room.JoinConnection("conn-a", "Alice", "11111111-1111-4111-8111-111111111111");
        var (_, bob) = room.JoinConnection("conn-b", "Bob", "22222222-2222-4222-8222-222222222222");
        room.SetPlayerDeck(alice.PlayerId, Deck());
        room.SetPlayerDeck(bob.PlayerId, Deck());
        return new SessionMatch(room, clock, alice, bob);
    }

    // Started match: the dice winner lets Alice go first; with mulligan both players keep.
    public static SessionMatch StartInMain(MatchRoomOptions? options = null, bool isQuickMatch = false, ulong seed = 7)
    {
        var match = CreateLobby(options, isQuickMatch, seed);
        match.Remember(match.Room.StartMatch());
        var order = match.Prompt("TURN_ORDER_DECISION");
        match.Submit(order, order.PlayerId == match.Alice.PlayerId ? "GO_FIRST" : "GO_SECOND");
        match.Submit(match.Prompt("MULLIGAN_DECISION", match.Alice.PlayerId), "KEEP");
        match.Submit(match.Prompt("MULLIGAN_DECISION", match.Bob.PlayerId), "KEEP");
        return match;
    }

    public MatchUpdate Remember(MatchUpdate? update)
    {
        Assert.NotNull(update);
        Updates.Add(update);
        return update;
    }

    public IReadOnlyList<ChoicePromptDto> OpenPrompts => Room.CreateInvariantSnapshot().OpenPrompts;

    public ChoicePromptDto Prompt(string kind, string? playerId = null)
    {
        return OpenPrompts.Single(prompt => prompt.Kind == kind && (playerId is null || prompt.PlayerId == playerId));
    }

    public MatchUpdate Submit(ChoicePromptDto prompt, string option, string? selected = null, string? target = null)
    {
        return Remember(Room.SubmitChoice(new ChoiceSubmissionDto
        {
            ChoiceId = prompt.ChoiceId,
            PlayerId = prompt.PlayerId,
            SelectedOption = option,
            SelectedCardInstanceId = selected,
            TargetCardInstanceId = target
        }));
    }

    public GameStateDto View(string playerId) => Room.CreateStateSnapshot(playerId);

    public PlayerStateDto Player(string playerId) => View(playerId).Players.Single(player => player.PlayerId == playerId);

    public string Name(string playerId) => playerId == Alice.PlayerId ? "Alice" : "Bob";

    // Alice ends her turn (turn 1), Bob ends his turn, and so on.
    public void EndTurn(string playerId) => Submit(Prompt("MAIN_ACTION", playerId), "END_TURN");

    // Plain Characters with a counter value, no card effects.
    public static PlayerDeckSubmissionDto Deck()
    {
        return new PlayerDeckSubmissionDto
        {
            DeckId = "session-deck",
            DeckName = "Session Deck",
            LeaderCardId = "SES-LEADER",
            LeaderName = "Session Leader",
            LeaderPower = 5000,
            LeaderLife = 4,
            Cards =
            [
                new PlayerDeckCardDto
                {
                    CardId = "SES-001",
                    Name = "Plain",
                    Quantity = 50,
                    Category = "Character",
                    Cost = 1,
                    Power = 3000,
                    Counter = 1000
                }
            ]
        };
    }
}
