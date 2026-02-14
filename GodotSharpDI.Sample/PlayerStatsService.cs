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

public partial class PlayerStatsService3 : IPlayerStats<int>
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;

    public PlayerStatsService3(PlayerStatsCenter playerStatsCenter) { }
}

// [Singleton(typeof(IPlayerStats2))]
// public partial class PlayerStatsService2
// {
//     public int Health { get; set; } = 100;
//     public int Mana { get; set; } = 50;
// }
