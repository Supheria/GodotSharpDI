using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public class GameState { }

public interface IGameState
{
    public GameState CurrentState { get; set; }
}

[Host]
public sealed partial class GameManager : Node, IGameState
{
    [Singleton(typeof(GameManager))]
    private GameManager Self
    {
        get
        {
            GD.Print("GameManager self provided");
            return this;
        }
    }

    GameState IGameState.CurrentState { get; set; } = new();

    public override partial void _Notification(int what);
}
