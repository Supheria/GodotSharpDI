using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Modules(Hosts = [typeof(HostB), typeof(HostC)])]
public partial class GameScope : Node, IScope
{
    public override partial void _Notification(int what);
}


// [Modules(Hosts = [typeof(GameManager7), typeof(HostC)])]
// public partial class GameScope8 : Node, IScope
// {
//     public override partial void _Notification(int what);
// }
