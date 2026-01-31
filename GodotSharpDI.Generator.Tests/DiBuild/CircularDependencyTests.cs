using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.DiBuild;
using GodotSharpDI.Generator.Internal.Helpers;
using GodotSharpDI.Generator.Internal.Semantic;
using GodotSharpDI.Generator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.Generator.Tests.DiBuild;

/// <summary>
/// 循环依赖检测器的全面测试
/// </summary>
public class CircularDependencyDetectorTests
{
    [Fact]
    public void Detect_SimpleCircle_AB_ReportsCorrectCycle()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IA { }
    public interface IB { }

    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IB b) { }
    }

    [Singleton(typeof(IB))]
    public partial class B : IB
    {
        public B(IA a) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Single(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
        var diagnostic = result.Diagnostics.First(d => d.Id == "GDI_D010");
        var message = diagnostic.GetMessage();

        // 应包含完整循环路径
        Assert.Contains("A", message);
        Assert.Contains("B", message);
        Assert.Contains("->", message);
    }

    [Fact]
    public void Detect_ThreeNodeCircle_ABC_ReportsCorrectCycle()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }

    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IB b) { }
    }

    [Singleton(typeof(IB))]
    public partial class B : IB
    {
        public B(IC c) { }
    }

    [Singleton(typeof(IC))]
    public partial class C : IC
    {
        public C(IA a) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Single(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
        var diagnostic = result.Diagnostics.First(d => d.Id == "GDI_D010");
        var message = diagnostic.GetMessage();

        Assert.Contains("A", message);
        Assert.Contains("B", message);
        Assert.Contains("C", message);
    }

    [Fact]
    public void Detect_SelfCircle_ReportsCorrectly()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IA { }

    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IA a) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Single(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
        var diagnostic = result.Diagnostics.First(d => d.Id == "GDI_D010");
        var message = diagnostic.GetMessage();

        // 自环应该显示为 A -> A
        Assert.Contains("A", message);
        Assert.Contains("->", message);
    }

    [Fact]
    public void Detect_MultipleSeparateCircles_ReportsAllCircles()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    // Circle 1: A <-> B
    public interface IA { }
    public interface IB { }

    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IB b) { }
    }

    [Singleton(typeof(IB))]
    public partial class B : IB
    {
        public B(IA a) { }
    }

    // Circle 2: C <-> D
    public interface IC { }
    public interface ID { }

    [Singleton(typeof(IC))]
    public partial class C : IC
    {
        public C(ID d) { }
    }

    [Singleton(typeof(ID))]
    public partial class D : ID
    {
        public D(IC c) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert - 应该检测到两个独立的循环
        var circularDiagnostics = result.Diagnostics.Where(d => d.Id == "GDI_D010").ToList();
        Assert.Equal(2, circularDiagnostics.Count);
    }

    [Fact]
    public void Detect_ComplexGraphWithOneCircle_OnlyReportsCircle()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }
    public interface ID { }

    // 无循环部分
    [Singleton(typeof(ID))]
    public partial class D : ID
    {
        public D() { }
    }

    [Singleton(typeof(IC))]
    public partial class C : IC
    {
        public C(ID d) { }
    }

    // 有循环部分: A <-> B
    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IB b, IC c) { }
    }

    [Singleton(typeof(IB))]
    public partial class B : IB
    {
        public B(IA a) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert - 只有 A 和 B 形成循环
        var circularDiagnostics = result.Diagnostics.Where(d => d.Id == "GDI_D010").ToList();
        Assert.Single(circularDiagnostics);

        var message = circularDiagnostics[0].GetMessage();
        Assert.Contains("A", message);
        Assert.Contains("B", message);
        Assert.DoesNotContain("C", message);
        Assert.DoesNotContain("D", message);
    }

    [Fact]
    public void Detect_DiamondDependency_NoCircle_NoError()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }
    public interface ID { }

    // Diamond: A depends on B and C, both depend on D
    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IB b, IC c) { }
    }

    [Singleton(typeof(IB))]
    public partial class B : IB
    {
        public B(ID d) { }
    }

    [Singleton(typeof(IC))]
    public partial class C : IC
    {
        public C(ID d) { }
    }

    [Singleton(typeof(ID))]
    public partial class D : ID
    {
        public D() { }
    }
}
";
        var result = BuildGraph(source);

        // Assert - 钻石依赖不是循环
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    [Fact]
    public void Detect_LongChain_NoCircle_NoError()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }
    public interface ID { }
    public interface IE { }

    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IB b) { }
    }

    [Singleton(typeof(IB))]
    public partial class B : IB
    {
        public B(IC c) { }
    }

    [Singleton(typeof(IC))]
    public partial class C : IC
    {
        public C(ID d) { }
    }

    [Singleton(typeof(ID))]
    public partial class D : ID
    {
        public D(IE e) { }
    }

    [Singleton(typeof(IE))]
    public partial class E : IE
    {
        public E() { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    [Fact]
    public void Detect_CircleInLargeGraph_DetectsCorrectly()
    {
        // Arrange - 大型依赖图中嵌入循环
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface I1 { }
    public interface I2 { }
    public interface I3 { }
    public interface I4 { }
    public interface I5 { }
    public interface I6 { }
    public interface I7 { }

    [Singleton(typeof(I1))]
    public partial class S1 : I1 { public S1() { } }

    [Singleton(typeof(I2))]
    public partial class S2 : I2 { public S2(I1 s1) { } }

    [Singleton(typeof(I3))]
    public partial class S3 : I3 { public S3(I2 s2) { } }

    // Circle starts: S4 -> S5 -> S6 -> S4
    [Singleton(typeof(I4))]
    public partial class S4 : I4 { public S4(I5 s5) { } }

    [Singleton(typeof(I5))]
    public partial class S5 : I5 { public S5(I6 s6) { } }

    [Singleton(typeof(I6))]
    public partial class S6 : I6 { public S6(I4 s4) { } }

    [Singleton(typeof(I7))]
    public partial class S7 : I7 { public S7(I3 s3) { } }
}
";
        var result = BuildGraph(source);

        // Assert
        var circularDiagnostics = result.Diagnostics.Where(d => d.Id == "GDI_D010").ToList();
        Assert.Single(circularDiagnostics);

        var message = circularDiagnostics[0].GetMessage();
        Assert.Contains("S4", message);
        Assert.Contains("S5", message);
        Assert.Contains("S6", message);
    }

    [Fact]
    public void Detect_PerformanceTest_LargeGraphWithoutCircles()
    {
        // Arrange - 生成大型依赖图（100个节点的链）
        var sourceBuilder = new System.Text.StringBuilder();
        sourceBuilder.AppendLine("using GodotSharpDI.Abstractions;");
        sourceBuilder.AppendLine("namespace Test {");

        const int nodeCount = 100;

        // 生成接口
        for (int i = 0; i < nodeCount; i++)
        {
            sourceBuilder.AppendLine($"public interface I{i} {{ }}");
        }

        // 生成服务（形成链式依赖）
        for (int i = 0; i < nodeCount; i++)
        {
            sourceBuilder.AppendLine($"[Singleton(typeof(I{i}))]");
            sourceBuilder.AppendLine($"public partial class S{i} : I{i} {{");

            if (i < nodeCount - 1)
            {
                sourceBuilder.AppendLine($"public S{i}(I{i + 1} next) {{ }}");
            }
            else
            {
                sourceBuilder.AppendLine($"public S{i}() {{ }}");
            }

            sourceBuilder.AppendLine("}");
        }

        sourceBuilder.AppendLine("}");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = BuildGraph(sourceBuilder.ToString());
        stopwatch.Stop();

        // Assert - 应该在合理时间内完成（< 1秒）
        Assert.True(
            stopwatch.ElapsedMilliseconds < 1000,
            $"Detection took too long: {stopwatch.ElapsedMilliseconds}ms"
        );
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    private static DiGraphBuildResult BuildGraph(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var symbols = new CachedSymbols(compilation);

        var classResults = ImmutableArray.CreateBuilder<ClassValidationResult>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDecls)
            {
                var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
                if (raw.Info != null)
                {
                    var result = ClassPipeline.ValidateAndClassify(raw.Info, symbols);
                    classResults.Add(result);
                }
            }
        }

        return DiGraphBuilder.Build(classResults.ToImmutable(), symbols);
    }
}
