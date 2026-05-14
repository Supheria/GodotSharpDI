using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

/// <summary>
/// Comprehensive tests for circular dependency detector ([Host]+[Provide]+[Inject]+WaitFor architecture)
///
/// Current architecture notes:
/// - DI graph only contains [Host], [User], [Modules] three types of nodes
/// - Services are exposed through [Host]'s [Provide] members
/// - Circular dependencies (GDI_D010) occur on wait chains formed by WaitFor
/// </summary>
public class CircularDependencyTests
{
    // ============================================================
    //  WaitFor cycles within the same Host (GDI_D010)
    // ============================================================

    [Fact]
    public void Detect_SameHost_TwoProvide_WaitForEachOther_ReportsCycle()
    {
        // A waits for B injection, B waits for A injection → deadlock
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
    public partial class MyHost : Node
    {
        [Inject] private IServiceA _serviceA { get; set; }
        [Inject] private IServiceB _serviceB { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_serviceB) })]
        public ServiceA CreateA() => new ServiceA();

        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_serviceA) })]
        public ServiceB CreateB() => new ServiceB();
    }
}";
        var result = BuildGraph(source);

        var cycleDiags = result.Diagnostics.Where(d => d.Id == "GDI_D010").ToList();
        Assert.NotEmpty(cycleDiags);
        var msg = cycleDiags[0].GetMessage();
        Assert.Contains("->", msg);
    }

    [Fact]
    public void Detect_SameHost_ThreeProvide_WaitForChain_ReportsCycle()
    {
        // A waitFor B, B waitFor C, C waitFor A (three-node cycle)
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
    public partial class MyHost : Node
    {
        [Inject] private IA _a { get; set; }
        [Inject] private IB _b { get; set; }
        [Inject] private IC _c { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IA) }, WaitFor = new string[] { nameof(_b) })]
        public SA CreateA() => new SA();

        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_c) })]
        public SB CreateB() => new SB();

        [Provide(ExposedTypes = new Type[] { typeof(IC) }, WaitFor = new string[] { nameof(_a) })]
        public SC CreateC() => new SC();
    }
}";
        var result = BuildGraph(source);
        Assert.NotEmpty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    [Fact]
    public void Detect_SameHost_DirectSelfWaitFor_ReportsCycle()
    {
        // [Provide(ExposedTypes=[IServiceA], WaitFor=[_serviceA])] self-wait
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public class ServiceA : IServiceA { }

    [Host]
    public partial class MyHost : Node
    {
        [Inject] private IServiceA _serviceA { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_serviceA) })]
        public ServiceA CreateA() => new ServiceA();
    }
}";
        var result = BuildGraph(source);
        Assert.NotEmpty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    // ============================================================
    //  Normal scenarios (should not report GDI_D010)
    // ============================================================

    [Fact]
    public void Detect_LinearWaitFor_Chain_NoCycle()
    {
        // A has no wait, C waitFor A, B waitFor C → normal directed acyclic graph
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
    public partial class MyHost : Node
    {
        [Inject] private IA _a { get; set; }
        [Inject] private IC _c { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IA) })]
        public SA CreateA() => new SA();

        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_c) })]
        public SB CreateB() => new SB();

        [Provide(ExposedTypes = new Type[] { typeof(IC) }, WaitFor = new string[] { nameof(_a) })]
        public SC CreateC() => new SC();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    [Fact]
    public void Detect_MultipleWaitFor_NoCycle()
    {
        // B simultaneously waits for A and C, A and C have no wait → no cycle
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
    public partial class MyHost : Node
    {
        [Inject] private IA _a { get; set; }
        [Inject] private IC _c { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IA) })]
        public SA CreateA() => new SA();

        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_a), nameof(_c) })]
        public SB CreateB() => new SB();

        [Provide(ExposedTypes = new Type[] { typeof(IC) })]
        public SC CreateC() => new SC();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    [Fact]
    public void Detect_MultipleIndependentHosts_NoCycle()
    {
        // Two independent Hosts each have no cycle, and no cross-Host wait exists
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceX { }
    public interface IServiceY { }
    public class ServiceX : IServiceX { }
    public class ServiceY : IServiceY { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceX) })]
        public ServiceX CreateX() => new ServiceX();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceY) })]
        public ServiceY CreateY() => new ServiceY();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D011"));
    }

    // ============================================================
    //  P6 regression test: prevent false positives (same Host provides multiple services but no self-loop)
    // ============================================================

    [Fact]
    public void Detect_GameManager7Pattern_NoFalsePositiveSelfLoop()
    {
        // Host provides IGameState (WaitFor=[_playerStatsService])
        // and also provides PlayerStatsService3
        // WaitFor pointing to another Provide member's injection type ≠ self-loop, should not report GDI_D010
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;
using System.Threading.Tasks;

namespace Test
{
    public interface IGameState { }
    public class PlayerStatsCenter { }
    public class PlayerStatsService3
    {
        public PlayerStatsService3(PlayerStatsCenter c) { }
    }

    [Host]
    public partial class GameManager : Node, IGameState
    {
        [Inject(ReadyCallback = true)]
        private PlayerStatsCenter _playerStatsCenter;

        [Inject]
        private PlayerStatsService3 _playerStatsService;

        [Provide(
            ExposedTypes = new Type[] { typeof(IGameState) },
            WaitFor = new string[] { nameof(_playerStatsService) }
        )]
        public async Task<GameManager> GetSelf()
        {
            return this;
        }

        [Provide(ExposedTypes = new Type[] { typeof(PlayerStatsService3) })]
        public PlayerStatsService3 GetPlayerStatsService3()
        {
            return new PlayerStatsService3(_playerStatsCenter);
        }
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    [Fact]
    public void Detect_HostExposesSelfAndWaitsForAnotherProvide_NoCycle()
    {
        // Further verify: Host exposes its own implementation type, WaitFor points to another service provided by the same Host
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IService1 { }
    public interface IService2 { }
    public class ImplOf1 : IService1 { }

    [Host]
    public partial class HostX : Node, IService2
    {
        [Inject] private IService1 _s1 { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IService2) }, WaitFor = new string[] { nameof(_s1) })]
        public HostX GetSelf() => this;

        [Provide(ExposedTypes = new Type[] { typeof(IService1) })]
        public ImplOf1 CreateService1() => new ImplOf1();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    // ============================================================
    //  Performance tests
    // ============================================================

    [Fact]
    public void Detect_Performance_LargeLinearWaitForChain_CompletesQuickly()
    {
        const int count = 50;
        var sb = new StringBuilder();
        sb.AppendLine("using GodotSharpDI.Abstractions;");
        sb.AppendLine("using Godot;");
        sb.AppendLine("using System;");
        sb.AppendLine("namespace Test {");

        for (int i = 0; i < count; i++)
            sb.AppendLine($"public interface IService{i} {{ }}");
        for (int i = 0; i < count; i++)
            sb.AppendLine($"public class Service{i} : IService{i} {{ }}");

        sb.AppendLine("[Host]");
        sb.AppendLine("public partial class BigHost : Godot.Node {");

        for (int i = 0; i < count; i++)
            sb.AppendLine($"[Inject] private IService{i} _s{i} {{ get; set; }}");

        // Form chain: Service{i} waitFor _s{i-1}, ultimately no cycle
        sb.AppendLine("[Provide(ExposedTypes = new Type[] { typeof(IService0) })]");
        sb.AppendLine("public Service0 Create0() => new Service0();");
        for (int i = 1; i < count; i++)
        {
            sb.AppendLine(
                $"[Provide(ExposedTypes = new Type[] {{ typeof(IService{i}) }}, WaitFor = new string[] {{ nameof(_s{i - 1}) }})]"
            );
            sb.AppendLine($"public Service{i} Create{i}() => new Service{i}();");
        }

        sb.AppendLine("}");
        sb.AppendLine("}");

        var sw = Stopwatch.StartNew();
        var result = BuildGraph(sb.ToString());
        sw.Stop();

        Assert.True(
            sw.ElapsedMilliseconds < 3000,
            $"Detection took too long: {sw.ElapsedMilliseconds}ms"
        );
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    // ============================================================
    //  Helpers
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
