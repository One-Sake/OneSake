using OneSake.Domain;
using OneSake.Server.Match;

namespace OneSake.Tests.Replays;

// Plays a complete match with simple, fixed decisions (no randomness in the bot itself), so the
// same seed must always lead to the same match. Used by the determinism and replay tests.
public sealed class MatchBot
{
    private readonly Dictionary<string, ChoicePromptDto> _prompts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GameStateDto> _views = new(StringComparer.Ordinal);
    private readonly string[] _players;

    public MatchBot(MatchRoom room, string firstPlayerId, string secondPlayerId)
    {
        Room = room;
        _players = [firstPlayerId, secondPlayerId];
    }

    public MatchRoom Room { get; }
    // The state hash after every decision.
    public List<string> Hashes { get; } = [];
    public int Decisions { get; private set; }
    public bool IsGameOver => _views.Values.FirstOrDefault()?.Phase == MatchPhase.GameOver;

    public static MatchRoom CreateRoom(ulong seed, string firstPlayerId, string secondPlayerId)
    {
        var room = new MatchRoom("BOT", seed: seed);
        room.JoinPlayer(firstPlayerId, "Alice", "11111111-1111-4111-8111-111111111111");
        room.JoinPlayer(secondPlayerId, "Bob", "22222222-2222-4222-8222-222222222222");
        room.SetPlayerDeck(firstPlayerId, Deck());
        room.SetPlayerDeck(secondPlayerId, Deck());
        return room;
    }

    public MatchBot Play(int maxDecisions = 400)
    {
        Apply(Room.StartMatch());
        while (!IsGameOver && Decisions < maxDecisions)
        {
            var playerId = _players.FirstOrDefault(_prompts.ContainsKey);
            if (playerId is null)
            {
                break;
            }

            var prompt = _prompts[playerId];
            _prompts.Remove(playerId);
            Apply(Decide(playerId, prompt));
            Decisions += 1;
            Hashes.Add(Room.ComputeStateHash());
        }

        return this;
    }

    private MatchUpdate Decide(string playerId, ChoicePromptDto prompt)
    {
        var me = _views[playerId].Players.Single(player => player.PlayerId == playerId);
        var opponent = _views[playerId].Players.Single(player => player.PlayerId != playerId);

        switch (prompt.Kind)
        {
            case "TURN_ORDER_DECISION":
                return Submit(prompt, "GO_FIRST");
            case "MULLIGAN_DECISION":
                // The first player always takes a mulligan: one more shuffle in the match.
                return Submit(prompt, playerId == _players[0] ? "MULLIGAN" : "KEEP");
            case "MAIN_ACTION":
            {
                // Once in turn 3: shuffle the deck manually (random from the player's stream).
                if (_views[playerId].TurnNumber == 3 && !_shuffled.Contains(playerId))
                {
                    _shuffled.Add(playerId);
                    return Room.ManualAction(playerId, new ManualActionDto { Kind = "SHUFFLE_DECK" });
                }

                var playable = me.HandCards.FirstOrDefault(card =>
                    card.Cost <= me.ActiveDonCount && (card.Category != "Character" || me.BoardCards.Count < 5) && card.Category != "Event");
                if (prompt.Options.Contains("PLAY_CARD") && playable is not null)
                {
                    return Submit(prompt, "PLAY_CARD", playable.InstanceId);
                }

                if (prompt.Options.Contains("ATTACK") && me.Leader?.CanAttack == true)
                {
                    return Submit(prompt, "ATTACK", me.Leader.InstanceId, opponent.Leader!.InstanceId);
                }

                var attacker = me.BoardCards.FirstOrDefault(card => card.CanAttack);
                if (prompt.Options.Contains("ATTACK") && attacker is not null)
                {
                    return Submit(prompt, "ATTACK", attacker.InstanceId, opponent.Leader!.InstanceId);
                }

                return Submit(prompt, "END_TURN");
            }

            case "BLOCK_STEP":
                return Submit(prompt, "NO_BLOCK");
            case "COUNTER_STEP":
            {
                var counter = me.HandCards.FirstOrDefault(card => card.Counter > 0);
                return counter is not null && me.HandCount > 4
                    ? Submit(prompt, "COUNTER", counter.InstanceId)
                    : Submit(prompt, "PASS");
            }

            case "TRIGGER_DECISION":
                return Submit(prompt, "ACTIVATE");
            case "ORDER_TRIGGERS":
                return Room.SubmitChoice(new ChoiceSubmissionDto
                {
                    ChoiceId = prompt.ChoiceId,
                    PlayerId = playerId,
                    SelectedOption = "ORDER",
                    OrderedIds = prompt.Triggers.Select(trigger => trigger.Id).Reverse().ToList()
                });
            case "EFFECT_CHOICE":
                if (prompt.Options.Contains("YES"))
                {
                    return Submit(prompt, "YES");
                }

                return prompt.Options.Contains("SELECT") && prompt.Cards.Count > 0
                    ? Submit(prompt, "SELECT", prompt.Cards[0].InstanceId)
                    : Submit(prompt, prompt.Options[0]);
            default:
                return Submit(prompt, prompt.Options[0]);
        }
    }

    private readonly HashSet<string> _shuffled = new(StringComparer.Ordinal);

    private MatchUpdate Submit(ChoicePromptDto prompt, string option, string? selected = null, string? target = null)
    {
        return Room.SubmitChoice(new ChoiceSubmissionDto
        {
            ChoiceId = prompt.ChoiceId,
            PlayerId = prompt.PlayerId,
            SelectedOption = option,
            SelectedCardInstanceId = selected,
            TargetCardInstanceId = target
        });
    }

    // Remembers the newest prompt per player and each player's own view.
    // Recordings delivered with an update (exactly one per finished match).
    public List<OneSake.Game.Replay.MatchRecording> DeliveredRecordings { get; } = [];

    private void Apply(MatchUpdate update)
    {
        if (update.Recording is not null)
        {
            DeliveredRecordings.Add(update.Recording);
        }

        foreach (var (playerId, view) in update.StateSnapshots)
        {
            _views[playerId] = view;
        }

        foreach (var prompt in update.ChoicePrompts)
        {
            _prompts[prompt.PlayerId] = prompt;
        }
    }

    public static PlayerDeckSubmissionDto Deck()
    {
        PlayerDeckCardDto Card(string id, string name, string text, int cost, int power, int counter, int quantity, string category = "Character") => new()
        {
            CardId = id,
            Name = name,
            Quantity = quantity,
            Category = category,
            Cost = cost,
            Power = power,
            Counter = counter,
            Text = text,
            ImageUrl = "https://example.com/card.png"
        };

        return new PlayerDeckSubmissionDto
        {
            DeckId = "bot-deck",
            DeckName = "Bot Deck",
            LeaderCardId = "BOT-LEADER",
            LeaderName = "Bot Leader",
            LeaderPower = 5000,
            LeaderLife = 4,
            Cards =
            [
                Card("BOT-001", "Plain", string.Empty, 1, 3000, 1000, 12),
                Card("BOT-002", "Drawer", "[On Play] Draw 1 card.", 2, 3000, 1000, 10),
                Card("BOT-003", "Lifebuoy", "[Trigger] Draw 1 card.", 1, 2000, 2000, 10),
                Card("BOT-004", "Breaker", "[On Play] K.O. up to 1 of your opponent's Characters with a cost of 3 or less.", 3, 5000, 0, 8),
                Card("BOT-005", "Ender", "[End of Your Turn] Draw 1 card.", 2, 4000, 1000, 10)
            ]
        };
    }
}
