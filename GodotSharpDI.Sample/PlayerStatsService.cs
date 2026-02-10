using System;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public interface IPlayerStats<T>
{
    int Health { get; set; }
    int Mana { get; set; }
}

public interface IPlayerStats2
{
    int Health { get; set; }
    int Mana { get; set; }
}

[Singleton(typeof(IPlayerStats<int>))]
public partial class PlayerStatsService : IPlayerStats<int>
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;

    public PlayerStatsService(GameManager gameManager)
    {
        // throw new Exception();
    }
}

[Singleton(typeof(IPlayerStats2))]
public partial class PlayerStatsService2 : IPlayerStats2
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
