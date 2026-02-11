using System;
using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public interface IPlayerStats<T>
{
    int Health { get; set; }
    int Mana { get; set; }
}

[Provider]
public partial class PlayerStatsService : IPlayerStats<int>
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;

    [Inject]
    private PlayerStatsService gameState
    {
        set => GD.Print("PlayerStatsService inject Game State");
    }

    [Provides(typeof(IPlayerStats<int>), WaitFor = [nameof(gameState)])]
    public PlayerStatsService Self => this;
}

// [Singleton(typeof(IPlayerStats2))]
public partial class PlayerStatsService2
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
