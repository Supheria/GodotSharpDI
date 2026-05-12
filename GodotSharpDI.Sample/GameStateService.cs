// =============================================================================
// GameStateService.cs
//
// 演示：纯 C# 游戏状态接口与实现
//
// 与 PlayerStatsService 类似，这些类型属于 Domain / Infrastructure 层，
// 可以被任何 [Host] 通过 [Provide] 暴露，被任何 [User] 通过 [Inject] 消费。
// =============================================================================

namespace GodotSharpDI.Sample;

/// <summary>
/// 游戏状态枚举
/// </summary>
public enum GameStateType
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}

/// <summary>
/// 游戏状态服务接口
/// </summary>
public interface IGameState
{
    GameStateType CurrentState { get; set; }
}

/// <summary>
/// 游戏状态服务实现
/// </summary>
public sealed class GameStateService : IGameState
{
    public GameStateType CurrentState { get; set; } = GameStateType.MainMenu;
}
