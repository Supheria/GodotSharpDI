using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

/// <summary>
/// Host [Provide] member exposed type validation tests
///
/// Note: Old architecture had independent [Singleton] service class and its ExposedTypes validation.
/// New architecture validates all service types through [Host]'s [Provide] members.
/// </summary>
public class ExposedTypeValidationTests
{
    // ============================================================
    //  [Provide] member exposed types - Interface validation
    // ============================================================

    [Fact]
    public void HostProvide_ExposesInterfaceNotImplementedByMemberType_ReportsDiagnostic()
    {
        var source =
            @"
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
        // ChunkManager does not implement IChunkGetter and IChunkGenerator
        [Provide(ExposedTypes = new Type[] { typeof(IChunkGetter), typeof(IChunkGenerator) })]
        private ChunkManager Self => this;
    }
}";
        var (result, _) = GetValidationResult(source, "ChunkManager");
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGetter")
        );
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGenerator")
        );
    }

    [Fact]
    public void HostProvide_ExposesOneInterfaceNotImplemented_ReportsSingleDiagnostic()
    {
        var source =
            @"
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
        // Implements IChunkGetter, but does not implement IChunkGenerator
        [Provide(ExposedTypes = new Type[] { typeof(IChunkGetter), typeof(IChunkGenerator) })]
        private ChunkManager Self => this;
    }
}";
        var (result, _) = GetValidationResult(source, "ChunkManager");
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGetter")
        );
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IChunkGenerator")
        );
    }

    [Fact]
    public void HostProvide_ExposesAllImplementedInterfaces_NoDiagnostic()
    {
        var source =
            @"
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
    //  [Provide] member exposed types - Non-Host members (sub-objects)
    // ============================================================

    [Fact]
    public void HostProvide_FieldObject_ExposesInterfaceNotImplemented_ReportsDiagnostic()
    {
        // Host field object does not implement the specified interface
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IWorldConfig { }
    public class WorldConfig { } // Does not implement IWorldConfig

    [Host]
    public partial class WorldManager : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IWorldConfig) })]
        private WorldConfig _config = new WorldConfig();
    }
}";
        var (result, _) = GetValidationResult(source, "WorldManager");
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("IWorldConfig")
        );
    }

    [Fact]
    public void HostProvide_FieldObject_ExposesImplementedInterface_NoDiagnostic()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IWorldConfig { }
    public class WorldConfig : IWorldConfig { } // Already implemented

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
        // Member type ConfigB, exposed type ConfigA (no inheritance relationship)
        var source =
            @"
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
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M060" && d.GetMessage().Contains("ConfigA")
        );
    }

    [Fact]
    public void HostProvide_ExposesBaseClassOfMemberType_NoDiagnostic()
    {
        // Member type DerivedConfig inherits BaseConfig → exposing BaseConfig is valid
        var source =
            @"
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
        // GDI_M061 = Warning (exposed type should be an interface)
        var source =
            @"
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
    //  Helpers
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
