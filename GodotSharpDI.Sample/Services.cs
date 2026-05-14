// =============================================================================
// Services.cs
//
// Demonstrates: Service interfaces and implementations needed for cross-Host dependency chain example
//
// Scenario:
//   HostB provides IServiceB
//   HostC depends on IServiceB (via WaitFor), and provides IServiceC
//   → Demonstrates cross-Host WaitFor dependency ordering control
// =============================================================================

namespace GodotSharpDI.Sample;

public interface IServiceA { }
public interface IServiceB { }
public interface IServiceC { }

public sealed class ServiceA : IServiceA { }
public sealed class ServiceB : IServiceB { }
public sealed class ServiceC : IServiceC { }
