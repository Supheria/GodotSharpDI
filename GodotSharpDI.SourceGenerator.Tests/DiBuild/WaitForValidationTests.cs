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
/// WaitFor 语义验证测试（GDI_M080 / GDI_M081）
///
/// 本文件只覆盖 WaitFor 字段引用合法性验证场景，
/// 循环依赖检测（GDI_D010）由 CircularDependencyTests.cs 覆盖，
/// 跨 Host 死锁（GDI_D011）由 CrossHostDeadlockTests.cs 覆盖。
/// </summary>
public class WaitForValidationTests
{
    // ============================================================
    //  GDI_M080 — WaitFor 引用不存在的字段
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
        // 错误消息应包含不存在的字段名
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
    //  GDI_M081 — WaitFor 引用字段但该字段无 [Inject]（Warning）
    // ============================================================

    [Fact]
    public void WaitFor_ReferencesProvideField_ReportsGDI_M081()
    {
        // GDI_M081 在 WaitForValidator 中触发的条件：
        //   字段存在于 _members 列表（即有 [Inject] 或 [Provide] 属性），
        //   但 IsInjectMember == false（即该字段是 [Provide] 成员，而非 [Inject]）
        // 注：无任何 DI 属性的字段不在 _members 中，会触发 GDI_M080（找不到）而非 GDI_M081
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
        // 这是一个 [Provide] 成员（IsInjectMember = false）
        // WaitFor 引用它时触发 GDI_M081（引用了非 [Inject] 字段）
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
        // 正常使用：WaitFor 字段有 [Inject] → 不应产生 GDI_M081
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
    //  边界场景：多个 WaitFor 部分合法
    // ============================================================

    [Fact]
    public void WaitFor_MultipleEntries_OneNonExistent_ReportsSingleM080()
    {
        // nameof(_valid) 合法，"_ghost" 不存在 → 只报告 _ghost
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
    //  辅助 — 合并类级别和图级别诊断
    // ============================================================

    /// <summary>
    /// 构建图并返回所有诊断（类级别 + 图级别）。
    /// GDI_M080/M081 来自 ClassValidationResult，GDI_D010/D011 来自图验证，
    /// 两者需要合并才能在同一结果中断言。
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

        // 合并：类级别诊断（GDI_M0xx）+ 图级别诊断（GDI_D0xx）
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
