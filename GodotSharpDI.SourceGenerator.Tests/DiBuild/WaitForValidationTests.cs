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
        var source = @"
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
        var result = BuildGraph(source);

        var errors = result.Diagnostics.Where(d => d.Id == "GDI_M080").ToList();
        Assert.NotEmpty(errors);
        // 错误消息应包含不存在的字段名
        Assert.Contains(errors, d => d.GetMessage().Contains("_nonExistent"));
    }

    [Fact]
    public void WaitFor_ReferencesExistingInjectField_NoGDI_M080()
    {
        var source = @"
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
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_M080"));
    }

    // ============================================================
    //  GDI_M081 — WaitFor 引用字段但该字段无 [Inject]（Warning）
    // ============================================================

    [Fact]
    public void WaitFor_ReferencesFieldWithoutInject_ReportsGDI_M081()
    {
        // WaitFor 引用的字段没有 [Inject] 特性 → GDI_M081 Warning
        var source = @"
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
        private IServiceA _notInjected { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_notInjected) })]
        public ServiceA CreateA() => new ServiceA();
    }
}
";
        var result = BuildGraph(source);

        var warnings = result.Diagnostics.Where(d => d.Id == "GDI_M081").ToList();
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.GetMessage().Contains("_notInjected"));
    }

    [Fact]
    public void WaitFor_ReferencesInjectField_NoGDI_M081()
    {
        // 正常使用：WaitFor 字段有 [Inject] → 不应产生 GDI_M081
        var source = @"
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
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_M081"));
    }

    // ============================================================
    //  边界场景：多个 WaitFor 部分合法
    // ============================================================

    [Fact]
    public void WaitFor_MultipleEntries_OneNonExistent_ReportsSingleM080()
    {
        // nameof(_valid) 合法，"_ghost" 不存在 → 只报告 _ghost
        var source = @"
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
        var result = BuildGraph(source);

        var m080 = result.Diagnostics.Where(d => d.Id == "GDI_M080").ToList();
        Assert.Single(m080);
        Assert.Contains("_ghost", m080[0].GetMessage());
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
