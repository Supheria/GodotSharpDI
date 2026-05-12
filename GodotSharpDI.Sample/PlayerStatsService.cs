// =============================================================================
// PlayerStatsService.cs
//
// 演示：纯 C# 服务接口与实现（无需继承 Node）
//
// 在分层架构中，这些类型属于 Domain / Infrastructure 层，
// 不依赖 Godot 或 GodotSharpDI，仅通过 [Host] 的 [Provide] 成员暴露给 DI 容器。
// =============================================================================

namespace GodotSharpDI.Sample;

/// <summary>
/// 玩家状态服务接口（Domain 层）
/// </summary>
public interface IPlayerStats
{
    int Health { get; set; }
    int Mana { get; set; }
}

/// <summary>
/// 玩家状态服务实现（Infrastructure 层）
/// 纯 C# 类，不需要继承 Node，通过 [Provide] 暴露给 DI 容器。
/// </summary>
public sealed class PlayerStatsService : IPlayerStats
{
    public int Health { get; set; } = 100;
    public int Mana { get; set; } = 50;
}
