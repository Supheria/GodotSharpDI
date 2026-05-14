// =============================================================================
// ChildScope.cs
//
// Demonstrated features:
//   1. Nested Scope — Child Scope inherits services from parent Scope
//   2. Independent [Modules] declaration — Child Scope manages its own Host collection
//   3. Scope hierarchy service lookup — Services not found in child Scope will be looked up in parent Scope
//
// Scene tree structure:
//   GameScope (parent Scope)
//   ├── GameManager (Host)
//   ├── PlayerStatsCenter (Host)
//   ├── PlayerUI (User)
//   ├── HostB (Host)
//   └── ChildScope (child Scope)
//       └── HostC (Host — depends on IServiceB provided by HostB in parent Scope)
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

/// <summary>
/// Child Scope example.
/// HostC is contained in this Scope, but it depends on IServiceB provided by HostB in the parent Scope.
/// Framework will automatically look up parent Scope's service cache.
/// </summary>
[Modules(typeof(HostC))]
public partial class ChildScope : Node, IScope
{
    public override partial void _Notification(int what);
}
