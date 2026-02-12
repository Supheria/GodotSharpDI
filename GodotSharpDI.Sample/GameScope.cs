using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Modules(Hosts = [typeof(GameManager), typeof(PlayerStatsCenter)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
