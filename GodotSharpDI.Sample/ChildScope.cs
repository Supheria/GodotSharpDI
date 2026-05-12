// =============================================================================
// ChildScope.cs
//
// 演示功能：
//   1. 嵌套 Scope — 子 Scope 继承父 Scope 的服务
//   2. 独立的 [Modules] 声明 — 子 Scope 管理自己的 Host 集合
//   3. Scope 层级服务查找 — 子 Scope 找不到的服务会向父 Scope 查找
//
// 场景树结构：
//   GameScope (父 Scope)
//   ├── GameManager (Host)
//   ├── PlayerStatsCenter (Host)
//   ├── PlayerUI (User)
//   ├── HostB (Host)
//   └── ChildScope (子 Scope)
//       └── HostC (Host — 依赖父 Scope 中 HostB 提供的 IServiceB)
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

/// <summary>
/// 子 Scope 示例。
/// HostC 被包含在此 Scope 中，但它依赖的 IServiceB 由父 Scope 的 HostB 提供。
/// 框架会自动向上查找父 Scope 的服务缓存。
/// </summary>
[Modules(Hosts = [typeof(HostC)])]
public partial class ChildScope : Node, IScope
{
    public override partial void _Notification(int what);
}
