using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

/// <summary>
/// Unit tests for ServiceIndexes construction
///
/// Focus on v1.2.0 new fields:
///   - DuplicateServiceProviders (P2: duplicate registration detection data)
///   - ServiceTypeToWaitForDeps (P1: service dependency graph data for cross-Host deadlock detection)
/// </summary>
public class ServiceIndexesTests
{
    // ============================================================
    //  DuplicateServiceProviders (P2)
    // ============================================================

    [Fact]
    public void DuplicateServiceProviders_TwoHosts_SameService_IsPopulated()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class ImplA : IMyService { }
    public class ImplB : IMyService { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public ImplA Create() => new ImplA();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public ImplB Create() => new ImplB();
    }
}";
        var indexes = BuildIndexes(source);

        Assert.NotEmpty(indexes.DuplicateServiceProviders);

        var dup = indexes.DuplicateServiceProviders.FirstOrDefault(kvp =>
            kvp.Key.Name == "IMyService"
        );
        Assert.NotNull(dup.Key);
        Assert.Equal(2, dup.Value.Length);
    }

    [Fact]
    public void DuplicateServiceProviders_UniqueServices_IsEmpty()
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
    public class A : IServiceA { }
    public class B : IServiceB { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) })]
        public A CreateA() => new A();
    }

    [Host]
    public partial class HostB : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) })]
        public B CreateB() => new B();
    }
}";
        var indexes = BuildIndexes(source);
        Assert.Empty(indexes.DuplicateServiceProviders);
    }

    [Fact]
    public void DuplicateServiceProviders_ThreeProviders_CountsAllThree()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IShared { }
    public class A : IShared { }
    public class B : IShared { }
    public class C : IShared { }

    [Host]
    public partial class H1 : Node { [Provide(ExposedTypes = new Type[] { typeof(IShared) })] public A Get() => new A(); }
    [Host]
    public partial class H2 : Node { [Provide(ExposedTypes = new Type[] { typeof(IShared) })] public B Get() => new B(); }
    [Host]
    public partial class H3 : Node { [Provide(ExposedTypes = new Type[] { typeof(IShared) })] public C Get() => new C(); }
}";
        var indexes = BuildIndexes(source);
        var dup = indexes.DuplicateServiceProviders.First(kvp => kvp.Key.Name == "IShared");
        Assert.Equal(3, dup.Value.Length);
    }

    // ============================================================
    //  ServiceTypeToWaitForDeps (P1)
    // ============================================================

    [Fact]
    public void ServiceTypeToWaitForDeps_SimpleWaitFor_IsMapped()
    {
        // HostA provides IServiceA, waits for IServiceB injection
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public class A : IServiceA { }

    [Host]
    public partial class HostA : Node
    {
        [Inject]
        private IServiceB _serviceB { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_serviceB) })]
        public A CreateA() => new A();
    }
}";
        var indexes = BuildIndexes(source);

        // IServiceA's WaitFor dependencies should include IServiceB
        var entry = indexes.ServiceTypeToWaitForDeps.FirstOrDefault(kvp =>
            kvp.Key.Name == "IServiceA"
        );
        Assert.NotNull(entry.Key);

        var depNames = entry.Value.Select(t => t.Name).ToList();
        Assert.Contains("IServiceB", depNames);
    }

    [Fact]
    public void ServiceTypeToWaitForDeps_NoWaitFor_NotInMap()
    {
        // Provide members without WaitFor should not appear in ServiceTypeToWaitForDeps
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public class A : IServiceA { }

    [Host]
    public partial class HostA : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) })]
        public A CreateA() => new A();
    }
}";
        var indexes = BuildIndexes(source);
        Assert.DoesNotContain(indexes.ServiceTypeToWaitForDeps, kvp => kvp.Key.Name == "IServiceA");
    }

    [Fact]
    public void ServiceTypeToWaitForDeps_MultipleWaitFor_AllDepsListed()
    {
        // Provide waits for two Inject fields → WaitForDeps should contain two types
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IA { }
    public interface IB { }
    public interface IC { }
    public class C : IC { }

    [Host]
    public partial class MyHost : Node
    {
        [Inject] private IA _a { get; set; }
        [Inject] private IB _b { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IC) }, WaitFor = new string[] { nameof(_a), nameof(_b) })]
        public C CreateC() => new C();
    }
}";
        var indexes = BuildIndexes(source);

        var entry = indexes.ServiceTypeToWaitForDeps.FirstOrDefault(kvp => kvp.Key.Name == "IC");
        Assert.NotNull(entry.Key);
        Assert.Equal(2, entry.Value.Count);

        var depNames = entry.Value.Select(t => t.Name).ToList();
        Assert.Contains("IA", depNames);
        Assert.Contains("IB", depNames);
    }

    [Fact]
    public void ServiceTypeToWaitForDeps_CrossHostCycle_BothServicesInMap()
    {
        // Two Hosts form a cross-Host circular wait → both services should be in ServiceTypeToWaitForDeps
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public class A : IServiceA { }
    public class B : IServiceB { }

    [Host]
    public partial class HostA : Node
    {
        [Inject] private IServiceB _b { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_b) })]
        public A CreateA() => new A();
    }

    [Host]
    public partial class HostB : Node
    {
        [Inject] private IServiceA _a { get; set; }
        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_a) })]
        public B CreateB() => new B();
    }
}";
        var indexes = BuildIndexes(source);

        var serviceNames = indexes.ServiceTypeToWaitForDeps.Keys.Select(k => k.Name).ToList();
        Assert.Contains("IServiceA", serviceNames);
        Assert.Contains("IServiceB", serviceNames);
    }

    [Fact]
    public void ServiceTypeToWaitForDeps_WaitForNonInjectField_NotIncludedInDeps()
    {
        // WaitFor references a non-[Inject] field (GDI_M081 Warning)
        // Non-Inject fields have no determined service type → should not appear in dependency graph
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public class A : IServiceA { }

    [Host]
    public partial class MyHost : Node
    {
        private object _notInjected { get; set; }

        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_notInjected) })]
        public A CreateA() => new A();
    }
}";
        var indexes = BuildIndexes(source);

        // IServiceA is either not in the map (non-Inject field filtered) or its dependency set is empty
        // Use null-safe lookup to avoid forced dereference after FirstOrDefault returns null
        var key = indexes.ServiceTypeToWaitForDeps.Keys.FirstOrDefault(k => k.Name == "IServiceA");
        if (key != null && indexes.ServiceTypeToWaitForDeps.TryGetValue(key, out var deps))
        {
            Assert.Empty(deps);
        }
        // If not in the map at all, the requirement is also satisfied (non-Inject field correctly filtered)
    }

    // ============================================================
    //  ServiceTypeToProviders basic validation
    // ============================================================

    [Fact]
    public void ServiceTypeToProviders_SingleHost_MapsServiceToHost()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IMyService { }
    public class Impl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IMyService) })]
        public Impl Create() => new Impl();
    }
}";
        var indexes = BuildIndexes(source);

        var providers = indexes
            .ServiceTypeToProviders.First(kvp => kvp.Key.Name == "IMyService")
            .Value;
        Assert.Single(providers);
        Assert.Equal("MyHost", providers[0].ValidatedTypeInfo.Symbol.Name);
    }

    [Fact]
    public void HasProvider_ExistingService_ReturnsTrue()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;
