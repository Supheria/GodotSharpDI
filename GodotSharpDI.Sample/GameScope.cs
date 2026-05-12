// =============================================================================
// GameScope.cs
//
// 演示功能：
//   1. [Modules] — 声明此 Scope 管理哪些 Host
//   2. IScope — DI 容器接口，管理服务生命周期和依赖解析
//   3. _Notification 声明 — 框架接管生命周期的必要声明
//
// 注意：
//   - [Modules] 中列出的 Host 类型必须在此 Scope 的场景子树中作为节点存在
//   - 每个服务类型在同一 Scope 中只能有一个 Provider
//   - 子 Scope（如 ChildScope）可以继承父 Scope 的服务
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Modules(Hosts = [typeof(GameManager), typeof(PlayerStatsCenter), typeof(HostB)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
