using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

/// <summary>
/// _Notification method existence and signature validation tests (GDI_C060 / GDI_C061)
///
/// All DI types ([Host] / [User] / [Modules]) must declare:
///   public override partial void _Notification(int what);
/// Otherwise the generator cannot output lifecycle stub code.
/// </summary>
public sealed class NotificationMethodValidationTests
{
    // ============================================================
    //  [Host] scenarios
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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  [User] scenarios
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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  [Modules] (Scope) scenarios
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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  Signature error scenarios (GDI_C061)
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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

        // Non-partial definition version should trigger missing or signature error
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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.Contains(
            diagnostics,
            d => d.Id == "GDI_C060" && d.GetMessage().Contains("TestHost")
        );
    }

    // ============================================================
    //  Combined scenarios: [Host] + [User] same class
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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);

        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
    }

    // ============================================================
    //  Generated code validation
    // ============================================================

    [Fact]
    public void NotificationMethod_WithCorrectSignature_GeneratesImplementation()
    {
        // Verify: When _Notification signature is correct, generator accepts the class and produces DI files
        // Note: GDI_D050 (no Provider) is a graph validation level error, unrelated to _Notification signature
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
        var compilation = DiagnosticCompilationHelper.CreateCompilationWithDI(source);

        // Core assertion: Correct _Notification signature should not trigger GDI_C060/C081
        var diagnostics = DiagnosticCompilationHelper.GetGeneratorDiagnostics(compilation);
        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C060");
        Assert.DoesNotContain(diagnostics, d => d.Id == "GDI_C061");

        // Generator should produce DI files for this class
        var sources = DiagnosticCompilationHelper.GetGeneratedSources(compilation);
        Assert.Contains(sources, s => s.HintName.Contains("TestUser") && s.HintName.Contains("DI"));
    }
}
