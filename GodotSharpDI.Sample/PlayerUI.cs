// =============================================================================
// PlayerUI.cs
//
// Demonstrated features:
//   1. [User] — Marks a class as a service consumer (must be a Node subclass, like Control)
//   2. [Inject] property injection — Receives injection via set-only property
//   3. [Inject(ReadyCallback = true)] — Callback handling when injection succeeds
//   4. IDependenciesResolved — Callback after all injections complete
//   5. IsAllDependenciesReady — Checks if all injections succeeded
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[User]
public sealed partial class PlayerUI : Control, IDependenciesResolved
{
    // ── [Inject] Basic field injection ──────────────────────────────────
    // Consumes IGameState service exposed by GameManager Host.

    [Inject]
    private IGameState _gameState = default!;

    // ── [Inject] Field injection + ReadyCallback ─────────────────────
    // ReadyCallback = true means OnXxxInjectionReady() will be called when injection succeeds.
    // This is suitable for scenarios where initialization logic needs to execute immediately after injection.

    [Inject(ReadyCallback = true)]
    private IPlayerStats _playerStats = default!;

    // ── [Inject(ReadyCallback)] callback method ─────────────────────
    // Method naming rule: On{memberName}InjectionReady(IPlayerStats value)
    // Called by framework when _playerStats injection succeeds, parameter is the injected instance.

    partial void OnPlayerStatsInjectionReady(IPlayerStats playerStats)
    {
        GD.Print($"[PlayerUI] PlayerStats ready! Health={playerStats.Health}, Mana={playerStats.Mana}");
    }

    // ── Godot lifecycle ────────────────────────────────────────
    // _Ready is called in Godot engine, at this point dependencies may not be injected yet.
    // Framework's _Notification will reset injection state on EnterTree,
    // and start resolving dependencies on Ready, so [Inject] members should not be accessed in _Ready.

    public override void _Ready()
    {
        GD.Print("[PlayerUI] _Ready called (dependencies may not be ready yet)");
    }

    // ── IDependenciesResolved callback ─────────────────────────────
    // Called when all [Inject] members are resolved.
    // Use IsAllDependenciesReady to determine if all succeeded.
    // When IsAllDependenciesReady == true, all injected members are guaranteed non-null.

    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            GD.Print("[PlayerUI] All dependencies ready, updating UI...");
            // All [Inject] members can be safely accessed here
        }
        else
        {
            GD.Print("[PlayerUI] Some dependencies failed, showing fallback UI");
        }
    }

    public override partial void _Notification(int what);
}
