using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.DiBuild;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Semantic;
using GodotSharp.DI.Generator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharp.DI.Generator.Tests.DiBuild;

public class CircularDependencyTests
{
    [Fact]
    public void Build_DirectCircularDependency_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }

    [Singleton(typeof(IServiceA))]
    public partial class ServiceA : IServiceA
    {
        public ServiceA(IServiceB b) { }
    }

    [Singleton(typeof(IServiceB))]
    public partial class ServiceB : IServiceB
    {
        public ServiceB(IServiceA a) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    [Fact]
    public void Build_IndirectCircularDependency_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface IServiceC { }

    [Singleton(typeof(IServiceA))]
    public partial class ServiceA : IServiceA
    {
        public ServiceA(IServiceB b) { }
    }

    [Singleton(typeof(IServiceB))]
    public partial class ServiceB : IServiceB
    {
        public ServiceB(IServiceC c) { }
    }

    [Singleton(typeof(IServiceC))]
    public partial class ServiceC : IServiceC
    {
        public ServiceC(IServiceA a) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    [Fact]
    public void Build_SelfCircularDependency_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IMyService { }

    [Singleton(typeof(IMyService))]
    public partial class MyService : IMyService
    {
        public MyService(IMyService self) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    [Fact]
    public void Build_NoDependencies_NoCircularDependency()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Singleton]
    public partial class ServiceA { }

    [Singleton]
    public partial class ServiceB { }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    [Fact]
    public void Build_LinearDependencyChain_NoCircularDependency()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface IServiceC { }

    [Singleton(typeof(IServiceA))]
    public partial class ServiceA : IServiceA
    {
        public ServiceA(IServiceB b) { }
    }

    [Singleton(typeof(IServiceB))]
    public partial class ServiceB : IServiceB
    {
        public ServiceB(IServiceC c) { }
    }

    [Singleton(typeof(IServiceC))]
    public partial class ServiceC : IServiceC
    {
        public ServiceC() { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    [Fact]
    public void Build_DiamondDependency_NoCircularDependency()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface IServiceC { }
    public interface IServiceD { }

    [Singleton(typeof(IServiceA))]
    public partial class ServiceA : IServiceA
    {
        public ServiceA(IServiceB b, IServiceC c) { }
    }

    [Singleton(typeof(IServiceB))]
    public partial class ServiceB : IServiceB
    {
        public ServiceB(IServiceD d) { }
    }

    [Singleton(typeof(IServiceC))]
    public partial class ServiceC : IServiceC
    {
        public ServiceC(IServiceD d) { }
    }

    [Singleton(typeof(IServiceD))]
    public partial class ServiceD : IServiceD
    {
        public ServiceD() { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    [Fact]
    public void Build_ComplexCircularDependency_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }
    public interface ID { }
    public interface IE { }

    [Singleton(typeof(IA))]
    public partial class A : IA
    {
        public A(IB b, IC c) { }
    }

    [Singleton(typeof(IB))]
    public partial class B : IB
    {
        public B(ID d) { }
    }

    [Singleton(typeof(IC))]
    public partial class C : IC
    {
        public C(IE e) { }
    }

    [Singleton(typeof(ID))]
    public partial class D : ID
    {
        public D(IE e) { }
    }

    [Singleton(typeof(IE))]
    public partial class E : IE
    {
        public E(IA a) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    [Fact]
    public void Build_ServiceConstructorParameterNotService_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }

    [Singleton(typeof(IServiceA))]
    public partial class ServiceA : IServiceA
    {
        public ServiceA(IServiceB b) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D020" // ServiceConstructorParameterInvalid
        );
    }

    [Fact]
    public void Build_MultipleServicesShareDependency_IsValid()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IShared { }
    public interface IServiceA { }
    public interface IServiceB { }

    [Singleton(typeof(IShared))]
    public partial class Shared : IShared
    {
        public Shared() { }
    }

    [Singleton(typeof(IServiceA))]
    public partial class ServiceA : IServiceA
    {
        public ServiceA(IShared shared) { }
    }

    [Singleton(typeof(IServiceB))]
    public partial class ServiceB : IServiceB
    {
        public ServiceB(IShared shared) { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id == "GDI_D010" // CircularDependencyDetected
        );
    }

    private static DiGraphBuildResult BuildGraph(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var symbols = new CachedSymbols(compilation);

        var classResults = ImmutableArray.CreateBuilder<ClassValidationResult>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var classDecls = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDecls)
            {
                var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
                if (raw.Info != null)
                {
                    var result = ClassPipeline.ValidateAndClassify(raw.Info, symbols);
                    classResults.Add(result);
                }
            }
        }

        return DiGraphBuilder.Build(classResults.ToImmutable(), symbols);
    }
}
