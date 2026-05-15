namespace GodotSharpDI.SourceGenerator.Tests.Helpers.Mocks;

/// <summary>
/// Minimal Godot type stubs for source generator diagnostic testing.
/// Only includes types referenced by generated code stubs (Node, GD).
/// Does NOT support runtime execution — use <see cref="E2EGodotMocks"/> for E2E tests.
/// </summary>
internal static class DiagnosticGodotStubs
{
    /// <summary>
    /// Minimal Godot stubs. Node.GetParent() returns null (no parent-child wiring).
    /// Sufficient for compilation validation; not for runtime scene tree traversal.
    /// </summary>
    public static string GetSource() =>
        @"
namespace Godot
{
    public class Node
    {
        public Node? GetParent() => null;
        public virtual void _Notification(int what) { }
        protected const int NotificationEnterTree = 10;
        protected const int NotificationExitTree = 11;
        protected const int NotificationReady = 13;
        protected const int NotificationPredelete = 1;
    }

    public static class GD
    {
        public static void PushError(string message) { }
        public static void PushError(System.Exception ex) { }
        public static void PrintErr(string message) { }
        public static void Print(string message) { }
    }
}
";
}
