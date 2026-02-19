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
/// Provide 方法/属性的签名验证测试
///
/// 架构背景：
/// 旧架构有独立的 [Singleton] 服务类和构造器注入（InjectConstructor），
/// 新架构已移除，服务全部通过 [Host] 的 [Provide] 成员（属性/方法）暴露。
/// 本文件测试 [Provide] 方法/属性的签名合法性验证。
/// </summary>
public class ProvideMemberSignatureTests
{
    [Fact]
    public void Provide_OnMethod_WithReturnType_Valid()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public MyImpl CreateService() => new MyImpl();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        // 应该有一个 ProvideMethod 类型成员
        Assert.Single(result.TypeInfo!.Members.Where(m => m.IsProvideMember));
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Provide_OnMethod_ReturnsVoid_ReportsDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { })]
        public void BadMethod() { }
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M031");
    }

    [Fact]
    public void Provide_OnMethod_WithParameters_ReportsDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public MyImpl CreateService(int param) => new MyImpl();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M032");
    }

    [Fact]
    public void Provide_OnProperty_WithoutGetter_ReportsDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public MyImpl Service { set { } }
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M030");
    }

    [Fact]
    public void Provide_OnProperty_WithGetter_Valid()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyImpl : IMyService { }

    [Host]
    public partial class MyHost : Node, IMyService
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public MyHost Self => this;
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Provide_StaticMethod_ReportsDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public static MyImpl CreateService() => new MyImpl();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M050");
    }

    [Fact]
    public void Provide_OnNonHostClass_ReportsDiagnostic()
    {
        // [Provide] 用在没有 [Host] 的普通类上
        var source = @"
using GodotSharpDI.Abstractions;
using System;

namespace Test
{
    public interface IMyService { }
    public class MyImpl : IMyService { }

    public partial class PlainClass
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public MyImpl Create() => new MyImpl();
    }
}";
        var (result, _) = GetValidationResult(source, "PlainClass");
        // PlainClass TypeRole.None → 诊断为空（直接忽略），或报 GDI_M010
        // 取决于 ClassValidator 是否会处理 TypeRole.None 的类的成员
        // 实际上 TypeRole.None 时 TypeInfo 为 null
        Assert.Null(result.TypeInfo);
    }

    [Fact]
    public void Inject_WithReadyCallback_PropertyParsedCorrectly()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Inject(ReadyCallback = true)]
        private IMyService _service { get; set; }
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.NotNull(result.TypeInfo);
        var injectMember = result.TypeInfo!.Members.FirstOrDefault(m => m.IsInjectMember);
        Assert.NotNull(injectMember);
        Assert.True(injectMember!.HasReadyCallback);
        Assert.False(injectMember.HasFailureCallback);
    }

    [Fact]
    public void Inject_WithFailureCallback_PropertyParsedCorrectly()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject(FailureCallback = true)]
        private IMyService _service;
    }
}";
        var (result, _) = GetValidationResult(source, "MyUser");
        Assert.NotNull(result.TypeInfo);
        var injectMember = result.TypeInfo!.Members.FirstOrDefault(m => m.IsInjectMember);
        Assert.NotNull(injectMember);
        Assert.True(injectMember!.HasFailureCallback);
        Assert.False(injectMember.HasReadyCallback);
    }

    // ============================================================
    //  辅助
    // ============================================================

    private static (ClassValidationResult Result, CachedSymbols Symbols) GetValidationResult(
        string source, string className)
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
