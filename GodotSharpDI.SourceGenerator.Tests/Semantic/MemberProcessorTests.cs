using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Semantic;

public class MemberProcessorTests
{
    [Fact]
    public void Process_InjectFieldInUser_ReturnsInjectMember()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private IMyService _service;
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.Single(result.TypeInfo.Members);
        Assert.Equal(MemberKind.InjectField, result.TypeInfo.Members[0].Kind);
    }

    [Fact]
    public void Process_InjectPropertyInUser_ReturnsInjectMember()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        public IMyService Service { get; set; }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.Single(result.TypeInfo.Members);
        Assert.Equal(MemberKind.InjectProperty, result.TypeInfo.Members[0].Kind);
    }

    [Fact]
    public void Process_InjectPropertyWithoutSetter_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        public IMyService Service { get; }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M020" // InjectMemberNotAssignable
        );
    }

    [Fact]
    public void Process_SingletonFieldInHost_ReturnsSingletonMember()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }
    public class MyServiceImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(IMyService))]
        private MyServiceImpl _service = new();
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.NotNull(result.TypeInfo);
        Assert.Single(result.TypeInfo.Members);
        Assert.Equal(MemberKind.SingletonField, result.TypeInfo.Members[0].Kind);
        Assert.Single(result.TypeInfo.Members[0].ExposedTypes);
    }

    [Fact]
    public void Process_SingletonPropertyWithoutGetter_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }
    public class MyServiceImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(IMyService))]
        public MyServiceImpl Service { set { } }
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M030" // SingletonPropertyNotAccessible
        );
    }

    [Fact]
    public void Process_InjectAndSingletonOnSameMember_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [Host, User]
    public partial class MyNode : Node
    {
        [Inject]
        [Singleton(typeof(IMyService))]
        private IMyService _service;
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyNode");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M012" // MemberConflictWithSingletonAndInject
        );
    }

    [Fact]
    public void Process_InjectMemberNotInUser_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IMyService { }

    [Singleton]
    public partial class MyService
    {
        [Inject]
        private IMyService _other;
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M011" // MemberHasInjectButNotInUser
        );
    }

    [Fact]
    public void Process_SingletonMemberNotInHost_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;

namespace Test
{
    public interface IMyService { }
    public class MyServiceImpl : IMyService { }

    [Singleton]
    public partial class MyService
    {
        [Singleton(typeof(IMyService))]
        private MyServiceImpl _impl = new();
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyService");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M010" // MemberHasSingletonButNotInHost
        );
    }

    [Fact]
    public void Process_InjectHostType_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [Host]
    public partial class MyHost : Node { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private MyHost _host;
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M041" // InjectMemberIsHostType
        );
    }

    [Fact]
    public void Process_InjectUserType_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [User]
    public partial class MyHost : Node { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private MyHost _host = new();
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M042" // InjectMemberIsUserType
        );
    }

    [Fact]
    public void Process_InjectClassImplementingIScope_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [Modules(Services = new[] { typeof(MyService) })]
    public partial class MyScope : Node, IScope
    {
        public void RegisterService<T>(T instance) where T : notnull { }
        public void UnregisterService<T>() where T : notnull { }
        public void ResolveDependency<T>(System.Action<T> onResolved) where T : notnull { }
    }

    [Singleton]
    public partial class MyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private MyScope _scope;
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M043" // InjectMemberIsScopeType
        );
    }

    [Fact]
    public void Process_StaticInjectMember_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }

    [User]
    public partial class MyUser : Node
    {
        [Inject]
        private static IMyService _service;
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyUser");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M044" // InjectMemberIsStatic
        );
    }

    [Fact]
    public void Process_StaticSingletonMember_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }
    public class MyServiceImpl : IMyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(IMyService))]
        private static MyServiceImpl _service = new();
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M045" // SingletonMemberIsStatic
        );
    }

    [Fact]
    public void Process_HostSingletonMemberIsServiceType_ReportsDiagnostic()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    [Singleton]
    public partial class MyService { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton]
        private MyService _service = new();
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M050" // HostSingletonMemberIsServiceType
        );
    }

    [Fact]
    public void Process_ExposedTypeIsClass_ReportsWarning()
    {
        // Arrange
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public class MyServiceImpl { }

    [Host]
    public partial class MyHost : Node
    {
        [Singleton(typeof(MyServiceImpl))]
        private MyServiceImpl _service = new();
    }
}
";
        var (result, symbols) = GetValidationResult(source, "MyHost");

        // Assert
        Assert.Contains(
            result.Diagnostics,
            d => d.Id == "GDI_M060" // ExposedTypeShouldBeInterface (Warning)
        );
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
