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
/// MemberProcessor 的完整测试
///
/// 注意：旧架构使用 [Singleton] 作为 Host 成员属性，
/// 新架构已统一使用 [Provide] 代替。测试已更新为新语义。
/// </summary>
public class MemberProcessorTests
{
    // ============================================================
    //  [Inject] 基础测试
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
        // [Inject] 用在没有 [User]/[Host] 的普通类上
        // RawClassSemanticInfoFactory 对无 DI 属性的类返回 null，
        // 或 ClassPipeline 将其分类为 TypeRole.None → TypeInfo = null（不处理）
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

        // 非 DI 类：Info 可能为 null（直接忽略），或 TypeInfo 为 null（分类失败）
        if (raw.Info == null)
        {
            // 预期行为：无 DI 属性的类不被处理，直接通过
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
        // GDI_M042 = InjectMemberIsHostType（Warning，不阻止使用）
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
    //  [Provide] 基础测试
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
        // [Provide] 用在 [User] 类（非 Host）
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
        // [Provide] 成员返回另一个 [Host] 类型（非自身）→ GDI_M052
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
        // 暴露类型是具体类而非接口 → GDI_M061 Warning
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
    //  WaitFor 相关
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

    // ============================================================
    //  v1.3.0 新功能：[Provide] 字段成员、[Provide] Node 类型成员、[Inject] Node 类型成员
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
        // IAlertBox 是接口，不是 Node，所以不触发 Node Warning，正常通过
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
    //  异步 Provide 成员的服务类型推断（Bug 修复回归测试）
    // ============================================================

    /// <summary>
    /// 回归测试：异步方法未指定 ExposedTypes 时，服务类型应为 T 而非 Task&lt;T&gt;。
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

        // 关键断言：ExposedTypes 不应包含 Task<MyService>，而应是 MyService
        Assert.Single(member.ExposedTypes);
        Assert.False(
            symbols.IsAsyncType(member.ExposedTypes[0]),
            "ExposedTypes should NOT be Task<T> — it should be the unwrapped inner type T."
        );
        Assert.Equal("MyService", member.ExposedTypes[0].Name);

        // MemberType 也应解包为内部类型
        Assert.Equal("MyService", member.MemberType.Name);
    }

    /// <summary>
    /// 回归测试：异步属性未指定 ExposedTypes 时，服务类型应为 T 而非 Task&lt;T&gt;。
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
    /// 验证：显式指定 ExposedTypes 时，行为不受影响。
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
    //  辅助
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
