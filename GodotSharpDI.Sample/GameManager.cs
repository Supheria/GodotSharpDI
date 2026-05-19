// =============================================================================
// GameManager.cs
//
// Demonstrated features:
//   1. [Host] — Marks a class as a service provider (must be a Node subclass)
//   2. [Inject] — Field injection, consumes services provided by other Hosts
//   3. [Inject(FailureCallback = true)] — Callback handling when injection fails
//   4. [Provide] property — Synchronously provides a service (exposes interface type)
//   5. [Provide] method — Asynchronously provides a service (returns Task<T>)
//   6. [Provide].WaitFor — Waits for dependency injection to complete before providing service
//   7. [Provide].ExposedTypes — Exposes implementation type as interface type
//   8. IDependenciesResolved — Callback after all [Inject] members are resolved
//   9. IsAllDependenciesReady — Checks if all injections succeeded (with null safety hints)
// =============================================================================

using System.Threading.Tasks;
using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Host]
public sealed partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // ── [Inject] Basic usage ──────────────────────────────────────
    // Consumes services provided by PlayerStatsCenter (another Host).
    // FailureCallback = true means OnXxxInjectionFailed() will be called when injection fails.

    [Inject(FailureCallback = true)]
    private PlayerStatsCenter _playerStatsCenter = default!;

    // ── [Inject] Consuming pure C# services ────────────────────────
    // PlayerStatsService is a pure C# class (not a Node), exposed by another Host's [Provide].

    [Inject]
    private IPlayerStats _playerStats = default!;

    // ── [Provide] property — Synchronous provision ──────────────────────
    // Exposes self as IGameState interface.
    // ExposedTypes allows exposing an implementation type as multiple interfaces.

    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;

    // ── [Provide] method — Async provision + WaitFor ────────────────────
    // Returns Task<PlayerStatsService>, framework handles async flow automatically.
    // WaitFor = [nameof(_playerStatsCenter)] means:
    //   Wait for _playerStatsCenter injection to complete (success or failure) before calling this method.
    // This ensures _playerStatsCenter can be safely used in GetPlayerStatsService().

    [Provide(ExposedTypes = [typeof(IPlayerStats)], WaitFor = [nameof(_playerStatsCenter)])]
    public async Task<PlayerStatsService> GetPlayerStatsService()
    {
        // Asynchronously create service instance, waits for _playerStatsCenter injection to complete before executing
        return await Task.Run(() => new PlayerStatsService());
    }

    // ── IGameState interface implementation ────────────────────────────

    public GameStateType CurrentState { get; set; }

    // ── IDependenciesResolved callback ─────────────────────────────
    // Called when all [Inject] members are resolved (success or failure).
    // Use IsAllDependenciesReady to check if all injections succeeded.

    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            GD.Print("[GameManager] All dependencies injected successfully");
        }
        else
        {
            GD.Print("[GameManager] Some dependencies failed to inject");
        }
    }

    // ── [Inject(FailureCallback)] callback method ───────────────────
    // Method naming rule: On{memberName}InjectionFailed()
    // Called by framework when _playerStatsCenter injection fails.

    partial void OnPlayerStatsCenterInjectionFailed()
    {
        GD.Print("[GameManager] PlayerStatsCenter injection failed!");
    }

    // ── _Notification declaration ─────────────────────────────────────
    // Framework requires all [Host]/[User]/[Scope] to declare this method,
    // used to take over Godot's lifecycle notifications (EnterTree/Ready/ExitTree).

    public override partial void _Notification(int what);
}
