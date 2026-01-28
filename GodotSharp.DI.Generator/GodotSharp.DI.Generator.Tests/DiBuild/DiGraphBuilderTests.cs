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

public class DiGraphBuilderTests
{
    [Fact]
    public void Build_ServiceTypeConflict_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IMyService { }

    [Singleton(typeof(IMyService))]
    public partial class ServiceA : IMyService { }

    [Singleton(typeof(IMyService))]
    public partial class ServiceB : IMyService { }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D040" // ServiceTypeConflict
        );
    }

    [Fact]
    public void Build_HostAndServiceProvidesSameType_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [Singleton(typeof(IMyService))]
    public partial class ServiceImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(IMyService))]
        private IMyService _service = new ServiceImpl();
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D040" // ServiceTypeConflict
        );
    }

    [Fact]
    public void Build_ScopeWithEmptyServices_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Modules(Services = new System.Type[] { })]
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D001" // ScopeModulesServicesEmpty
        );
    }

    [Fact]
    public void Build_ScopeWithEmptyHosts_ReportsInfoDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Singleton]
    public partial class MyService { }

    [Modules(Services = new[] { typeof(MyService) }, Hosts = new System.Type[] { })]
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D002" // ScopeModulesHostsEmpty (Info)
        );
    }

    [Fact]
    public void Build_ScopeModulesServiceMustBeService_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    public partial class NotAService { }

    [Modules(Services = new[] { typeof(NotAService) })]
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D003" // ScopeModulesServiceMustBeService
        );
    }

    [Fact]
    public void Build_ScopeModulesHostMustBeHost_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    [Singleton]
    public partial class MyService { }

    public partial class NotAHost : Node { }

    [Modules(Services = new[] { typeof(MyService) }, Hosts = new[] { typeof(NotAHost) })]
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_D004" // ScopeModulesHostMustBeHost
        );
    }

    [Fact]
    public void Build_UserInjectMemberNotService_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    public interface INotAService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private INotAService _notService;
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M040" // InjectMemberInvalidType
        );
    }

    [Fact]
    public void Build_ValidGraph_NoErrors()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

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
        public ServiceB() { }
    }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton]
        private object _hostService = new();
    }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private IServiceA _serviceA;
    }

    [Modules(Services = new[] { typeof(ServiceA), typeof(ServiceB) }, Hosts = new[] { typeof(MyHost) })]
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.NotNull(result.Graph);
        var errors = result
            .Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Build_MultipleExposedTypes_HandlesCorrectly()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }

    [Singleton(typeof(IServiceA), typeof(IServiceB))]
    public partial class MultiService : IServiceA, IServiceB
    {
        public MultiService() { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.NotNull(result.Graph);
        var serviceNode = result.Graph.ServiceNodes.FirstOrDefault();
        Assert.NotNull(serviceNode);
        Assert.Equal(2, serviceNode.ProvidedServices.Length);
    }

    [Fact]
    public void Build_TransientService_IsCorrectlyIdentified()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;

namespace Test
{
    [Transient]
    public partial class MyTransientService
    {
        public MyTransientService() { }
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.NotNull(result.Graph);
        var serviceNode = result.Graph.ServiceNodes.FirstOrDefault();
        Assert.NotNull(serviceNode);
        Assert.Equal(ServiceLifetime.Transient, serviceNode.TypeInfo.Lifetime);
    }

    [Fact]
    public void Build_HostProvidedServices_AreTracked()
    {
        // Arrange
        var source =
            @"
using GodotSharp.DI.Abstractions;
using Godot;

namespace Test
{
    public interface IHostService { }
    public class HostServiceImpl : IHostService { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(IHostService))]
        private HostServiceImpl _service = new();
    }
}
";
        var result = BuildGraph(source);

        // Assert
        Assert.NotNull(result.Graph);
        var hostNode = result.Graph.HostNodes.FirstOrDefault();
        Assert.NotNull(hostNode);
        Assert.Single(hostNode.ProvidedServices);
    }

    [Fact]
    public void Build_ServiceWithNoExposedTypesSpecified_UsesImplementationType()
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
        var result = BuildGraph(source);

        // Assert
        Assert.NotNull(result.Graph);
        var serviceNode = result.Graph.ServiceNodes.FirstOrDefault();
        Assert.NotNull(serviceNode);
        Assert.Single(serviceNode.ProvidedServices);
        Assert.Equal("MyService", serviceNode.ProvidedServices[0].Name);
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
