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
/// ServiceIndexes 构建的单元测试
///
/// 重点覆盖 v1.2.0 新增字段：
///   - DuplicateServiceProviders（P2：重复注册检测数据）
///   - ServiceTypeToWaitForDeps（P1：跨 Host 死锁检测的服务依赖图数据）
/// </summary>
public class ServiceIndexesTests
{
    // ============================================================
    //  DuplicateServiceProviders（P2）
    // ============================================================

    [Fact]
    public void DuplicateServiceProviders_TwoHosts_SameService_IsPopulated()
    {
        var source = @"
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

        var dup = indexes.DuplicateServiceProviders
            .FirstOrDefault(kvp => kvp.Key.Name == "IMyService");
        Assert.NotNull(dup.Key);
        Assert.Equal(2, dup.Value.Length);
    }

    [Fact]
    public void DuplicateServiceProviders_UniqueServices_IsEmpty()
    {
        var source = @"
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
        var source = @"
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
    //  ServiceTypeToWaitForDeps（P1）
    // ============================================================

    [Fact]
    public void ServiceTypeToWaitForDeps_SimpleWaitFor_IsMapped()
    {
        // HostA 提供 IServiceA，等待 IServiceB 注入
        var source = @"
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

        // IServiceA 的 WaitFor 依赖应包含 IServiceB
        var entry = indexes.ServiceTypeToWaitForDeps
            .FirstOrDefault(kvp => kvp.Key.Name == "IServiceA");
        Assert.NotNull(entry.Key);

        var depNames = entry.Value.Select(t => t.Name).ToList();
        Assert.Contains("IServiceB", depNames);
    }

    [Fact]
    public void ServiceTypeToWaitForDeps_NoWaitFor_NotInMap()
    {
        // 没有 WaitFor 的 Provide 成员不应出现在 ServiceTypeToWaitForDeps 中
        var source = @"
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
        // Provide 等待两个 Inject 字段 → WaitForDeps 应包含两个类型
        var source = @"
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

        var entry = indexes.ServiceTypeToWaitForDeps
            .FirstOrDefault(kvp => kvp.Key.Name == "IC");
        Assert.NotNull(entry.Key);
        Assert.Equal(2, entry.Value.Count);

        var depNames = entry.Value.Select(t => t.Name).ToList();
        Assert.Contains("IA", depNames);
        Assert.Contains("IB", depNames);
    }

    [Fact]
    public void ServiceTypeToWaitForDeps_CrossHostCycle_BothServicesInMap()
    {
        // 两个 Host 形成跨 Host 循环等待 → 两个服务都在 ServiceTypeToWaitForDeps 中
        var source = @"
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
        // WaitFor 引用了一个非 [Inject] 字段（GDI_M081 Warning）
        // 非 Inject 字段没有确定的服务类型 → 不应出现在依赖图中
        var source = @"
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

        // IServiceA 要么不在 map 中（非 Inject 字段被过滤），要么其依赖集为空
        if (indexes.ServiceTypeToWaitForDeps.TryGetValue(
            indexes.ServiceTypeToWaitForDeps.Keys.FirstOrDefault(k => k.Name == "IServiceA")!,
            out var deps))
        {
            Assert.Empty(deps);
        }
        // 若根本不在 map 中则也满足要求
    }

    // ============================================================
    //  ServiceTypeToProviders 基础验证
    // ============================================================

    [Fact]
    public void ServiceTypeToProviders_SingleHost_MapsServiceToHost()
    {
        var source = @"
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

        var providers = indexes.ServiceTypeToProviders
            .First(kvp => kvp.Key.Name == "IMyService").Value;
        Assert.Single(providers);
        Assert.Equal("MyHost", providers[0].ValidatedTypeInfo.Symbol.Name);
    }

    [Fact]
    public void HasProvider_ExistingService_ReturnsTrue()
    {
        var source = @"
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
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var indexes = BuildIndexes(source);
        var fooSymbol = compilation.GetTypeByMetadataName("Test.IFoo");
        Assert.NotNull(fooSymbol);
        Assert.True(indexes.HasProvider(fooSymbol!));
    }

    [Fact]
    public void HasProvider_MissingService_ReturnsFalse()
    {
        var source = @"
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
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var indexes = BuildIndexes(source);
        var barSymbol = compilation.GetTypeByMetadataName("Test.IBar");
        Assert.NotNull(barSymbol);
        Assert.False(indexes.HasProvider(barSymbol!));
    }

    // ============================================================
    //  辅助
    // ============================================================

    private static ServiceIndexes BuildIndexes(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var symbols = new CachedSymbols(compilation);
        var hosts = ImmutableArray.CreateBuilder<ValidatedTypeInfo>();
        var users = ImmutableArray.CreateBuilder<ValidatedTypeInfo>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
                if (raw.Info == null) continue;

                var result = ClassPipeline.ValidateAndClassify(raw.Info, symbols);
                if (result.TypeInfo == null) continue;

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
