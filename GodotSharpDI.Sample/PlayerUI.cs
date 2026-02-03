using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[User]
public sealed partial class PlayerUI : Control, IServicesReady
{
    [Inject]
    private IPlayerStats PlayerStats
    {
        set => GD.Print("PlayerUI inject Player Stats");
    }

    [Inject]
    private IGameState GameState
    {
        set => GD.Print("PlayerUI inject Game State");
    }

    public override void _Ready()
    {
        base._Ready();

        GD.Print("PlayerUI is ready before services ready");
    }

    void IServicesReady.OnServicesReady()
    {
        GD.Print("PlayerUI updated after services ready");
    }

    public override partial void _Notification(int what);
}
