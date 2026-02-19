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
/// 测试 P2 修复：重复服务注册警告（GDI_D041）
///
/// 当两个或多个 [Host] 通过 [Provide] 暴露同一服务类型时，
/// 生成器发出 GDI_D041 警告，告知开发者第一个注册的 Host 会赢得所有权。
/// </summary>
public class DuplicateServiceRegistrationTests
{
    // ============================================================
    //  应触发 GDI_D041 的场景
    // ============================================================

    [Fact]
    public void TwoHosts_BothProvide_SameInterface_ReportsWarning()
    {
        // Arrange - 两个 Host 都声明提供 IMyService
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class ImplA : IMyService { }
    public class ImplB : IMyService { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public ImplA Create() => new ImplA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public ImplB Create() => new ImplB();
    }
}";
        var result = BuildGraph(source);

        var warnings = result.Diagnostics
            .Where(d => d.Id == "GDI_D041" && d.Severity == DiagnosticSeverity.Warning)
            .ToList();
        Assert.NotEmpty(warnings);
        // 至少有一个警告包含服务类型名
        Assert.Contains(warnings, w => w.GetMessage().Contains("IMyService"));
    }

    [Fact]
    public void ThreeHosts_AllProvide_SameInterface_ReportsWarningWithAllProviders()
    {
        // Arrange - 三个 Host 都提供同一接口
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface ISharedService { }
    public class ImplA : ISharedService { }
    public class ImplB : ISharedService { }
    public class ImplC : ISharedService { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(ISharedService) })]
        public ImplA CreateA() => new ImplA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(ISharedService) })]
        public ImplB CreateB() => new ImplB();
    }

    [Host]
    public partial class HostC : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(ISharedService) })]
        public ImplC CreateC() => new ImplC();
    }
}";
        var result = BuildGraph(source);

        var warnings = result.Diagnostics.Where(d => d.Id == "GDI_D041").ToList();
        Assert.NotEmpty(warnings);
        // 警告消息应包含服务名
        Assert.All(warnings, w => Assert.Contains("ISharedService", w.GetMessage()));
    }

    [Fact]
    public void TwoHosts_PartialOverlap_OnlyDuplicateServiceWarned()
    {
        // Arrange - HostA 提供 IServiceA + ICommon，HostB 提供 IServiceB + ICommon
        //           只有 ICommon 应该触发 GDI_D041
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface ICommon { }
    public class ImplA : IServiceA { }
    public class ImplB : IServiceB { }
    public class ImplCA : ICommon { }
    public class ImplCB : ICommon { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) })]
        public ImplA CreateA() => new ImplA();

        [Provide(ExposedTypes = new Type[] { typeof(ICommon) })]
        public ImplCA CreateCommon() => new ImplCA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) })]
        public ImplB CreateB() => new ImplB();

        [Provide(ExposedTypes = new Type[] { typeof(ICommon) })]
        public ImplCB CreateCommon() => new ImplCB();
    }
}";
        var result = BuildGraph(source);

        var warnings = result.Diagnostics.Where(d => d.Id == "GDI_D041").ToList();
        Assert.NotEmpty(warnings);
        // 只有 ICommon 重复
        Assert.Contains(warnings, w => w.GetMessage().Contains("ICommon"));
        Assert.DoesNotContain(warnings, w => w.GetMessage().Contains("IServiceA"));
        Assert.DoesNotContain(warnings, w => w.GetMessage().Contains("IServiceB"));
    }

    // ============================================================
    //  不应触发 GDI_D041 的场景
    // ============================================================

    [Fact]
    public void OneHost_ProvideService_NoWarning()
    {
        // Arrange - 只有一个 Host 提供该服务
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class Impl : IMyService { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public Impl Create() => new Impl();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D041"));
    }

    [Fact]
    public void TwoHosts_DifferentServices_NoWarning()
    {
        // Arrange - 两个 Host 各提供不同服务，无重复
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceX { }
    public interface IServiceY { }
    public class ImplX : IServiceX { }
    public class ImplY : IServiceY { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceX) })]
        public ImplX CreateX() => new ImplX();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceY) })]
        public ImplY CreateY() => new ImplY();
    }
}";
        var result = BuildGraph(source);
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D041"));
    }

    [Fact]
    public void GDI_D041_IsWarning_NotError_CanBeSuppressed()
    {
        // GDI_D041 应该是 Warning，而不是 Error（允许故意覆盖）
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class ImplA : IMyService { }
    public class ImplB : IMyService { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public ImplA Create() => new ImplA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public ImplB Create() => new ImplB();
    }
}";
        var result = BuildGraph(source);

        var gdi041Diags = result.Diagnostics.Where(d => d.Id == "GDI_D041").ToList();
        Assert.NotEmpty(gdi041Diags);
        // 所有 GDI_D041 均为 Warning，不能是 Error
        Assert.All(gdi041Diags, d =>
            Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
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
