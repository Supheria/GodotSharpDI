// =============================================================================
// GameScope.cs
//
// Demonstrated features:
//   1. [Modules] — Declares which Hosts this Scope manages
//   2. IScope — DI container interface, manages service lifecycle and dependency resolution
//   3. _Notification declaration — Required declaration for framework to take over lifecycle
//
// Notes:
//   - Host types listed in [Modules] must exist as nodes in this Scope's scene subtree
//   - Each service type can only have one Provider in the same Scope
//   - Child Scopes (like ChildScope) can inherit services from parent Scope
// =============================================================================

using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Modules(typeof(GameManager), typeof(PlayerStatsCenter), typeof(HostB))]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
