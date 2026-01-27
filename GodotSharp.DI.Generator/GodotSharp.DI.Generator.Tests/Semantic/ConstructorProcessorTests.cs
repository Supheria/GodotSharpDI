using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Semantic;
using GodotSharp.DI.Generator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharp.DI.Generator.Tests.Semantic;

public class ConstructorProcessorTests
{
    [Fact]
    public void Process_ServiceWithParameterlessConstructor_ReturnsConstructorInfo()
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
        public MyService() { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.NotNull(result.TypeInfo.Constructor);
        Assert.Empty(result.TypeInfo.Constructor.Parameters);
    }

    [Fact]
    public void Process_ServiceWithSingleConstructor_ReturnsConstructorInfo()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IMyDependency { }

    [Singleton]
    public partial class MyService
    {
        public MyService(IMyDependency dep) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.NotNull(result.TypeInfo.Constructor);
        Assert.Single(result.TypeInfo.Constructor.Parameters);
    }

    [Fact]
    public void Process_ServiceWithMultipleConstructorsWithAttribute_ReturnsMarkedConstructor()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IMyDependency { }

    [Singleton]
    public partial class MyService
    {
        public MyService() { }

        [InjectConstructor]
        public MyService(IMyDependency dep) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.NotNull(result.TypeInfo.Constructor);
        Assert.Single(result.TypeInfo.Constructor.Parameters);
    }

    [Fact]
    public void Process_ServiceWithMultipleConstructorsNoAttribute_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IMyDependency { }

    [Singleton]
    public partial class MyService
    {
        public MyService() { }
        public MyService(IMyDependency dep) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_S021" // AmbiguousConstructor
        );
    }

    [Fact]
    public void Process_ServiceWithNoPublicConstructor_ReportsDiagnostic()
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
        private MyService() { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_S020" // NoPublicConstructor
        );
    }

    [Fact]
    public void Process_NonServiceWithInjectConstructor_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyDependency { }

    [User]
    public partial class MyUser : Node
    {
        [InjectConstructor]
        public MyUser(IMyDependency dep) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_S022" // InjectConstructorAttributeIsInvalid
        );
    }

    [Fact]
    public void Process_ServiceConstructorWithInvalidParameterType_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Singleton]
    public partial class MyService
    {
        public MyService(Node node) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_S030" // InjectConstructorParameterTypeInvalid
        );
    }

    [Fact]
    public void Process_ServiceWithMultipleInjectConstructors_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IDepA { }
    public interface IDepB { }

    [Singleton]
    public partial class MyService
    {
        [InjectConstructor]
        public MyService(IDepA a) { }

        [InjectConstructor]
        public MyService(IDepB b) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_S021" // AmbiguousConstructor
        );
    }

    [Fact]
    public void Process_ServiceConstructorWithInterfaceParameter_IsValid()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IMyDependency { }

    [Singleton]
    public partial class MyService
    {
        public MyService(IMyDependency dep) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.NotNull(result.TypeInfo.Constructor);
        Assert.Single(result.TypeInfo.Constructor.Parameters);
        Assert.Empty(
            result.Diagnostics.Where(d =>
                d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
            )
        );
    }

    [Fact]
    public void Process_ServiceConstructorWithClassParameter_IsValid()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Singleton]
    public partial class MyDependency { }

    [Singleton]
    public partial class MyService
    {
        public MyService(MyDependency dep) { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.NotNull(result.TypeInfo.Constructor);
        Assert.Single(result.TypeInfo.Constructor.Parameters);
        Assert.Empty(
            result.Diagnostics.Where(d =>
                d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error
            )
        );
    }

    [Fact]
    public void Process_HostDoesNotProcessConstructor()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Host]
    public partial class MyHost : Node
    {
        public MyHost() { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.Null(result.TypeInfo.Constructor);
    }

    [Fact]
    public void Process_UserDoesNotProcessConstructor()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [User]
    public partial class MyUser : Node
    {
        public MyUser() { }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.Null(result.TypeInfo.Constructor);
    }

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
