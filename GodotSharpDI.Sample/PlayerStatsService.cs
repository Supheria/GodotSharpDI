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

[Provider]
public partial class PlayerStatsService2 : IPlayerStats<int>
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;

    [Inject]
    private PlayerStatsService2 gameState
    {
        set => GD.Print("PlayerStatsService inject Game State");
    }

    [Provides(typeof(IPlayerStats<int>), WaitFor = [nameof(gameState)])]
    public Task<PlayerStatsService2> Self => Task.Run(() => this);
}

// [Singleton(typeof(IPlayerStats2))]
// public partial class PlayerStatsService2
// {
//     public int Health { get; set; } = 100;
//     public int Mana { get; set; } = 50;
// }
