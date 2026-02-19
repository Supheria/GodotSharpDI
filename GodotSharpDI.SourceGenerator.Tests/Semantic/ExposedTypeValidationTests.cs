using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

/// <summary>
/// Host [Provide] 成员的暴露类型验证测试
///
/// 注意：旧架构有独立的 [Singleton] 服务类及其 ExposedTypes 验证。
/// 新架构服务类型验证全部通过 [Host] 的 [Provide] 成员进行。
/// </summary>
public class ExposedTypeValidationTests
{
    // ============================================================
    //  [Provide] 成员暴露类型 - 接口验证
    // ============================================================

    [Fact]
    public void HostProvide_ExposesInterfaceNotImplementedByMemberType_ReportsDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IChunkGetter { }
    public interface IChunkGenerator { }

    [Host]
    public partial class ChunkManager : Node
    {
        // ChunkManager 没有实现 IChunkGetter 和 IChunkGenerator
        [Provide(ExposedTypes = new Type[] { typeof(IChunkGetter), typeof(IChunkGenerator) })]
        private ChunkManager Self => this;
    }
}";
        var (result, _) = GetValidationResult(source, "ChunkManager");
        Assert.Contains(result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGetter"));
        Assert.Contains(result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGenerator"));
    }

    [Fact]
    public void HostProvide_ExposesOneInterfaceNotImplemented_ReportsSingleDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IChunkGetter { }
    public interface IChunkGenerator { }

    [Host]
    public partial class ChunkManager : Node, IChunkGetter
    {
        // 实现了 IChunkGetter，但没有实现 IChunkGenerator
        [Provide(ExposedTypes = new Type[] { typeof(IChunkGetter), typeof(IChunkGenerator) })]
        private ChunkManager Self => this;
    }
}";
        var (result, _) = GetValidationResult(source, "ChunkManager");
        Assert.DoesNotContain(result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGetter"));
        Assert.Contains(result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGenerator"));
    }

    [Fact]
    public void HostProvide_ExposesAllImplementedInterfaces_NoDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IChunkGetter { }
    public interface IChunkGenerator { }

    [Host]
    public partial class ChunkManager : Node, IChunkGetter, IChunkGenerator
    {
        [Provide(ExposedTypes = new Type[] { typeof(IChunkGetter), typeof(IChunkGenerator) })]
        private ChunkManager Self => this;
    }
}";
        var (result, _) = GetValidationResult(source, "ChunkManager");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_M060");
    }

    // ============================================================
    //  [Provide] 成员暴露类型 - 非 Host 成员（子对象）
    // ============================================================

    [Fact]
    public void HostProvide_FieldObject_ExposesInterfaceNotImplemented_ReportsDiagnostic()
    {
        // Host 字段对象不实现指定接口
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IWorldConfig { }
    public class WorldConfig { } // 未实现 IWorldConfig

    [Host]
    public partial class WorldManager : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IWorldConfig) })]
        private WorldConfig _config = new WorldConfig();
    }
}";
        var (result, _) = GetValidationResult(source, "WorldManager");
        Assert.Contains(result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IWorldConfig"));
    }

    [Fact]
    public void HostProvide_FieldObject_ExposesImplementedInterface_NoDiagnostic()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IWorldConfig { }
    public class WorldConfig : IWorldConfig { } // 已实现

    [Host]
    public partial class WorldManager : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IWorldConfig) })]
        private WorldConfig _config = new WorldConfig();
    }
}";
        var (result, _) = GetValidationResult(source, "WorldManager");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_M060");
    }

    [Fact]
    public void HostProvide_ExposesConcreteClass_NotMatching_ReportsDiagnostic()
    {
        // 成员类型 ConfigB，暴露类型 ConfigA（无继承关系）
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public class ConfigA { }
    public class ConfigB { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(ConfigA) })]
        private ConfigB _config = new ConfigB();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("ConfigA"));
    }

    [Fact]
    public void HostProvide_ExposesBaseClassOfMemberType_NoDiagnostic()
    {
        // 成员类型 DerivedConfig 继承 BaseConfig → 暴露 BaseConfig 合法
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public class BaseConfig { }
    public class DerivedConfig : BaseConfig { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(BaseConfig) })]
        private DerivedConfig _config = new DerivedConfig();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_M060");
    }

    [Fact]
    public void HostProvide_ExposedTypeIsConcreteClass_NotInterface_ReportsWarning()
    {
        // GDI_M061 = Warning（暴露类型应该是接口）
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public class MyConcreteService { }

    [Host]
    public partial class MyHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(MyConcreteService) })]
        private MyConcreteService _service = new MyConcreteService();
    }
}";
        var (result, _) = GetValidationResult(source, "MyHost");
        Assert.Contains(result.Diagnostics, d => d.Id == "GDI_M061");
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
