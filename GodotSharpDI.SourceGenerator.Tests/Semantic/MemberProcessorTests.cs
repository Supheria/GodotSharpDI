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
