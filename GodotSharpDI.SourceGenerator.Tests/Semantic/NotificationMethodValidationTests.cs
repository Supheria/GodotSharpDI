using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

public sealed class NotificationMethodValidationTests
{
    [Fact]
    public void Host_WithoutNotificationMethod_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
public partial class TestHost : Node
{
    [Singleton(typeof(ITestService))]
    private ITestService Self => this;
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C080" && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void Host_WithNotificationMethod_NoDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
public partial class TestHost : Node, ITestService
{
    public override partial void _Notification(int what);
    
    [Singleton(typeof(ITestService))]
    private ITestService Self => this;
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C080");
    }

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
            d => d.Id == "GDI_C080" && d.GetMessage().Contains("TestUser")
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

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C080");
    }

    [Fact]
    public void Scope_WithoutNotificationMethod_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Modules(Services = new[] { typeof(TestService) })]
public partial class TestScope : Node, IScope
{
    public void RegisterService<T>(T instance) where T : notnull { }
    public void UnregisterService<T>() where T : notnull { }
    public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
}

[Singleton(typeof(ITestService))]
public partial class TestService : ITestService
{
    public TestService() { }
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C080" && d.GetMessage().Contains("TestScope")
        );
    }

    [Fact]
    public void Scope_WithNotificationMethod_NoDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Modules(Services = new[] { typeof(TestService) })]
public partial class TestScope : Node, IScope
{
    public override partial void _Notification(int what);
    
    public void RegisterService<T>(T instance) where T : notnull { }
    public void UnregisterService<T>() where T : notnull { }
    public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
}

[Singleton(typeof(ITestService))]
public partial class TestService : ITestService
{
    public TestService() { }
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C080");
    }

    [Fact]
    public void NotificationMethod_MissingPublic_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
public partial class TestHost : Node, ITestService
{
    private override partial void _Notification(int what);
    
    [Singleton(typeof(ITestService))]
    private ITestService Self => this;
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C081" && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void NotificationMethod_MissingOverride_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
public partial class TestHost : Node, ITestService
{
    public partial void _Notification(int what);
    
    [Singleton(typeof(ITestService))]
    private ITestService Self => this;
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C081" && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void NotificationMethod_MissingPartial_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
public partial class TestHost : Node, ITestService
{
    public override void _Notification(int what) { }
    
    [Singleton(typeof(ITestService))]
    private ITestService Self => this;
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        // 如果方法不是 partial definition，验证逻辑会报告缺少方法或签名错误
        Assert.Contains(
            diagnostics,
            d => (d.Id == "GDI_C080" || d.Id == "GDI_C081") && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void NotificationMethod_WrongParameterType_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
public partial class TestHost : Node, ITestService
{
    public override partial void _Notification(long what);
    
    [Singleton(typeof(ITestService))]
    private ITestService Self => this;
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C080" && d.GetMessage().Contains("TestHost")
        );
    }

    [Fact]
    public void HostAndUser_WithoutNotificationMethod_ReportsDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
[User]
public partial class TestHostUser : Node, ITestService
{
    [Singleton(typeof(ITestService))]
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
            d => d.Id == "GDI_C080" && d.GetMessage().Contains("TestHostUser")
        );
    }

    [Fact]
    public void HostAndUser_WithNotificationMethod_NoDiagnostic()
    {
        var source =
            @"
using Godot;
using GodotSharpDI.Abstractions;

[Host]
[User]
public partial class TestHostUser : Node, ITestService
{
    public override partial void _Notification(int what);
    
    [Singleton(typeof(ITestService))]
    private ITestService Self => this;
    
    [Inject]
    private IAnotherService _service = null!;
}

public interface ITestService { }
public interface IAnotherService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C080");
    }

    [Fact]
    public void Service_DoesNotRequireNotificationMethod()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;

[Singleton(typeof(ITestService))]
public partial class TestService : ITestService
{
    // Service 不需要 _Notification 方法
    public TestService() { }
}

public interface ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);

        // Service 类型不应该报告 GDI_C080 错误
        Assert.DoesNotContain(
            diagnostics,
            d => d.Id == "GDI_C080" && d.GetMessage().Contains("TestService")
        );
    }

    [Fact]
    public void NotificationMethod_WithCorrectSignature_GeneratesImplementation()
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

[Singleton(typeof(ITestService))]
public partial class TestService : ITestService { }
";

        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);

        // 检查没有诊断错误
        var diagnostics = TestCompilationHelper.GetGeneratorDiagnostics(compilation);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        // 检查生成了实现文件
        var sources = TestCompilationHelper.GetGeneratedSources(compilation);
        Assert.Contains(sources, s => s.HintName.Contains("TestUser") && s.HintName.Contains("DI"));
    }
}
