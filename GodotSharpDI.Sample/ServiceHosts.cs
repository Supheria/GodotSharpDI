// =============================================================================
// ServiceHosts.cs
//
// 演示功能：
//   1. 跨 Host 依赖链 — HostB 提供 IServiceB，HostC 依赖 IServiceB 并提供 IServiceC
//   2. [Provide] WaitFor — HostC 等待 IServiceB 注入完成后再创建 ServiceC
//   3. 多 Host 协作 — 多个 Host 通过 Scope 协调依赖关系
//
// 场景树结构：
//   GameScope
//   ├── HostB (提供 IServiceB)
//   └── HostC (注入 IServiceB，等待后提供 IServiceC)
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

// ── HostB：简单的服务提供者 ────────────────────────────────────
// 提供 IServiceB，无任何依赖。

[Host]
public partial class HostB : Node
{
    [Provide(ExposedTypes = [typeof(IServiceB)])]
    public ServiceB ServiceB => new();

    public override partial void _Notification(int what);
}

// ── HostC：有 WaitFor 依赖的服务提供者 ────────────────────────
// 依赖 IServiceB（通过 [Inject]），使用 WaitFor 确保在注入完成后再创建 ServiceC。
// 这演示了服务之间的初始化顺序控制。

[Host]
public partial class HostC : Node
{
    [Inject]
    private IServiceB _serviceB = default!;

    [Provide(ExposedTypes = [typeof(IServiceC)], WaitFor = [nameof(_serviceB)])]
    public ServiceC ServiceC => new();

    public override partial void _Notification(int what);
}
