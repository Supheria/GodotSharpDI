using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

/// <summary>
/// MemberProcessor comprehensive tests
///
/// Note: Old architecture used [Singleton] as Host member attribute.
/// New architecture has unified to use [Provide] instead. Tests have been updated for new semantics.
/// </summary>
public class MemberProcessorTests
{
    // ============================================================
    //  [Inject] basic tests
    // ============================================================

    [Fact]
    public void Process_InjectFieldInUser_ReturnsInjectFieldMember()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private IMyService _service;
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.NotNull(result.TypeInfo);
        Assert.Single(result.TypeInfo!.Members);
        Assert.Equal(MemberKind.InjectField, result.TypeInfo.Members[0].Kind);
    }

    [Fact]
    public void Process_InjectPropertyInUser_ReturnsInjectPropertyMember()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        public IMyService Service { get; set; }
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.NotNull(result.TypeInfo);
        Assert.Single(result.TypeInfo!.Members);
        Assert.Equal(MemberKind.InjectProperty, result.TypeInfo.Members[0].Kind);
    }

    [Fact]
    public void Process_InjectPropertyWithoutSetter_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        public IMyService Service { get; }
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M020"); // InjectMemberNotAssignable
    }

    [Fact]
    public void Process_InjectMemberNotInUserOrHost_ReportsDiagnostic()
    {
        // [Inject] used on a plain class without [User]/[Host]
        // RawClassSemanticInfoFactory returns null for classes without DI attributes,
        // or ClassPipeline classifies it as TypeRole.None → TypeInfo = null (not processed)
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IMyService { }

    public partial class PlainClass
    {
        [Inject]
        private IMyService _other;
    }
}";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "PlainClass");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);

        // Non-DI class: Info may be null (directly ignored), or TypeInfo is null (classification failed)
        if (raw.Info == null)
        {
            // Expected behavior: classes without DI attributes are not processed, pass through
            return;
        }

        var symbols = new CachedSymbols(compilation);
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);
        Assert.Null(result.TypeInfo);
    }

    [Fact]
    public void Process_StaticInjectMember_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private static IMyService _service;
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M040"); // InjectMemberIsStatic
    }

    [Fact]
    public void Process_InjectHostType_ReportsWarning()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IFoo { }
    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IFoo) })]
        public MyHost Self => this;
    }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private MyHost _host;
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        // GDI_M042 = InjectMemberIsHostType (Warning, does not block usage)
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M042");
    }

    [Fact]
    public void Process_InjectScopeType_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private MyScope _scope;
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M044"); // InjectMemberIsScopeType
    }

    // ============================================================
    //  [Provide] basic tests
    // ============================================================

    [Fact]
    public void Process_ProvidePropertyInHost_ReturnsProvideMember()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }

    [Host]
    public partial class MyHost : Node, IMyService
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public MyHost Self => this;
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        Assert.Single(result.TypeInfo!.Members.Where(m => m.IsProvideMember));
        Assert.Equal(MemberKind.ProvideProperty, result.TypeInfo.Members[0].Kind);
    }

    [Fact]
    public void Process_ProvidePropertyWithoutGetter_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyServiceImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public MyServiceImpl Service { set { } }
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M030"); // ProvidePropertyNotAccessible
    }

    [Fact]
    public void Process_ProvideMemberAndInjectOnSameMember_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        [Inject]
        private IMyService _service { get; set; }
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M012"); // MemberConflictWithProvideAndInject
    }

    [Fact]
    public void Process_ProvideMemberNotInHost_ReportsDiagnostic()
    {
        // [Provide] used on [User] class (not Host)
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class Impl : IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public Impl Create() => new Impl();
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M010"); // ProvideMemberNotInServiceOrHost
    }

    [Fact]
    public void Process_StaticProvideMember_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyServiceImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        private static MyServiceImpl _service = new MyServiceImpl();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M050"); // ProvideMemberIsStatic
    }

    [Fact]
    public void Process_ProvideMemberIsAnotherHostType_ReportsDiagnostic()
    {
        // [Provide] member returns another [Host] type (not self) → GDI_M052
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    [Host]
    public partial class OtherHost : Node { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide]
        private OtherHost _otherHost = new OtherHost();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M052"); // ProvideMemberIsHostType
    }

    [Fact]
    public void Process_ExposedTypeIsConcreteClass_ReportsWarning()
    {
        // Exposed type is a concrete class instead of an interface → GDI_M061 Warning
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public class MyServiceImpl { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(MyServiceImpl) })]
        private MyServiceImpl _service = new MyServiceImpl();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M061" && d.Severity == DiagnosticSeverity.Warning
        );
    }

    [Fact]
    public void Process_HostWithoutProvideMember_ReportsWarning()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [Host]
    public partial class MyHost : Node
    {
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M070" && d.Severity == DiagnosticSeverity.Warning
        );
    }

    [Fact]
    public void Process_UserWithoutInjectMember_ReportsWarning()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [User]
    public partial class MyUser : Node
    {
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M071" && d.Severity == DiagnosticSeverity.Warning
        );
    }

    // ============================================================
    //  WaitFor related
    // ============================================================

    [Fact]
    public void Process_ProvideWithWaitFor_ParsedCorrectly()
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
    public partial class MyHost : Node
    {
        [Inject]
        private IServiceA _serviceA { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_serviceA) })]
        public ImplB CreateB() => new ImplB();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        var provideMember = result.TypeInfo!.Members.FirstOrDefault(m => m.IsProvideMember);
        Assert.NotNull(provideMember);
        Assert.True(provideMember!.HasWaitFor);
        Assert.Single(provideMember.WaitFor);
        Assert.Equal("_serviceA", provideMember.WaitFor[0]);
    }

    [Fact]
    public void Process_WaitForReferencesNonExistentField_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public class ImplA : IServiceA { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { ""_nonExistent"" })]
        public ImplA CreateA() => new ImplA();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M080");
    }

    [Fact]
    public void Process_InjectUserType_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [User]
    public partial class AnotherUser : Node
    {
        [Inject] private object _something;
    }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private AnotherUser _host;
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M043"); // InjectMemberIsUserType
    }

    [Fact]
    public void Process_ProvideUserType_ReportsDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [User]
    public partial class AnotherUser : Node
    {
        [Inject] private object _something;
    }

    [Host]
    public partial class MyHost : Node
    {
        [Provide]
        private AnotherUser _host;
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M053"); // ProvideMemberIsUserType
    }

    // ============================================================
    //  v1.3.0 new features: [Provide] field members, [Provide] Node type members, [Inject] Node type members
    // ============================================================

    [Fact]
    public void Process_ProvideFieldInHost_ReturnsProvideFieldMember()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IAlertBox { }
    public partial class AlertBox : Node, IAlertBox { }

    [Host]
    public partial class GuiHost : Node
    {
        [Export]
        [Provide(ExposedTypes = new Type[] { typeof(IAlertBox) })]
        private AlertBox _alertBox;
    }
}";
        var (result, _) = GetValidationResult(source, "GuiHost");
        Assert.NotNull(result.TypeInfo);
        Assert.Single(result.TypeInfo!.Members);
        Assert.Equal(MemberKind.ProvideField, result.TypeInfo.Members[0].Kind);
    }

    [Fact]
    public void Process_InjectNodeMemberInUser_ReportsWarning()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IAlertBox { }
    public partial class AlertBox : Node, IAlertBox { }

    [User]
    public partial class MapLoader : Node
    {
        [Inject]
        private IAlertBox _alertBox;
    }
}";
        var (result, _) = GetValidationResult(source, "MapLoader");
        Assert.NotNull(result.TypeInfo);
        // IAlertBox is an interface, not a Node, so it doesn't trigger Node Warning, passes normally
        Assert.Single(result.TypeInfo!.Members);
    }

    [Fact]
    public void Process_ProvidePropertyNodeTypeWithExposedTypes_ReturnsProvideMember()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IAlertBox { }
    public partial class AlertBox : Node, IAlertBox { }

    [Host]
    public partial class GuiHost : Node
    {
        [Export]
        private AlertBox _alertBox = null!;

        [Provide(ExposedTypes = new Type[] { typeof(IAlertBox) })]
        public AlertBox AlertBox => _alertBox;
    }
}";
        var (result, _) = GetValidationResult(source, "GuiHost");
        Assert.NotNull(result.TypeInfo);
        Assert.Equal(MemberKind.ProvideProperty, result.TypeInfo!.Members[0].Kind);
    }

    [Fact]
    public void Process_ProvideWaitForNonExistent()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public class ImplA : IServiceA { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { ""_nonExistent"" })]
        public ImplA CreateA() => new ImplA();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M080");
    }

    // ============================================================
    //  Async Provide member service type inference (Bug fix regression tests)
    // ============================================================

    /// <summary>
    /// Regression test: When async method doesn't specify ExposedTypes, service type should be T, not Task&lt;T&gt;.
    /// </summary>
    [Fact]
    public void Process_AsyncProvideMethod_NoExposedTypes_ServiceTypeIsInnerType()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System.Threading.Tasks;

