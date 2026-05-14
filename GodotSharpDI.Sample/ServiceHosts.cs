// =============================================================================
// ServiceHosts.cs
//
// Demonstrated features:
//   1. Cross-Host dependency chain — HostB provides IServiceB, HostC depends on IServiceB and provides IServiceC
//   2. [Provide] WaitFor — HostC waits for IServiceB injection to complete before creating ServiceC
//   3. Multi-Host collaboration — Multiple Hosts coordinate dependencies through Scope
//
// Scene tree structure:
//   GameScope
//   ├── HostB (provides IServiceB)
//   └── HostC (injects IServiceB, provides IServiceC after waiting)
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

// ── HostB: Simple service provider ────────────────────────────────────
// Provides IServiceB, no dependencies.

[Host]
public partial class HostB : Node
{
    [Provide(ExposedTypes = [typeof(IServiceB)])]
    public ServiceB ServiceB => new();

    public override partial void _Notification(int what);
}

// ── HostC: Service provider with WaitFor dependencies ────────────────────────
// Depends on IServiceB (via [Inject]), uses WaitFor to ensure ServiceC is created after injection completes.
// This demonstrates initialization ordering control between services.

[Host]
public partial class HostC : Node
{
    [Inject]
    private IServiceB _serviceB = default!;

    [Provide(ExposedTypes = [typeof(IServiceC)], WaitFor = [nameof(_serviceB)])]
    public ServiceC ServiceC => new();

    public override partial void _Notification(int what);
}
