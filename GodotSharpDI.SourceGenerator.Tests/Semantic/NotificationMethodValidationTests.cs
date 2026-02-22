using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

/// <summary>
/// _Notification 方法的存在性与签名验证测试（GDI_C060 / GDI_C061）
///
/// 所有 DI 类型（[Host] / [User] / [Modules]）必须声明：
///   public override partial void _Notification(int what);
/// 否则生成器无法输出生命周期桩代码。
/// </summary>
public sealed class NotificationMethodValidationTests
{
    // ============================================================
    //  [Host] 场景
    // ============================================================

    [Fact]
    public void Host_WithoutNotificationMethod_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
public partial class TestHost : Node, ITestService
{
    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C060" && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void Host_WithNotificationMethod_NoDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
public partial class TestHost : Node, ITestService
{
    public override partial void _Notification(int what);

    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  [User] 场景
    // ============================================================

    [Fact]
    public void User_WithoutNotificationMethod_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[User]
public partial class TestUser : Node
{
    [Inject]
    private ITestService _service = null!;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C060" && d.GetMessage().Contains("TestUser")
        );
    }

    [Fact]
    public void User_WithNotificationMethod_NoDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[User]
public partial class TestUser : Node
{
    public override partial void _Notification(int what);

    [Inject]
    private ITestService _service = null!;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  [Modules] (Scope) 场景
    // ============================================================

    [Fact]
    public void Scope_WithoutNotificationMethod_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Modules]
public partial class TestScope : Node, IScope
{
    public void RegisterService<T>(T instance) where T : notnull { }
    public void UnregisterService<T>() where T : notnull { }
    public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C060" && d.GetMessage().Contains("TestScope")
        );
    }

    [Fact]
    public void Scope_WithNotificationMethod_NoDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Modules]
public partial class TestScope : Node, IScope
{
    public override partial void _Notification(int what);

    public void RegisterService<T>(T instance) where T : notnull { }
    public void UnregisterService<T>() where T : notnull { }
    public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  签名错误场景（GDI_C061）
    // ============================================================

    [Fact]
    public void NotificationMethod_MissingPublic_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
public partial class TestHost : Node, ITestService
{
    private override partial void _Notification(int what);

    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C061" && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void NotificationMethod_MissingOverride_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
public partial class TestHost : Node, ITestService
{
    public partial void _Notification(int what);

    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C061" && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void NotificationMethod_MissingPartial_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
public partial class TestHost : Node, ITestService
{
    public override void _Notification(int what) { }

    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        // 非 partial 定义版本应触发缺失或签名错误
        Assert.Contains(
            diagnostics,
            d => (d.Id == "GDI_C060" || d.Id == "GDI_C061") && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void NotificationMethod_WrongParameterType_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
public partial class TestHost : Node, ITestService
{
    public override partial void _Notification(long what);

    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C060" && d.GetMessage().Contains("TestHost")
        );
    }

    // ============================================================
    //  组合场景：[Host] + [User] 同一类
    // ============================================================

    [Fact]
    public void HostAndUser_WithoutNotificationMethod_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
[User]
public partial class TestHostUser : Node, ITestService
{
    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;

    [Inject]
    private IAnotherService _service = null!;
}

public interface ITestService { }
public interface IAnotherService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C060" && d.GetMessage().Contains("TestHostUser")
        );
    }

    [Fact]
    public void HostAndUser_WithNotificationMethod_NoDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;
using System;

[Host]
[User]
public partial class TestHostUser : Node, ITestService
{
    public override partial void _Notification(int what);

    [Provide(ExposedTypes = new Type[] { typeof(ITestService) })]
    private ITestService Self => this;

    [Inject]
    private IAnotherService _service = null!;
}

public interface ITestService { }
public interface IAnotherService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  生成代码验证
    // ============================================================

    [Fact]
    public void NotificationMethod_WithCorrectSignature_GeneratesImplementation()
    {
        // 验证：_Notification 签名正确时，生成器接受该类并产生 DI 文件
        // 注：GDI_D050（无 Provider）是图验证级错误，与 _Notification 签名无关
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[User]
public partial class TestUser : Node
{
    public override partial void _Notification(int what);

    [Inject]
    private ITestService _service = null!;
}

public interface ITestService { }
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);

        // 核心断言：正确的 _Notification 签名不应触发 GDI_C060/C081
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);
        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C061");

        // 生成器应为该类产生 DI 文件
        var sources = TestCompilationHelper.GetGeneratedSources(compilation);
        Assert.Contains(sources, s => s.HintName.Contains("TestUser") && s.HintName.Contains("DI"));
    }
}
