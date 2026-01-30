using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.DiBuild;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Semantic;
using GodotSharp.DI.Generator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharp.DI.Generator.Tests.DiBuild;

/// <summary>
/// 测试 DiGraph 索引的正确性和性能
/// </summary>
public class DiGraphIndexTests
{
    [Fact]
    public void ServiceNodeMap_ContainsAllServiceNodes()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Singleton]
    public partial class ServiceA { }

    [Singleton]
    public partial class ServiceB { }

    [Singleton]
    public partial class ServiceC { }
}
";
        var graph = BuildGraph(source).Graph;

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(3, graph.ServiceNodes.Length);
        Assert.Equal(3, graph.ServiceNodeMap.Count);

        // 验证每个 ServiceNode 都在索引中
        foreach (var node in graph.ServiceNodes)
        {
            Assert.True(
                graph.ServiceNodeMap.TryGetValue(node.TypeInfo.Symbol, out var mappedNode),
                $"ServiceNodeMap should contain {node.TypeInfo.Symbol.Name}"
            );
            Assert.Same(node, mappedNode);
        }
    }

    [Fact]
    public void HostNodeMap_ContainsAllHostNodes()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Host]
    public partial class HostA : Node { }

    [Host]
    public partial class HostB : Node { }
}
";
        var graph = BuildGraph(source).Graph;

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(2, graph.HostNodes.Length);
        Assert.Equal(2, graph.HostNodeMap.Count);

        foreach (var node in graph.HostNodes)
        {
            Assert.True(
                graph.HostNodeMap.TryGetValue(node.TypeInfo.Symbol, out var mappedNode),
                $"HostNodeMap should contain {node.TypeInfo.Symbol.Name}"
            );
            Assert.Same(node, mappedNode);
        }
    }

    [Fact]
    public void HostAndUserNodeMap_ContainsAllHostAndUserNodes()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Host, User]
    public partial class HostAndUserA : Node { }

    [Host, User]
    public partial class HostAndUserB : Node { }
}
";
        var graph = BuildGraph(source).Graph;

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(2, graph.HostAndUserNodes.Length);
        Assert.Equal(2, graph.HostAndUserNodeMap.Count);

        foreach (var node in graph.HostAndUserNodes)
        {
            Assert.True(
                graph.HostAndUserNodeMap.TryGetValue(node.TypeInfo.Symbol, out var mappedNode),
                $"HostAndUserNodeMap should contain {node.TypeInfo.Symbol.Name}"
            );
            Assert.Same(node, mappedNode);
        }
    }

    [Fact]
    public void UserNodes_NotInSpecializedMaps()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [User]
    public partial class UserA : Node { }
}
";
        var graph = BuildGraph(source).Graph;

        // Assert
        Assert.NotNull(graph);
        Assert.Single(graph.UserNodes);

        var userNode = graph.UserNodes[0];

        // User节点不应该在索引中
        Assert.False(graph.ServiceNodeMap.ContainsKey(userNode.TypeInfo.Symbol));
        Assert.False(graph.HostNodeMap.ContainsKey(userNode.TypeInfo.Symbol));
        Assert.False(graph.HostAndUserNodeMap.ContainsKey(userNode.TypeInfo.Symbol));
    }

    [Fact]
    public void TypeMap_ContainsAllNodes()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Singleton]
    public partial class ServiceA { }

    [Host]
    public partial class HostB : Node { }

    [User]
    public partial class UserC : Node { }

    [Host, User]
    public partial class HostAndUserD : Node { }
}
";
        var graph = BuildGraph(source).Graph;

        // Assert
        Assert.NotNull(graph);

        var totalNodes =
            graph.ServiceNodes.Length
            + graph.HostNodes.Length
            + graph.UserNodes.Length
            + graph.HostAndUserNodes.Length;

        Assert.Equal(4, totalNodes);
    }

    [Fact]
    public void ServiceNodeMap_FastLookup_WorksCorrectly()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface IServiceC { }

    [Singleton(typeof(IServiceA))]
    public partial class ServiceA : IServiceA { }

    [Singleton(typeof(IServiceB))]
    public partial class ServiceB : IServiceB { }

    [Singleton(typeof(IServiceC))]
    public partial class ServiceC : IServiceC { }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var graph = BuildGraphFromCompilation(compilation).Graph;

        // Assert
        Assert.NotNull(graph);

        // 获取 ServiceA 的类型符号
        var serviceASymbol = compilation.GetTypeByMetadataName("Test.ServiceA");
        Assert.NotNull(serviceASymbol);

        // 使用索引快速查找
        var found = graph.ServiceNodeMap.TryGetValue(serviceASymbol, out var node);

        Assert.True(found, "Should find ServiceA in ServiceNodeMap");
        Assert.NotNull(node);
        Assert.Equal("ServiceA", node.TypeInfo.Symbol.Name);
    }

    private static DiGraphBuildResult BuildGraph(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        return BuildGraphFromCompilation(compilation);
    }

    private static DiGraphBuildResult BuildGraphFromCompilation(Compilation compilation)
    {
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
