using System;
using System.Threading.Tasks;
using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public interface IPlayerStats<T>
{
    int Health { get; set; }
    int Mana { get; set; }
}

[Singleton]
public partial class PlayerStatsService3 : IPlayerStats<int>
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;

    [Inject]
    private PlayerStatsService3 gameState
    {
        set => GD.Print("PlayerStatsService inject Game State");
    }

    [Provide(ExposedTypes = [typeof(IPlayerStats<int>)], WaitFor = [nameof(gameState)])]
    public Task<PlayerStatsService3> Self => Task.Run(() => this);

    public PlayerStatsService3(int a) { }

    public PlayerStatsService3() { }
}

// [Singleton(typeof(IPlayerStats2))]
// public partial class PlayerStatsService2
// {
//     public int Health { get; set; } = 100;
//     public int Mana { get; set; } = 50;
// }
