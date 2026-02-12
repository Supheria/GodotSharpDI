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
public sealed partial class GameManager : Node, IGameState, IDependenciesResolved
{
    [Inject]
    private PlayerStatsCenter _playerStatsCenter;

    [Provide(ExposedTypes = [typeof(IGameState)])]
    public async Task<GameManager> GetSelf()
    {
        return this;
    }

    [Provide(ExposedTypes = [typeof(PlayerStatsService3)], WaitFor = [nameof(_playerStatsCenter)])]
    public Task<PlayerStatsService3> GetPlayerStatsService3()
    {
        return Task.Run(() => new PlayerStatsService3(_playerStatsCenter));
    }

    public override partial void _Notification(int what);

    public GameState CurrentState { get; set; }

    public void OnDependenciesResolved(bool isAllDependenciesReady)
    {
        var a = 0;
    }
}
