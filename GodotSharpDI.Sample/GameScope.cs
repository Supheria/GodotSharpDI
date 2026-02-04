using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Modules(Services = [typeof(PlayerStatsService)], Hosts = [typeof(GameManager)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}
