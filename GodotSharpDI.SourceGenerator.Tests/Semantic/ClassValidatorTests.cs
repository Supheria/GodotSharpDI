using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

public class ClassValidatorTests
{
    [Fact]
    public void Validate_NonPartialClass_ReportsDiagnostic()
    {
        // [Host] 类必须是 partial — 非 partial 应报 GDI_C050
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [Host]
    public class MyHost : Node
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyHost");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Null(result.TypeInfo);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C050" // DiClassMustBePartial
        );
    }

    [Fact]
    public void Validate_UserNonPartialClass_ReportsDiagnostic()
    {
        // [User] 类也必须是 partial
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [User]
    public class MyUser : Node
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyUser");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        Assert.Null(result.TypeInfo);
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_C050");
    }

    [Fact]
    public void Validate_HostNotInheritingFromNode_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    [Host]
    public partial class MyHost
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyHost");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C020" // HostMustBeNode
        );
    }

    [Fact]
    public void Validate_UserNotInheritingFromNode_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    [User]
    public partial class MyUser
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyUser");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C021" // UserMustBeNode
        );
    }

    [Fact]
    public void Validate_ScopeNotInheritingFromNode_ReportsDiagnostic()
    {
        // Scope 必须继承 Godot.Node，否则报 GDI_C022
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    [Modules]
    public partial class MyScope : IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyScope");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C022" // ScopeMustBeNode
        );
    }

    [Fact]
    public void Validate_IDependenciesResolvedOnScopeClass_ReportsDiagnostic()
    {
        // IDependenciesResolved 只能用于 [Host] 或 [User]
        // Scope 类实现该接口 → GDI_C030
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [Modules]
    public partial class MyScope : Node, IScope, IDependenciesResolved
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
        public void OnDependenciesResolved() { }
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyScope");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert - IDependenciesResolved 不能用于 Scope
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C030" // IDependenciesResolvedInvalid
        );
    }

    [Fact]
    public void Validate_ScopeWithoutModules_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyScope");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C040" // ScopeMissingModules
        );
    }
}
