using System;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

[Provider]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;

    [Provides(typeof(IPlayerStats))]
    public PlayerStatsService Self => this;
}

// [Singleton(typeof(IPlayerStats2))]
public partial class PlayerStatsService2 
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
