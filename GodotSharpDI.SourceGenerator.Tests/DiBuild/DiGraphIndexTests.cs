using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

/// <summary>
/// DiGraph 索引的正确性测试
///
/// 测试重点：
/// - HostNodeMap（Host 类型 → TypeNode）的完整性和准确性
/// - UserNodes 是否被正确分类（不在 HostNodeMap 中）
/// - ProvidedServices 的服务类型追踪
/// </summary>
public class DiGraphIndexTests
{
    [Fact]
    public void HostNodeMap_ContainsAllHostNodes()
    {
        var source =
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IA { } public interface IB { } public interface IC { }
    public class A : IA { } public class B : IB { } public class C : IC { }
    [Host] public partial class HostA : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IA) })] public A CreateA() => new A();
    }
    [Host] public partial class HostB : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IB) })] public B CreateB() => new B();
    }
    [Host] public partial class HostC : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IC) })] public C CreateC() => new C();
    }
}";
        var graph = BuildGraph(source).Graph;
        Assert.NotNull(graph);
        Assert.Equal(3, graph!.HostNodes.Length);
        Assert.Equal(3, graph.HostNodeMap.Count);

        foreach (var node in graph.HostNodes)
        {
            Assert.True(
                graph.HostNodeMap.TryGetValue(node.ValidatedTypeInfo.Symbol, out var mapped)
            );
            Assert.Same(node, mapped);
        }
    }

    [Fact]
    public void UserNodes_NotInHostNodeMap()
    {
        var source =
            @"
using GodotSharpDI.Abstractions; using Godot;
namespace Test {
    public interface IFoo { }
    [User] public partial class UserA : Node {
        [Inject] private IFoo _foo;
    }
    [User] public partial class UserB : Node {
        [Inject] private IFoo _bar;
    }
}";
        var graph = BuildGraph(source).Graph;
        Assert.NotNull(graph);
        Assert.Equal(2, graph!.UserNodes.Length);
        // User 节点不应存在于 HostNodeMap 中
        foreach (var userNode in graph.UserNodes)
            Assert.False(graph.HostNodeMap.ContainsKey(userNode.ValidatedTypeInfo.Symbol));
    }

    [Fact]
    public void HostNode_ProvidedServices_ReflectsExposedTypes()
    {
        var source =
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IServiceA { } public interface IServiceB { }
    [Host] public partial class MyHost : Node, IServiceA, IServiceB {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA), typeof(IServiceB) })]
        public MyHost GetSelf() => this;
    }
}";
        var graph = BuildGraph(source).Graph;
        Assert.NotNull(graph);

        var hostNode = graph!.HostNodes.FirstOrDefault();
        Assert.NotNull(hostNode);
        Assert.Equal(2, hostNode!.ProvidedServices.Length);

        var serviceNames = hostNode.ProvidedServices.Select(s => s.Name).ToList();
        Assert.Contains("IServiceA", serviceNames);
        Assert.Contains("IServiceB", serviceNames);
    }

    [Fact]
    public void HostNodeMap_SymbolEqualityComparison_WorksAcrossCompilation()
    {
        var source =
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IServiceA { } public class ImplA : IServiceA { }
    [Host] public partial class HostA : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) })]
        public ImplA Create() => new ImplA();
    }
}";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var graph = BuildGraphFromCompilation(compilation).Graph;
        Assert.NotNull(graph);

        // 获取同一编译中的符号，验证 SymbolEqualityComparer 工作正常
        var hostASymbol = compilation.GetTypeByMetadataName("Test.HostA");
        Assert.NotNull(hostASymbol);

        Assert.True(graph!.HostNodeMap.ContainsKey(hostASymbol!));
    }

    [Fact]
    public void MixedDiTypes_CorrectlySegregatedIntoNodes()
    {
        var source =
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IService { }
    public class Impl : IService { }
    [Host] public partial class MyHost : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IService) })]
        public Impl Create() => new Impl();
    }
    [User] public partial class UserA : Node {
        [Inject] private IService _s;
    }
    [User] public partial class UserB : Node {
        [Inject] private IService _s;
    }
}";
        var graph = BuildGraph(source).Graph;
        Assert.NotNull(graph);
        Assert.Equal(1, graph!.HostNodes.Length);
        Assert.Equal(2, graph.UserNodes.Length);
        Assert.Equal(1, graph.HostNodeMap.Count);
    }

    [Fact]
    public void HostWithWaitFor_DependencyEdges_Recorded()
    {
        var source =
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IA { } public interface IB { }
    public class A : IA { } public class B : IB { }
    [Host] public partial class MyHost : Node {
        [Inject] private IA _a { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_a) })]
        public B CreateB() => new B();
        [Provide(ExposedTypes = new Type[] { typeof(IA) })]
        public A CreateA() => new A();
    }
}";
        var graph = BuildGraph(source).Graph;
        Assert.NotNull(graph);
        var hostNode = graph!.HostNodes.FirstOrDefault();
        Assert.NotNull(hostNode);
        // 有 WaitFor 的成员应产生 DependencyEdge
        Assert.NotEmpty(hostNode!.Dependencies);
    }

    // ============================================================
    //  辅助
    // ============================================================

    private static DiGraphBuildResult BuildGraph(string source) =>
        BuildGraphFromCompilation(TestCompilationHelper.CreateCompilationWithDI(source));

    private static DiGraphBuildResult BuildGraphFromCompilation(
        Microsoft.CodeAnalysis.Compilation compilation
    )
    {
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
