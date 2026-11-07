namespace OneSake.Game.State;

public enum BattleStep
{
    Block,
    Counter,
}

// The currently running battle (rule 7). Only one battle can happen at the same time.
public sealed class BattleState
{
    public string AttackerPlayerId { get; init; } = string.Empty;
    public string DefenderPlayerId { get; init; } = string.Empty;
    public CardInstance Attacker { get; init; } = new();
    public CardInstance Target { get; set; } = new();
    // Power added to the target by [Counter] cards during this battle.
    public int CounterBonus { get; set; }
    public BattleStep Step { get; set; } = BattleStep.Block;
    public bool BlockerUsed { get; set; }
}
