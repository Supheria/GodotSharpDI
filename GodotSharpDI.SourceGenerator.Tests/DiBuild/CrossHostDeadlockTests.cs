using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

/// <summary>
/// 测试 P1 修复：跨 Host WaitFor 死锁编译期检测（GDI_D011）
///
/// 场景：
///   HostA 提供 IServiceA，且其 WaitFor 等待 IServiceB 注入
///   HostB 提供 IServiceB，且其 WaitFor 等待 IServiceA 注入
///   → IServiceA → IServiceB → IServiceA 形成跨 Host 循环等待
///   → GDI_D011（编译期发出 Error）
///
/// GDI_D010 vs GDI_D011：
///   D010 - 同一 Host 内的 WaitFor 环（单机死锁）
///   D011 - 跨不同 Host 的 WaitFor 环（分布式死锁）
/// </summary>
public class CrossHostDeadlockTests
{
    // ============================================================
    //  应触发 GDI_D011 的场景
    // ============================================================

    [Fact]
    public void TwoHosts_WaitForEachOther_ReportsDeadlock()
    {
        // HostA provides IServiceA, waits for IServiceB
        // HostB provides IServiceB, waits for IServiceA
        // → 跨 Host 死锁
        var source = @"
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
        // 应包含两个服务名
        Assert.True(
            msg.Contains("IServiceA") || msg.Contains("IServiceB"),
            $"Expected IServiceA or IServiceB in message: {msg}");
    }

    [Fact]
    public void ThreeHosts_CircularWait_ReportsDeadlock()
    {
        // HostA provides IA, waits for IB
        // HostB provides IB, waits for IC
        // HostC provides IC, waits for IA
        // → 三节点跨 Host 环
        var source = @"
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
    //  不应触发 GDI_D011 的场景
    // ============================================================

    [Fact]
    public void TwoHosts_OneDirectional_WaitForOnly_NoCycle()
    {
        // HostA provides IA (no WaitFor)
        // HostB provides IB, waits for IA
        // → 单向等待，无环
        var source = @"
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
        // → 钻石形无环图
        var source = @"
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
        // 同一 Host 内的循环 → GDI_D010（非 GDI_D011）
        var source = @"
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
        // 同一 Host 内的环不触发 GDI_D011（跨 Host 死锁检测器不报告单节点 SCC）
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    [Fact]
    public void HostA_WaitsForUnregisteredService_NoDeadlock()
    {
        // HostA 等待 IUnregistered，但没有任何 Host 提供它
        // 这是运行时问题，不是编译期死锁
        var source = @"
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
        // 等待一个没有提供者的服务不构成死锁（CrossHostDeadlockDetector 只分析有提供者的服务）
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    [Fact]
    public void TwoIndependentCycles_BothReported()
    {
        // 两组互不相关的跨 Host 死锁同时存在 → 两组都应报告 GDI_D011
        // 组1：HostA(IA) ↔ HostB(IB)
        // 组2：HostC(IC) ↔ HostD(ID)
        var source = @"
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
        // 两组死锁应各自产生至少一个 GDI_D011
        Assert.NotEmpty(deadlocks);
        // 应覆盖两个独立环（每个环至少两个诊断节点）
        Assert.True(deadlocks.Count >= 2, $"Expected ≥2 GDI_D011 but got {deadlocks.Count}");
    }

    [Fact]
    public void MixedSameHostAndCrossHostCycles_BothD010AndD011Reported()
    {
        // SingleHost 内部有 WaitFor 循环 → GDI_D010
        // HostA ↔ HostB 跨 Host 循环   → GDI_D011
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IX { }  public interface IY { }
    public interface IA { }  public interface IB { }
    public class X : IX { }  public class Y : IY { }
    public class A : IA { }  public class B : IB { }

    // 同 Host 内循环
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

    // 跨 Host 循环
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
    //  辅助
    // ============================================================

    private static DiGraphBuildResult BuildGraph(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var symbols = new CachedSymbols(compilation);
        var classResults = ImmutableArray.CreateBuilder<ClassValidationResult>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
                if (raw.Info != null)
                    classResults.Add(ClassPipeline.ValidateAndClassify(raw.Info, symbols));
            }
        }

        return DiGraphBuilder.Build(classResults.ToImmutable(), symbols);
    }
}
