// =============================================================================
// GameManager.cs
//
// 演示功能：
//   1. [Host] — 标记类为服务提供者（必须是 Node 子类）
//   2. [Inject] — 字段注入，消费其他 Host 提供的服务
//   3. [Inject(FailureCallback = true)] — 注入失败时的回调处理
//   4. [Provide] 属性 — 同步提供服务（暴露接口类型）
//   5. [Provide] 方法 — 异步提供服务（返回 Task<T>）
//   6. [Provide].WaitFor — 等待依赖注入完成后再提供服务
//   7. [Provide].ExposedTypes — 将实现类型暴露为接口类型
//   8. IDependenciesResolved — 所有 [Inject] 成员解析完成后的回调
//   9. IsAllDependenciesReady — 检查所有注入是否成功（带 null 安全提示）
// =============================================================================

using System.Threading.Tasks;
using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Host]
public sealed partial class GameManager : Node, IGameState, IDependenciesResolved
{
    // ── [Inject] 基本用法 ──────────────────────────────────────
    // 消费 PlayerStatsCenter（另一个 Host）提供的服务。
    // FailureCallback = true 表示注入失败时会调用 OnXxxInjectionFailed()。

    [Inject(FailureCallback = true)]
    private PlayerStatsCenter _playerStatsCenter = default!;

    // ── [Inject] 消费纯 C# 服务 ────────────────────────────────
    // PlayerStatsService 是纯 C# 类（非 Node），由其他 Host 的 [Provide] 暴露。

    [Inject]
    private IPlayerStats _playerStats = default!;

    // ── [Provide] 属性 — 同步提供 ──────────────────────────────
    // 将自身暴露为 IGameState 接口。
    // ExposedTypes 允许将一个实现类型暴露为多个接口。

    [Provide(ExposedTypes = [typeof(IGameState)])]
    public GameManager Self => this;

    // ── [Provide] 方法 — 异步提供 + WaitFor ────────────────────
    // 返回 Task<PlayerStatsService>，框架自动处理异步流程。
    // WaitFor = [nameof(_playerStatsCenter)] 表示：
    //   等待 _playerStatsCenter 注入完成（成功或失败）后，才调用此方法。
    // 这确保了 GetPlayerStatsService() 中可以安全使用 _playerStatsCenter。

    [Provide(
        ExposedTypes = [typeof(IPlayerStats)],
        WaitFor = [nameof(_playerStatsCenter)]
    )]
    public async Task<PlayerStatsService> GetPlayerStatsService()
    {
        // 异步创建服务实例，等待 _playerStatsCenter 注入完成后才执行
        return await Task.Run(() => new PlayerStatsService());
    }

    // ── IGameState 接口实现 ────────────────────────────────────

    public GameStateType CurrentState { get; set; }

    // ── IDependenciesResolved 回调 ─────────────────────────────
    // 当所有 [Inject] 成员都解析完成（成功或失败）后调用。
    // 使用 IsAllDependenciesReady 检查是否全部注入成功。

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

    // ── [Inject(FailureCallback)] 的回调方法 ───────────────────
    // 方法名规则：On{成员名}InjectionFailed()
    // 当 _playerStatsCenter 注入失败时由框架调用。

    partial void OnPlayerStatsCenterInjectionFailed()
    {
        GD.Print("[GameManager] PlayerStatsCenter injection failed!");
    }

    // ── _Notification 声明 ─────────────────────────────────────
    // 框架要求所有 [Host]/[User]/[Scope] 必须声明此方法，
    // 用于接管 Godot 的生命周期通知（EnterTree/Ready/ExitTree）。

    public override partial void _Notification(int what);
}
