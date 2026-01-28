using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Semantic;
using GodotSharp.DI.Generator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharp.DI.Generator.Tests.Semantic;

public class ClassValidatorTests
{
    [Fact]
    public void Validate_NonPartialClass_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Singleton]
    public class MyService
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyService");

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
    public void Validate_PartialSingletonClass_ReturnsValidTypeInfo()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Singleton]
    public partial class MyService
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyService");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.Equal(TypeRole.Service, result.TypeInfo.Role);
        Assert.Equal(ServiceLifetime.Singleton, result.TypeInfo.Lifetime);
    }

    [Fact]
    public void Validate_BothSingletonAndTransient_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Singleton]
    [Transient]
    public partial class MyService
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyService");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Null(result.TypeInfo);
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C001" // ServiceLifetimeConflict
        );
    }

    [Fact]
    public void Validate_ServiceInheritsFromNode_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Singleton]
    public partial class MyService : Node
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyService");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_S010" // ServiceCannotBeNode
        );
    }

    [Fact]
    public void Validate_HostNotInheritingFromNode_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

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
using GodotSharp.DI.Abstractions;

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
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Modules(Services = new[] { typeof(MyService) })]
    public partial class MyScope : IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }

    [Singleton]
    public partial class MyService { }
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
    public void Validate_IServicesReadyWithoutUser_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Singleton]
    public partial class MyService : IServicesReady
    {
        public void OnServicesReady() { }
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyService");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C030" // ServiceReadyNeedUser
        );
    }

    [Fact]
    public void Validate_ScopeWithoutModules_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
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

    [Fact]
    public void Validate_HostAndUser_ReturnsCorrectRole()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Host]
    [User]
    public partial class MyNode : Node
    {
    }
}
";
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var tree = compilation.SyntaxTrees.First();
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "MyNode");

        var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
        Assert.NotNull(raw.Info);

        var symbols = new CachedSymbols(compilation);

        // Act
        var result = ClassPipeline.ValidateAndClassify(raw.Info!, symbols);

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.Equal(TypeRole.HostAndUser, result.TypeInfo.Role);
    }
}
