using System;
using System.Threading.Tasks;
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
    [Provides(ExposedTypes = [typeof(IGameState)])]
    public async Task<GameManager> GetSelf()
    {
        return this;
    }

    [Inject]
    private GameManager inj;

    // [Provides(ExposedTypes = [typeof(IGameState)], WaitFor = [nameof(inj)])]
    // public Task<GameManager> Self => Task.CompletedTask;

    public override partial void _Notification(int what);

    public GameState CurrentState { get; set; }
}