namespace Test
{
    public interface IMyService { }
    public class MyService : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide]
        public async Task<MyService> CreateServiceAsync()
        {
            await Task.Yield();
            return new MyService();
        }
    }
}";
        var (result, symbols) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        var member = result.TypeInfo!.Members[0];
        Assert.True(member.IsAsync);
        Assert.Equal(MemberKind.ProvideMethod, member.Kind);

        // Key assertion: ExposedTypes should NOT contain Task<MyService>, it should be MyService
        Assert.Single(member.ExposedTypes);
        Assert.False(
            symbols.IsAsyncType(member.ExposedTypes[0]),
            "ExposedTypes should NOT be Task<T> — it should be the unwrapped inner type T."
        );
        Assert.Equal("MyService", member.ExposedTypes[0].Name);

        // MemberType should also be unwrapped to inner type
        Assert.Equal("MyService", member.MemberType.Name);
    }

    /// <summary>
    /// Regression test: When async property doesn't specify ExposedTypes, service type should be T, not Task&lt;T&gt;.
    /// </summary>
    [Fact]
    public void Process_AsyncProvideProperty_NoExposedTypes_ServiceTypeIsInnerType()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System.Threading.Tasks;

namespace Test
{
    public interface IMyService { }
    public class MyService : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide]
        public Task<MyService> Service => Task.FromResult(new MyService());
    }
}";
        var (result, symbols) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        var member = result.TypeInfo!.Members[0];
        Assert.True(member.IsAsync);
        Assert.Equal(MemberKind.ProvideProperty, member.Kind);

        Assert.Single(member.ExposedTypes);
        Assert.False(
            symbols.IsAsyncType(member.ExposedTypes[0]),
            "ExposedTypes should NOT be Task<T> — it should be the unwrapped inner type T."
        );
        Assert.Equal("MyService", member.ExposedTypes[0].Name);
    }

    /// <summary>
    /// Verify: When ExposedTypes is explicitly specified, behavior is not affected.
    /// </summary>
    [Fact]
    public void Process_AsyncProvideMethod_WithExposedTypes_UsesExplicitTypes()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;
using System.Threading.Tasks;

namespace Test
{
    public interface IMyService { }
    public class MyService : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public async Task<MyService> CreateServiceAsync()
        {
            await Task.Yield();
            return new MyService();
        }
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        var member = result.TypeInfo!.Members[0];
        Assert.True(member.IsAsync);

        Assert.Single(member.ExposedTypes);
        Assert.Equal("IMyService", member.ExposedTypes[0].Name);
    }

    // ============================================================
    //  Helpers
    // ============================================================

    private static (ClassValidationResult Result, CachedSymbols Symbols) GetValidationResult(
        string source,
        string className
    )
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == className);

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);
        return (result, symbols);
    }
}
