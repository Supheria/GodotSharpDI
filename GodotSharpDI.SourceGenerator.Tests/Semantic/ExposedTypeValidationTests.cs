using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

/// <summary>
/// 测试 Service 和 Host 暴露类型验证
/// </summary>
public class ExposedTypeValidationTests
{
    #region Service 暴露类型测试

    [Fact]
    public void Service_ExposesInterfaceNotImplemented_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IDataReader { }
    public interface IDataWriter { }

    [Singleton(typeof(IDataWriter), typeof(IDataReader))]
    public partial class DataBase
    {
        // DataBase 没有实现 IDataWriter 和 IDataReader
    }
}
";
        var (result, symbols) = GetValidationResult(source, "DataBase");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C071" && d.GetMessage().Contains("IDataWriter")
        );
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C071" && d.GetMessage().Contains("IDataReader")
        );
    }

    [Fact]
    public void Service_ExposesOneInterfaceNotImplemented_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IDataReader { }
    public interface IDataWriter { }

    [Singleton(typeof(IDataWriter), typeof(IDataReader))]
    public partial class DataBase : IDataWriter
    {
        // DataBase 实现了 IDataWriter，但没有实现 IDataReader
    }
}
";
        var (result, symbols) = GetValidationResult(source, "DataBase");

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id == "GDI_C071" && d.GetMessage().Contains("IDataWriter")
        );
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C071" && d.GetMessage().Contains("IDataReader")
        );
    }

    [Fact]
    public void Service_ExposesAllImplementedInterfaces_NoDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IDataReader { }
    public interface IDataWriter { }

    [Singleton(typeof(IDataWriter), typeof(IDataReader))]
    public partial class DataBase : IDataWriter, IDataReader
    {
        // DataBase 实现了所有暴露的接口
    }
}
";
        var (result, symbols) = GetValidationResult(source, "DataBase");

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_C070");
    }

    [Fact]
    public void Service_ExposesClassNotInheritFrom_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public class BaseConfig { }
    public class OtherClass { }

    [Singleton(typeof(BaseConfig))]
    public partial class MyConfig : OtherClass
    {
        // MyConfig 继承自 OtherClass，不是 BaseConfig
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyConfig");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_C070" && d.GetMessage().Contains("BaseConfig")
        );
    }

    [Fact]
    public void Service_ExposesSelfClass_NoDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    [Singleton(typeof(MyService))]
    public partial class MyService
    {
        // 暴露自己
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_C071");
    }

    [Fact]
    public void Service_ExposesBaseClass_NoDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public class BaseService { }

    [Singleton(typeof(BaseService))]
    public partial class DerivedService : BaseService
    {
        // 暴露基类
    }
}
";
        var (result, symbols) = GetValidationResult(source, "DerivedService");

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_C062");
    }

    #endregion

    #region Host 成员暴露类型测试

    [Fact]
    public void HostMember_ExposesInterfaceNotImplemented_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IChunkGetter { }
    public interface IChunkGenerator { }

    [Host]
    public partial class ChunkManager : Node
    {
        [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
        private ChunkManager Self => this;
        // ChunkManager 没有实现 IChunkGetter 和 IChunkGenerator
    }
}
";
        var (result, symbols) = GetValidationResult(source, "ChunkManager");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M055" && d.GetMessage().Contains("IChunkGetter")
        );
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M055" && d.GetMessage().Contains("IChunkGenerator")
        );
    }

    [Fact]
    public void HostMember_ExposesOneInterfaceNotImplemented_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IChunkGetter { }
    public interface IChunkGenerator { }

    [Host]
    public partial class ChunkManager : Node, IChunkGetter
    {
        [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
        private ChunkManager Self => this;
        // ChunkManager 实现了 IChunkGetter，但没有实现 IChunkGenerator
    }
}
";
        var (result, symbols) = GetValidationResult(source, "ChunkManager");

        // Assert
        Assert.DoesNotContain(
            result.Diagnostics,
            d => d.Id == "GDI_M055" && d.GetMessage().Contains("IChunkGetter")
        );
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M055" && d.GetMessage().Contains("IChunkGenerator")
        );
    }

    [Fact]
    public void HostMember_ExposesAllImplementedInterfaces_NoDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IChunkGetter { }
    public interface IChunkGenerator { }

    [Host]
    public partial class ChunkManager : Node, IChunkGetter, IChunkGenerator
    {
        [Singleton(typeof(IChunkGetter), typeof(IChunkGenerator))]
        private ChunkManager Self => this;
    }
}
";
        var (result, symbols) = GetValidationResult(source, "ChunkManager");

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_M070");
    }

    [Fact]
    public void HostMember_ExposesOtherObjectInterface_ReportsDiagnosticWhenNotImplemented()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IWorldConfig { }
    public class WorldConfig { }

    [Host]
    public partial class WorldManager : Node
    {
        [Singleton(typeof(IWorldConfig))]
        private WorldConfig _config = new();
        // WorldConfig 没有实现 IWorldConfig
    }
}
";
        var (result, symbols) = GetValidationResult(source, "WorldManager");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M055" && d.GetMessage().Contains("IWorldConfig")
        );
    }

    [Fact]
    public void HostMember_ExposesOtherObjectInterface_NoDiagnosticWhenImplemented()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IWorldConfig { }
    public class WorldConfig : IWorldConfig { }

    [Host]
    public partial class WorldManager : Node
    {
        [Singleton(typeof(IWorldConfig))]
        private WorldConfig _config = new();
    }
}
";
        var (result, symbols) = GetValidationResult(source, "WorldManager");

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_M070");
    }

    [Fact]
    public void HostMember_ExposesClassNotMatching_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public class ConfigA { }
    public class ConfigB { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(ConfigA))]
        private ConfigB _config = new();
        // ConfigB 不是 ConfigA 且不继承自 ConfigA
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M055" && d.GetMessage().Contains("ConfigA")
        );
    }

    [Fact]
    public void HostMember_ExposesBaseClassOfMemberType_NoDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public class BaseConfig { }
    public class DerivedConfig : BaseConfig { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(BaseConfig))]
        private DerivedConfig _config = new();
        // DerivedConfig 继承自 BaseConfig
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "GDI_M070");
    }

    #endregion

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
