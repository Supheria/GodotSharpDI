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
/// 循环依赖检测器的全面测试（[Host]+[Provide]+[Inject]+WaitFor 架构）
///
/// 当前架构说明：
/// - DI 图只包含 [Host]、[User]、[Modules] 三种节点
/// - 服务通过 [Host] 上的 [Provide] 成员暴露
/// - 循环依赖（GDI_D010）发生在 WaitFor 形成的等待链上
/// </summary>
public class CircularDependencyTests
{
    // ============================================================
    //  同一 Host 内的 WaitFor 循环（GDI_D010）
    // ============================================================

    [Fact]
    public void Detect_SameHost_TwoProvide_WaitForEachOther_ReportsCycle()
    {
        // A 等待 B 注入，B 等待 A 注入 → 死锁
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
        // A waitFor B, B waitFor C, C waitFor A (三节点环)
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
        // [Provide(ExposedTypes=[IServiceA], WaitFor=[_serviceA])] 自等待
        var source = @"
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
    //  正常场景（不应报告 GDI_D010）
    // ============================================================

    [Fact]
    public void Detect_LinearWaitFor_Chain_NoCycle()
    {
        // A 无等待，C waitFor A，B waitFor C → 正常有向无环图
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
        // B 同时等待 A 和 C，A、C 均无等待 → 无环
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
        // 两个独立 Host 各自无环，且不存在跨 Host 等待
        var source = @"
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
    //  P6 回归测试：防止假阳性（同 Host 提供多个服务但无自环）
    // ============================================================

    [Fact]
    public void Detect_GameManager7Pattern_NoFalsePositiveSelfLoop()
    {
        // Host 提供 IGameState（WaitFor=[_playerStatsService]）
        // 且同时提供 PlayerStatsService3
        // WaitFor 指向另一个 Provide 成员的注入类型 ≠ 自环，不应报 GDI_D010
        var source = @"
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
        // 进一步验证：Host 暴露自身实现类型，WaitFor 指向同 Host 提供的另一服务
        var source = @"
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
    //  性能测试
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

        // 形成链：Service{i} waitFor _s{i-1}，最终无环
        sb.AppendLine("[Provide(ExposedTypes = new Type[] { typeof(IService0) })]");
        sb.AppendLine("public Service0 Create0() => new Service0();");
        for (int i = 1; i < count; i++)
        {
            sb.AppendLine($"[Provide(ExposedTypes = new Type[] {{ typeof(IService{i}) }}, WaitFor = new string[] {{ nameof(_s{i - 1}) }})]");
            sb.AppendLine($"public Service{i} Create{i}() => new Service{i}();");
        }

        sb.AppendLine("}");
        sb.AppendLine("}");

        var sw = Stopwatch.StartNew();
        var result = BuildGraph(sb.ToString());
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Detection took too long: {sw.ElapsedMilliseconds}ms");
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
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
