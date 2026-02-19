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
/// DiGraphBuilder 整合测试
///
/// 当前 DiGraph 结构：
/// - HostNodes（[Host] 节点）、UserNodes（[User] 节点）、ScopeNodes（[Modules] 节点）
/// - 服务通过 Host 的 [Provide] 成员暴露，不存在独立 ServiceNode
/// - HostNodeMap = Host 类型符号 → TypeNode 快速索引
/// </summary>
public class DiGraphBuilderTests
{
    [Fact]
    public void Build_EmptyInput_ReturnsEmpty()
    {
        var result = BuildGraph(@"namespace Test { public class NotADiClass { } }");
        Assert.Null(result.Graph);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Build_SingleHost_ReturnsOneHostNode()
    {
        var result = BuildGraph(
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IMyService { }
    public class Impl : IMyService { }
    [Host] public partial class MyHost : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public Impl Create() => new Impl();
    }
}"
        );
        Assert.NotNull(result.Graph);
        Assert.Single(result.Graph!.HostNodes);
        Assert.Equal("MyHost", result.Graph.HostNodes[0].ValidatedTypeInfo.Symbol.Name);
    }

    [Fact]
    public void Build_SingleUser_ReturnsOneUserNode()
    {
        var result = BuildGraph(
            @"
using GodotSharpDI.Abstractions; using Godot;
namespace Test {
    public interface IMyService { }
    [User] public partial class MyUser : Node {
        [Inject] private IMyService _service;
    }
}"
        );
        Assert.NotNull(result.Graph);
        Assert.Single(result.Graph!.UserNodes);
        Assert.Equal("MyUser", result.Graph.UserNodes[0].ValidatedTypeInfo.Symbol.Name);
    }

    [Fact]
    public void Build_MultipleHosts_AllIncluded()
    {
        var result = BuildGraph(
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IA { } public interface IB { }
    public class A : IA { } public class B : IB { }
    [Host] public partial class HostA : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IA) })] public A CreateA() => new A();
    }
    [Host] public partial class HostB : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IB) })] public B CreateB() => new B();
    }
}"
        );
        Assert.NotNull(result.Graph);
        Assert.Equal(2, result.Graph!.HostNodes.Length);
        Assert.Contains(result.Graph.HostNodes, n => n.ValidatedTypeInfo.Symbol.Name == "HostA");
        Assert.Contains(result.Graph.HostNodes, n => n.ValidatedTypeInfo.Symbol.Name == "HostB");
    }

    [Fact]
    public void Build_HostWithMultipleExposedTypes_TracksAll()
    {
        var result = BuildGraph(
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IA { } public interface IB { }
    [Host] public partial class MyHost : Node, IA, IB {
        [Provide(ExposedTypes = new Type[] { typeof(IA), typeof(IB) })]
        public MyHost GetSelf() => this;
    }
}"
        );
        Assert.NotNull(result.Graph);
        var node = result.Graph!.HostNodes.FirstOrDefault();
        Assert.NotNull(node);
        Assert.Equal(2, node!.ProvidedServices.Length);
    }

    [Fact]
    public void Build_ValidGraph_NoErrors()
    {
        var result = BuildGraph(
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IServiceA { } public class ImplA : IServiceA { }
    [Host] public partial class HostA : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) })]
        public ImplA Create() => new ImplA();
    }
    [User] public partial class MyUser : Node {
        [Inject] private IServiceA _service;
    }
}"
        );
        Assert.NotNull(result.Graph);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Build_HostNodeMap_ContainsAllHosts()
    {
        var result = BuildGraph(
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IA { } public interface IB { }
    public class A : IA { } public class B : IB { }
    [Host] public partial class HostA : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IA) })] public A CreateA() => new A();
    }
    [Host] public partial class HostB : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IB) })] public B CreateB() => new B();
    }
}"
        );
        Assert.NotNull(result.Graph);
        Assert.Equal(result.Graph!.HostNodes.Length, result.Graph.HostNodeMap.Count);
        foreach (var node in result.Graph.HostNodes)
        {
            Assert.True(
                result.Graph.HostNodeMap.TryGetValue(node.ValidatedTypeInfo.Symbol, out var mapped)
            );
            Assert.Same(node, mapped);
        }
    }

    [Fact]
    public void Build_HostNodeMap_FastLookupBySymbol()
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
        var result = BuildGraphFromCompilation(compilation);

        Assert.NotNull(result.Graph);
        var symbol = compilation.GetTypeByMetadataName("Test.HostA");
        Assert.NotNull(symbol);

        Assert.True(result.Graph!.HostNodeMap.TryGetValue(symbol!, out var node));
        Assert.Equal("HostA", node!.ValidatedTypeInfo.Symbol.Name);
    }

    [Fact]
    public void Build_DuplicateService_ReportsGDI_D041()
    {
        var result = BuildGraph(
            @"
using GodotSharpDI.Abstractions; using Godot; using System;
namespace Test {
    public interface IMyService { }
    public class ImplA : IMyService { } public class ImplB : IMyService { }
    [Host] public partial class HostA : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })] public ImplA Create() => new ImplA();
    }
    [Host] public partial class HostB : Node {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })] public ImplB Create() => new ImplB();
    }
}"
        );
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_D041");
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
