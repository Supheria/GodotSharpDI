using System;
using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[Host]
public sealed partial class PlayerStatsCenter : Node
{
    [Provide]
    public PlayerStatsCenter Self
    {
        get
        {
            throw new Exception();
            return this;
        }
    }

    public override partial void _Notification(int what);
}