namespace Test
{
    public interface IFoo { }
    public class Foo : IFoo { }
    [Host]
    public partial class H : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IFoo) })] public Foo Get() => new Foo();
    }
}";
        // Must use the same compilation instance; cross-compilation ITypeSymbols cannot match via SymbolEqualityComparer
        var (indexes, compilation) = BuildIndexesWithCompilation(source);
        var fooSymbol = compilation.GetTypeByMetadataName("Test.IFoo");
        Assert.NotNull(fooSymbol);
        Assert.True(indexes.HasProvider(fooSymbol!));
    }

    [Fact]
    public void HasProvider_MissingService_ReturnsFalse()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;
namespace Test
{
    public interface IFoo { }
    public interface IBar { }
    public class Foo : IFoo { }
    [Host]
    public partial class H : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IFoo) })] public Foo Get() => new Foo();
    }
}";
        var (indexes, compilation) = BuildIndexesWithCompilation(source);
        var barSymbol = compilation.GetTypeByMetadataName("Test.IBar");
        Assert.NotNull(barSymbol);
        Assert.False(indexes.HasProvider(barSymbol!));
    }

    // ============================================================
    //  Helpers
    // ============================================================

    /// <summary>
    /// Builds indexes and also returns the compilation instance for tests that need cross-symbol comparison.
    /// SymbolEqualityComparer is only valid within a single compilation; cross-compilation must use the same instance.
    /// </summary>
    private static (
        ServiceIndexes Indexes,
        Microsoft.CodeAnalysis.Compilation Compilation
    ) BuildIndexesWithCompilation(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        return (BuildIndexesFromCompilation(compilation), compilation);
    }

    private static ServiceIndexes BuildIndexes(string source) =>
        BuildIndexesFromCompilation(TestCompilationHelper.CreateCompilationWithDI(source));

    private static ServiceIndexes BuildIndexesFromCompilation(
        Microsoft.CodeAnalysis.Compilation compilation
    )
    {
        var symbols = new CachedSymbols(compilation);
        var hosts = ImmutableArray.CreateBuilder<ValidatedTypeInfo>();
        var users = ImmutableArray.CreateBuilder<ValidatedTypeInfo>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
                if (raw.Info == null)
                    continue;

                var result = ClassPipeline.ValidateAndClassify(raw.Info, symbols);
                if (result.TypeInfo == null)
                    continue;

                if (result.TypeInfo.Role == TypeRole.Host)
                    hosts.Add(result.TypeInfo);
                else if (result.TypeInfo.Role == TypeRole.User)
                    users.Add(result.TypeInfo);
            }
        }

        var diagBuilder = ImmutableArray.CreateBuilder<Diagnostic>();
        var hostNodes = NodeBuilders.BuildHostNodes(hosts.ToImmutable(), diagBuilder);
        var userNodes = NodeBuilders.BuildUserNodes(users.ToImmutable(), diagBuilder);
        return ServiceIndexes.Build(hostNodes, userNodes);
    }
}
