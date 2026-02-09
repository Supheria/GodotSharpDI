using Godot;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI.Sample;

[User]
public sealed partial class PlayerUI : Control, IDependenciesResolved
{
    [Inject]
    private IPlayerStats PlayerStats
    {
        set => GD.Print("PlayerUI inject Player Stats");
    }

    [Inject(FailureCallback = true)]
    private GameManager gameState
    {
        set => GD.Print("PlayerUI inject Game State");
    }

    public override void _Ready()
    {
        base._Ready();

        GD.Print("PlayerUI is ready before services ready");
    }

    void IDependenciesResolved.OnDependenciesResolved()
    {
        GD.Print("PlayerUI updated after services ready");
    }

    public override partial void _Notification(int what);

    partial void OnGameStateInjectionFailed(string error)
    {
        GD.Print("PlayerUI inject Game State Injection Failed");
    }
}
