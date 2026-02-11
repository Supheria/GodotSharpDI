// using Godot;
// using GodotSharpDI.Abstractions;
//
// namespace GodotSharpDI.Sample;
//
// [User]
// public sealed partial class PlayerUI2 : Control, IDependenciesResolved
// {
//     public override partial void _Notification(int what);
//
//     public void OnDependenciesResolved(bool isAllDependenciesReady)
//     {
//         throw new System.NotImplementedException();
//     }
// }
//
// [User]
// public sealed partial class PlayerUI : Control, IDependenciesResolved
// {
//     // [Inject]
//     // private IPlayerStats<int> PlayerStats
//     // {
//     //     set => GD.Print("PlayerUI inject Player Stats");
//     // }
//
//     [Inject]
//     private IPlayerStats<int>? _playerStats;
//
//     [Inject]
//     private GameManager gameState
//     {
//         set => GD.Print("PlayerUI inject Game State");
//     }
//
//     // [Inject]
//     // private IPlayerStats PlayerStats;
//
//     public override void _Ready()
//     {
//         base._Ready();
//
//         GD.Print("PlayerUI is ready before services ready");
//     }
//
//     void IDependenciesResolved.OnDependenciesResolved(bool isAllDependenciesReady)
//     {
//         if (isAllDependenciesReady)
//         {
//             GD.Print("PlayerUI updated after dependencies ready");
//         }
//         else
//         {
//             GD.Print("PlayerUI updated after some dependencies failed");
//         }
//
//         if (IsAllDependenciesReady)
//         {
//             // var a = PlayerStats.Health;
//         }
//     }
//
//     public override partial void _Notification(int what);
//
//     // partial void OnGameStateInjectionFailed(string error)
//     // {
//     //     GD.Print("PlayerUI inject Game State Injection Failed");
//     // }
// }
