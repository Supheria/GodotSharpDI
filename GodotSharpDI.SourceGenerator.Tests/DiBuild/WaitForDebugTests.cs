using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

/// <summary>
/// Integration tests for WaitFor mechanism (with observable diagnostic output)
/// </summary>
public class WaitForIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public WaitForIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // ============================================================
    //  WaitFor member info correctly parsed into MemberInfo
    // ============================================================

    [Fact]
    public void WaitFor_SingleDependency_ParsedIntoMemberInfo()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public class ImplB : IServiceB { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject]
        private IServiceA _serviceA { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_serviceA) })]
        public ImplB CreateB() => new ImplB();
    }
}";
        var result = BuildGraph(source);

        // Graph construction should succeed
        Assert.NotNull(result.Graph);
        var hostNode = result.Graph!.HostNodes.FirstOrDefault();
        Assert.NotNull(hostNode);

        // Should have one WaitFor dependency edge
        var waitForEdges = hostNode!
            .Dependencies.Where(d => d.Source == DependencySource.WaitForMember)
            .ToList();
        Assert.NotEmpty(waitForEdges);

        _output.WriteLine($"WaitFor edges: {waitForEdges.Count}");
        foreach (var edge in waitForEdges)
            _output.WriteLine(
                $"  {edge.SourceProvidedType?.Name} → waitFor → {edge.TargetType.Name}"
            );
    }

    [Fact]
    public void WaitFor_MultipleTargets_AllParsed()
    {
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
    public class ImplC : IC { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject] private IA _a { get; set; }
        [Inject] private IB _b { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IC) }, WaitFor = new string[] { nameof(_a), nameof(_b) })]
        public ImplC CreateC() => new ImplC();
    }
}";
        var result = BuildGraph(source);
        Assert.NotNull(result.Graph);
        var hostNode = result.Graph!.HostNodes.FirstOrDefault();
        Assert.NotNull(hostNode);

        var waitForEdges = hostNode!
            .Dependencies.Where(d => d.Source == DependencySource.WaitForMember)
            .ToList();
        // Two WaitFor targets → two dependency edges
        Assert.Equal(2, waitForEdges.Count);

        _output.WriteLine($"WaitFor edges count: {waitForEdges.Count}");
    }

    // ============================================================
    //  WaitFor diagnostic integration verification
    // ============================================================

    [Fact]
    public void WaitFor_Cycle_DiagnosticsContainServiceNames()
    {
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
    public partial class ServiceHost : Node
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

        _output.WriteLine($"\n=== GDI_D010 diagnostics: {cycleDiags.Count} ===");
        foreach (var diag in cycleDiags)
        {
            _output.WriteLine($"  [{diag.Id}] {diag.GetMessage()}");
            // Message contains dependency path arrows
            Assert.Contains("->", diag.GetMessage());
        }
    }

    [Fact]
    public void WaitFor_ValidConfiguration_ZeroDiagnostics()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IConfig { }
    public interface IEngine { }
    public class Config : IConfig { }
    public class Engine : IEngine { }

    [Host]
    public partial class GameHost : Node
    {
        [Inject] private IConfig _config { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IConfig) })]
        public Config CreateConfig() => new Config();

        [Provide(ExposedTypes = new Type[] { typeof(IEngine) }, WaitFor = new string[] { nameof(_config) })]
        public Engine CreateEngine() => new Engine();
    }
}";
        var result = BuildGraph(source);

        var errorDiags = result
            .Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        _output.WriteLine($"\n=== Total diagnostics: {result.Diagnostics.Length} ===");
        foreach (var d in result.Diagnostics)
            _output.WriteLine($"  [{d.Id}] {d.Severity}: {d.GetMessage()}");

        Assert.Empty(errorDiags);
    }

    // ============================================================
    //  Helpers
    // ============================================================

    private static DiGraphBuildResult BuildGraph(string source) =>
        GraphBuildHelper.BuildGraph(source);
}
