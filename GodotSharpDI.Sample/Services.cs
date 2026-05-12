// =============================================================================
// Services.cs
//
// 演示：跨 Host 依赖链示例所需的服务接口与实现
//
// 场景：
//   HostB 提供 IServiceB
//   HostC 依赖 IServiceB（通过 WaitFor），并提供 IServiceC
//   → 演示跨 Host 的 WaitFor 依赖顺序控制
// =============================================================================

namespace GodotSharpDI.Sample;

public interface IServiceA { }
public interface IServiceB { }
public interface IServiceC { }

public sealed class ServiceA : IServiceA { }
public sealed class ServiceB : IServiceB { }
public sealed class ServiceC : IServiceC { }
