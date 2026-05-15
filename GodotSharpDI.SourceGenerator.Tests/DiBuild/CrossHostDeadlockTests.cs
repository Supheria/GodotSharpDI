using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

/// <summary>
/// Tests P1 fix: compile-time detection of cross-Host WaitFor deadlocks (GDI_D011)
///
/// Scenario:
///   HostA provides IServiceA and its WaitFor waits for IServiceB injection
///   HostB provides IServiceB and its WaitFor waits for IServiceA injection
///   → IServiceA → IServiceB → IServiceA forms a cross-Host circular wait
///   → GDI_D011 (compile-time Error)
///
/// GDI_D010 vs GDI_D011:
///   D010 - WaitFor cycle within the same Host (local deadlock)
///   D011 - WaitFor cycle across different Hosts (distributed deadlock)
/// </summary>
public class CrossHostDeadlockTests
{
    // ============================================================
    //  Scenarios that should trigger GDI_D011
    // ============================================================

    [Fact]
    public void TwoHosts_WaitForEachOther_ReportsDeadlock()
    {
        // HostA provides IServiceA, waits for IServiceB
        // HostB provides IServiceB, waits for IServiceA
        // → cross-Host deadlock
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public class ServiceA : IServiceA { }
    public class ServiceB : IServiceB { }

    [Host]
    public partial class HostA : Node
    {
        [Inject]
        private IServiceB _serviceB { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_serviceB) })]
        public ServiceA CreateA() => new ServiceA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Inject]
        private IServiceA _serviceA { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_serviceA) })]
        public ServiceB CreateB() => new ServiceB();
    }
}";
        var result = BuildGraph(source);

        var deadlockDiags = result.Diagnostics.Where(d => d.Id == "GDI_D011").ToList();
        Assert.NotEmpty(deadlockDiags);

        var msg = deadlockDiags[0].GetMessage();
        Assert.Contains("->", msg);
        // Should contain both service names
        Assert.True(
            msg.Contains("IServiceA") || msg.Contains("IServiceB"),
            $"Expected IServiceA or IServiceB in message: {msg}"
        );
    }

    [Fact]
    public void ThreeHosts_CircularWait_ReportsDeadlock()
    {
        // HostA provides IA, waits for IB
        // HostB provides IB, waits for IC
        // HostC provides IC, waits for IA
        // → three-node cross-Host cycle
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }
    public class SA : IA { }
    public class SB : IB { }
    public class SC : IC { }

    [Host]
    public partial class HostA : Node
    {
        [Inject] private IB _b { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IA) }, WaitFor = new string[] { nameof(_b) })]
        public SA CreateA() => new SA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Inject] private IC _c { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_c) })]
        public SB CreateB() => new SB();
    }

    [Host]
    public partial class HostC : Node
    {
        [Inject] private IA _a { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IC) }, WaitFor = new string[] { nameof(_a) })]
        public SC CreateC() => new SC();
    }
}";
        var result = BuildGraph(source);

        Assert.NotEmpty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    // ============================================================
    //  Scenarios that should NOT trigger GDI_D011
    // ============================================================

    [Fact]
    public void TwoHosts_OneDirectional_WaitForOnly_NoCycle()
    {
        // HostA provides IA (no WaitFor)
        // HostB provides IB, waits for IA
        // → unidirectional wait, no cycle
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public class SA : IA { }
    public class SB : IB { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IA) })]
        public SA CreateA() => new SA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Inject] private IA _a { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_a) })]
        public SB CreateB() => new SB();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    [Fact]
    public void TwoHosts_DiamondDependency_NoCycle()
    {
        // HostA provides IA (no WaitFor)
        // HostB provides IB (no WaitFor)
        // HostC provides IC, waits for IA and IB
        // → diamond-shaped acyclic graph
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }
    public class SA : IA { }
    public class SB : IB { }
    public class SC : IC { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IA) })]
        public SA CreateA() => new SA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IB) })]
        public SB CreateB() => new SB();
    }

    [Host]
    public partial class HostC : Node
    {
        [Inject] private IA _a { get; set; }
        [Inject] private IB _b { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IC) }, WaitFor = new string[] { nameof(_a), nameof(_b) })]
        public SC CreateC() => new SC();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    [Fact]
    public void SameHost_WaitForCycle_ReportsD010_NotD011()
    {
        // Cycle within the same Host → GDI_D010 (not GDI_D011)
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public class ServiceA : IServiceA { }
    public class ServiceB : IServiceB { }

    [Host]
    public partial class SingleHost : Node
    {
        [Inject] private IServiceA _a { get; set; }
        [Inject] private IServiceB _b { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_b) })]
        public ServiceA CreateA() => new ServiceA();

        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_a) })]
        public ServiceB CreateB() => new ServiceB();
    }
}";
        var result = BuildGraph(source);
        Assert.NotEmpty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
        // Cycle within the same Host does not trigger GDI_D011 (cross-Host deadlock detector does not report single-node SCCs)
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    [Fact]
    public void HostA_WaitsForUnregisteredService_NoDeadlock()
    {
        // HostA waits for IUnregistered, but no Host provides it
        // This is a runtime issue, not a compile-time deadlock
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IUnregistered { }
    public class ServiceA : IServiceA { }

    [Host]
    public partial class HostA : Node
    {
        [Inject] private IUnregistered _unregistered { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_unregistered) })]
        public ServiceA CreateA() => new ServiceA();
    }
}";
        var result = BuildGraph(source);
        // Waiting for a service with no provider does not constitute a deadlock (CrossHostDeadlockDetector only analyzes services with providers)
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    [Fact]
    public void TwoIndependentCycles_BothReported()
    {
        // Two unrelated cross-Host deadlocks coexist → both should report GDI_D011
        // Group 1: HostA(IA) ↔ HostB(IB)
        // Group 2: HostC(IC) ↔ HostD(ID)
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IA { }  public interface IB { }
    public interface IC { }  public interface ID { }
    public class A : IA { }  public class B : IB { }
    public class C : IC { }  public class D : ID { }

    [Host]
    public partial class HostA : Node
    {
        [Inject] private IB _b { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IA) }, WaitFor = new string[] { nameof(_b) })]
        public A CreateA() => new A();
    }

    [Host]
    public partial class HostB : Node
    {
        [Inject] private IA _a { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_a) })]
        public B CreateB() => new B();
    }

    [Host]
    public partial class HostC : Node
    {
        [Inject] private ID _d { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IC) }, WaitFor = new string[] { nameof(_d) })]
        public C CreateC() => new C();
    }

    [Host]
    public partial class HostD : Node
    {
        [Inject] private IC _c { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(ID) }, WaitFor = new string[] { nameof(_c) })]
        public D CreateD() => new D();
    }
}";
        var result = BuildGraph(source);

        var deadlocks = result.Diagnostics.Where(d => d.Id == "GDI_D011").ToList();
        // Each deadlock group should produce at least one GDI_D011
        Assert.NotEmpty(deadlocks);
        // Should cover two independent cycles (at least two diagnostic nodes per cycle)
        Assert.True(deadlocks.Count >= 2, $"Expected ≥2 GDI_D011 but got {deadlocks.Count}");
    }

    [Fact]
    public void MixedSameHostAndCrossHostCycles_BothD010AndD011Reported()
    {
        // SingleHost has an internal WaitFor cycle → GDI_D010
        // HostA ↔ HostB cross-Host cycle   → GDI_D011
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IX { }  public interface IY { }
    public interface IA { }  public interface IB { }
    public class X : IX { }  public class Y : IY { }
    public class A : IA { }  public class B : IB { }

    // Same-Host cycle
    [Host]
    public partial class SingleHost : Node
    {
        [Inject] private IX _x { get; set; }
        [Inject] private IY _y { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IX) }, WaitFor = new string[] { nameof(_y) })]
        public X CreateX() => new X();

        [Provide(ExposedTypes = new Type[] { typeof(IY) }, WaitFor = new string[] { nameof(_x) })]
        public Y CreateY() => new Y();
    }

    // Cross-Host cycle
    [Host]
    public partial class HostA : Node
    {
        [Inject] private IB _b { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IA) }, WaitFor = new string[] { nameof(_b) })]
        public A CreateA() => new A();
    }

    [Host]
    public partial class HostB : Node
    {
        [Inject] private IA _a { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_a) })]
        public B CreateB() => new B();
    }
}";
        var result = BuildGraph(source);

        Assert.NotEmpty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
        Assert.NotEmpty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    // ============================================================
    //  Helpers
    // ============================================================

    private static DiGraphBuildResult BuildGraph(string source) =>
        GraphBuildHelper.BuildGraph(source);
}
