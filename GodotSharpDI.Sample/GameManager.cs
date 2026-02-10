using System;
using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public class GameState { }

public interface IGameState
{
    public GameState CurrentState { get; set; }
}

[Host, User]
public sealed partial class GameManager : Node, IGameState
{
    [Inject]
    private IPlayerStats<int> PlayerStats
    {
        set => GD.Print("PlayerUI inject Player Stats");
    }

    [Provides(WaitFor = nameof(PlayerStats))]
    private GameManager Self
    {
        get
        {
            GD.Print("GameManager self provided");
            // throw new Exception();
            return this;
        }
    }

    GameState IGameState.CurrentState { get; set; } = new();

    public override partial void _Notification(int what);
}
