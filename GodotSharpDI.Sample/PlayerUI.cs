// =============================================================================
// PlayerUI.cs
//
// 演示功能：
//   1. [User] — 标记类为服务消费者（必须是 Node 子类，如 Control）
//   2. [Inject] 属性注入 — 通过 set-only 属性接收注入
//   3. [Inject(ReadyCallback = true)] — 注入成功时的回调处理
//   4. IDependenciesResolved — 所有注入完成后的回调
//   5. IsAllDependenciesReady — 检查注入是否全部成功
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[User]
public sealed partial class PlayerUI : Control, IDependenciesResolved
{
    // ── [Inject] 基本字段注入 ──────────────────────────────────
    // 消费 GameManager Host 暴露的 IGameState 服务。

    [Inject]
    private IGameState _gameState = default!;

    // ── [Inject] 字段注入 + ReadyCallback ─────────────────────
    // ReadyCallback = true 表示注入成功时会调用 OnXxxInjectionReady()。
    // 这适合需要在注入成功后立即执行初始化逻辑的场景。

    [Inject(ReadyCallback = true)]
    private IPlayerStats _playerStats = default!;

    // ── [Inject(ReadyCallback)] 的回调方法 ─────────────────────
    // 方法名规则：On{成员名}InjectionReady(IPlayerStats value)
    // 当 _playerStats 注入成功时由框架调用，参数为注入的实例。

    partial void OnPlayerStatsInjectionReady(IPlayerStats playerStats)
    {
        GD.Print($"[PlayerUI] PlayerStats ready! Health={playerStats.Health}, Mana={playerStats.Mana}");
    }

    // ── Godot 生命周期 ────────────────────────────────────────
    // _Ready 在 Godot 引擎中调用，此时依赖可能尚未注入完成。
    // 框架的 _Notification 会在 EnterTree 时重置注入状态，
    // 在 Ready 时开始解析依赖，因此 _Ready 中不应访问 [Inject] 成员。

    public override void _Ready()
    {
        GD.Print("[PlayerUI] _Ready called (dependencies may not be ready yet)");
    }

    // ── IDependenciesResolved 回调 ─────────────────────────────
    // 所有 [Inject] 成员解析完成后调用。
    // 使用 IsAllDependenciesReady 判断是否全部成功。
    // 当 IsAllDependenciesReady == true 时，所有注入成员保证非 null。

    public void OnDependenciesResolved()
    {
        if (IsAllDependenciesReady)
        {
            GD.Print("[PlayerUI] All dependencies ready, updating UI...");
            // 此处可以安全访问所有 [Inject] 成员
        }
        else
        {
            GD.Print("[PlayerUI] Some dependencies failed, showing fallback UI");
        }
    }

    public override partial void _Notification(int what);
}
