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
/// WaitFor semantic validation tests (GDI_M080 / GDI_M081)
///
/// This file only covers WaitFor field reference validity scenarios.
/// Circular dependency detection (GDI_D010) is covered by CircularDependencyTests.cs.
/// Cross-Host deadlock (GDI_D011) is covered by CrossHostDeadlockTests.cs.
/// </summary>
public class WaitForValidationTests
{
    // ============================================================
    //  GDI_M080 — WaitFor references a non-existent field
    // ============================================================

    [Fact]
    public void WaitFor_ReferencesNonExistentField_ReportsGDI_M080()
    {
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
    public partial class ServiceHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { ""_nonExistent"" })]
        public ServiceA CreateA() => new ServiceA();
    }
}
";
        var diags = BuildAllDiagnostics(source);

        var errors = diags.Where(d => d.Id == "GDI_M080").ToList();
        Assert.NotEmpty(errors);
        // Error message should contain the non-existent field name
        Assert.Contains(errors, d => d.GetMessage().Contains("_nonExistent"));
    }

    [Fact]
    public void WaitFor_ReferencesExistingInjectField_NoGDI_M080()
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
    public class ServiceB : IServiceB { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject]
        private IServiceA _serviceA { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_serviceA) })]
        public ServiceB CreateB() => new ServiceB();
    }
}
";
        var diags = BuildAllDiagnostics(source);
        Assert.Empty(diags.Where(d => d.Id == "GDI_M080"));
    }

    // ============================================================
    //  GDI_M081 — WaitFor references a field without [Inject] (Warning)
    // ============================================================

    [Fact]
    public void WaitFor_ReferencesProvideField_ReportsGDI_M081()
    {
        // GDI_M081 triggering conditions in WaitForValidator:
        //   Field exists in _members list (has [Inject] or [Provide] attribute),
        //   but IsInjectMember == false (i.e., the field is a [Provide] member, not [Inject])
        // Note: Fields without any DI attribute are not in _members, and trigger GDI_M080 (not found) instead of GDI_M081
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
    public partial class ServiceHost : Node
    {
        // This is a [Provide] member (IsInjectMember = false)
        // WaitFor referencing it triggers GDI_M081 (referenced non-[Inject] field)
        [Provide(ExposedTypes = new Type[] { })]
        public ServiceA AnotherProvide => null!;

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(AnotherProvide) })]
        public ServiceA CreateA() => new ServiceA();
    }
}
";
        var diags = BuildAllDiagnostics(source);

        var warnings = diags.Where(d => d.Id == "GDI_M081").ToList();
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.GetMessage().Contains("AnotherProvide"));
    }

    [Fact]
    public void WaitFor_ReferencesInjectField_NoGDI_M081()
    {
        // Normal usage: WaitFor field has [Inject] → should not produce GDI_M081
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
        [Inject]
        private IConfig _config { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IConfig) })]
        public Config CreateConfig() => new Config();

        [Provide(ExposedTypes = new Type[] { typeof(IEngine) }, WaitFor = new string[] { nameof(_config) })]
        public Engine CreateEngine() => new Engine();
    }
}
";
        var diags = BuildAllDiagnostics(source);
        Assert.Empty(diags.Where(d => d.Id == "GDI_M081"));
    }

    // ============================================================
    //  Edge cases: multiple WaitFor entries, partially valid
    // ============================================================

    [Fact]
    public void WaitFor_MultipleEntries_OneNonExistent_ReportsSingleM080()
    {
        // nameof(_valid) is valid, "_ghost" does not exist → only reports _ghost
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public class B : IB { }

    [Host]
    public partial class MyHost : Node
    {
        [Inject]
        private IA _valid { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IB) }, WaitFor = new string[] { nameof(_valid), ""_ghost"" })]
        public B CreateB() => new B();
    }
}
";
        var diags = BuildAllDiagnostics(source);

        var m080 = diags.Where(d => d.Id == "GDI_M080").ToList();
        Assert.Single(m080);
        Assert.Contains("_ghost", m080[0].GetMessage());
    }

    // ============================================================
    //  Helpers — merge class-level and graph-level diagnostics
    // ============================================================

    /// <summary>
    /// Builds the graph and returns all diagnostics (class-level + graph-level).
    /// GDI_M080/M081 come from ClassValidationResult, GDI_D010/D011 come from graph validation.
    /// Both need to be merged to assert in a single result.
    /// </summary>
    private static ImmutableArray<Diagnostic> BuildAllDiagnostics(string source)
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

        var graphResult = DiGraphBuilder.Build(classResults.ToImmutable(), symbols);

        // Merge: class-level diagnostics (GDI_M0xx) + graph-level diagnostics (GDI_D0xx)
        return classResults
            .SelectMany(r => r.Diagnostics)
            .Concat(graphResult.Diagnostics)
            .ToImmutableArray();
    }

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
