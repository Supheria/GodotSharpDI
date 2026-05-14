// =============================================================================
// GameStateService.cs
//
// Demonstrates: Pure C# game state interface and implementation
//
// Similar to PlayerStatsService, these types belong to the Domain / Infrastructure layer,
// can be exposed by any [Host] via [Provide], and consumed by any [User] via [Inject].
// =============================================================================

namespace GodotSharpDI.Sample;

/// <summary>
/// Game state enum
/// </summary>
public enum GameStateType
{
    MainMenu,
    Playing,
    Paused,
    GameOver
}

/// <summary>
/// Game state service interface
/// </summary>
public interface IGameState
{
    GameStateType CurrentState { get; set; }
}

/// <summary>
/// Game state service implementation
/// </summary>
public sealed class GameStateService : IGameState
{
    public GameStateType CurrentState { get; set; } = GameStateType.MainMenu;
}
