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

public interface IGameState2
{
    public GameState CurrentState { get; set; }
}

[Host]
public sealed partial class GameManager6 : Node, IGameState, IGameState2
{
    [Inject]
    private PlayerStatsCenter _playerStatsCenter;

    [Inject]
    private PlayerStatsService3 _playerStatsService;

    [Provide(
        ExposedTypes = [typeof(IGameState)],
        WaitFor = [nameof(_playerStatsCenter), nameof(_playerStatsService)]
    )]
    public async Task<GameManager6> GetSelf()
    {
        return this;
    }
    //
    [Provide(ExposedTypes = [typeof(IGameState), typeof(IGameState2)])]
    public GameManager6 Self => this;
    //
    [Provide(ExposedTypes = [typeof(PlayerStatsService3)])]
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
