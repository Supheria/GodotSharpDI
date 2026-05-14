// =============================================================================
// PlayerStatsService.cs
//
// Demonstrates: Pure C# service interface and implementation (no need to inherit Node)
//
// In layered architecture, these types belong to the Domain / Infrastructure layer,
// do not depend on Godot or GodotSharpDI, only exposed to DI container through [Host]'s [Provide] member.
// =============================================================================

namespace GodotSharpDI.Sample;

/// <summary>
/// Player stats service interface (Domain layer)
/// </summary>
public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

/// <summary>
/// Player stats service implementation (Infrastructure layer)
/// Pure C# class, does not need to inherit Node, exposed to DI container via [Provide].
/// </summary>
public sealed class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
