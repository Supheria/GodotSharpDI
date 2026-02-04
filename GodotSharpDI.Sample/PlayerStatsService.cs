using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

[Singleton(typeof(IPlayerStats))]
public partial class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
