namespace GodotSharpDI.SourceGenerator.Tests.Helpers.Mocks;

/// <summary>
/// Full Godot type mocks for end-to-end testing.
/// Includes Node (with parent-child wiring via SetParent/AddChild),
/// Callable (executes immediately), Timer stub, and GD stub.
/// Used by <see cref="E2ETestHelper"/> to compile and run generated code.
/// </summary>
internal static class E2EGodotMocks
{
    /// <summary>
    /// Mock Godot types source. Callable.CallDeferred executes immediately.
    /// Node supports parent-child relationships via SetParent/AddChild.
    /// </summary>
    public static string GetSource() =>
        @"
namespace Godot
{
    public class Node
    {
        private Node? _parent;

        public Node? GetParent() => _parent;

        public void SetParent(Node? parent) => _parent = parent;

        public void AddChild(Node child)
        {
            child._parent = this;
        }

        public virtual void _Notification(int what) { }

        protected const int NotificationEnterTree = 10;
        protected const int NotificationExitTree = 11;
        protected const int NotificationReady = 13;
        protected const int NotificationPredelete = 1;
    }

    public struct Callable
    {
        private System.Action? _action;

        public static Callable From(System.Action action)
        {
            return new Callable { _action = action };
        }

        public void CallDeferred()
        {
            // Execute immediately in tests (no threading)
            _action?.Invoke();
        }
    }

    public class Timer
    {
        public double WaitTime { get; set; }
        public event System.Action? Timeout;
        public void Start() { }
        public void Stop() { }
        public void QueueFree() { }
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
