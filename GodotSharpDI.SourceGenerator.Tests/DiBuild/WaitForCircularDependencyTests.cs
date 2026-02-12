using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

/// <summary>
/// WaitFor 机制下的循环依赖检测测试
/// </summary>
public class WaitForCircularDependencyTests
{
    [Fact]
    public void WaitFor_SimpleCircularDependency_ShouldDetect()
    {
        // Arrange - A 等待 B, B 等待 A
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }

    public partial class ServiceA : IServiceA { }
    public partial class ServiceB : IServiceB { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject]
        private IServiceA _serviceA { get; set; }
        
        [Inject]
        private IServiceB _serviceB { get; set; }
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_serviceB) })]
        public ServiceA CreateA()
        {
            return new ServiceA();
        }
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_serviceA) })]
        public ServiceB CreateB()
        {
            return new ServiceB();
        }
    }
}
";
        var result = BuildGraph(source);

        // Assert - 应该检测到循环依赖
        var circularDiagnostics = result.Diagnostics.Where(d => d.Id == "GDI_D010").ToList();

        Assert.NotEmpty(circularDiagnostics);
        var message = circularDiagnostics[0].GetMessage();
        Assert.Contains("ServiceA", message);
        Assert.Contains("ServiceB", message);
    }

    [Fact]
    public void WaitFor_ThreeNodeCircle_ShouldDetect()
    {
        // Arrange - A 等待 B, B 等待 C, C 等待 A
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface IServiceC { }

    public partial class ServiceA : IServiceA { }
    public partial class ServiceB : IServiceB { }
    public partial class ServiceC : IServiceC { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject]
        private IServiceA _a { get; set; }
        
        [Inject]
        private IServiceB _b { get; set; }
        
        [Inject]
        private IServiceC _c { get; set; }
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_b) })]
        public ServiceA CreateA() => new ServiceA();
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_c) })]
        public ServiceB CreateB() => new ServiceB();
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceC) }, WaitFor = new string[] { nameof(_a) })]
        public ServiceC CreateC() => new ServiceC();
    }
}
";
        var result = BuildGraph(source);

        // Assert
        var circularDiagnostics = result.Diagnostics.Where(d => d.Id == "GDI_D010").ToList();

        Assert.NotEmpty(circularDiagnostics);
    }

    [Fact]
    public void WaitFor_NoDependency_NoCircle()
    {
        // Arrange - 正常的依赖链: A <- B <- C
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface IServiceC { }

    public partial class ServiceA : IServiceA { }
    public partial class ServiceB : IServiceB { }
    public partial class ServiceC : IServiceC { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject]
        private IServiceA _a { get; set; }
        
        [Inject]
        private IServiceC _c { get; set; }
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) })]
        public ServiceA CreateA() => new ServiceA();
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_c) })]
        public ServiceB CreateB() => new ServiceB();
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceC) }, WaitFor = new string[] { nameof(_a) })]
        public ServiceC CreateC() => new ServiceC();
    }
}
";
        var result = BuildGraph(source);

        // Assert - 不应该有循环依赖
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
    }

    [Fact]
    public void WaitFor_SelfReference_ShouldDetect()
    {
        // Arrange - A 等待自己（自环）
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public partial class ServiceA : IServiceA { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject]
        private IServiceA _a { get; set; }
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_a) })]
        public ServiceA CreateA() => new ServiceA();
    }
}
";
        var result = BuildGraph(source);

        // Assert
        var circularDiagnostics = result.Diagnostics.Where(d => d.Id == "GDI_D010").ToList();

        Assert.NotEmpty(circularDiagnostics);
    }

    [Fact]
    public void WaitFor_FieldNotFound_ShouldReportError()
    {
        // Arrange - WaitFor 引用不存在的字段
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public partial class ServiceA : IServiceA { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { ""_nonExistent"" })]
        public ServiceA CreateA() => new ServiceA();
    }
}
";
        var result = BuildGraph(source);

        // Assert - 应该报告字段不存在
        var errorDiagnostics = result.Diagnostics.Where(d => d.Id == "GDI_M080").ToList();

        Assert.NotEmpty(errorDiagnostics);
    }

    [Fact]
    public void WaitFor_FieldNotInjected_ShouldWarn()
    {
        // Arrange - WaitFor 引用的字段没有 [Inject]
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public partial class ServiceA : IServiceA { }

    [Host]
    public partial class ServiceHost : Node
    {
        private IServiceA _notInjected { get; set; }
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) }, WaitFor = new string[] { nameof(_notInjected) })]
        public ServiceA CreateA() => new ServiceA();
    }
}
";
        var result = BuildGraph(source);

        // Assert - 应该发出警告
        var warnings = result.Diagnostics.Where(d => d.Id == "GDI_M081").ToList();

        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void WaitFor_MultipleWaitFor_NormalDependencyChain()
    {
        // Arrange - B 等待 A 和 C
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;
using System;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public interface IServiceC { }

    public partial class ServiceA : IServiceA { }
    public partial class ServiceB : IServiceB { }
    public partial class ServiceC : IServiceC { }

    [Host]
    public partial class ServiceHost : Node
    {
        [Inject]
        private IServiceA _a { get; set; }
        
        [Inject]
        private IServiceC _c { get; set; }
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceA) })]
        public ServiceA CreateA() => new ServiceA();
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceB) }, WaitFor = new string[] { nameof(_a), nameof(_c) })]
        public ServiceB CreateB() => new ServiceB();
        
        [Provide(ExposedTypes = new Type[] { typeof(IServiceC) })]
        public ServiceC CreateC() => new ServiceC();
    }
}
";
        var result = BuildGraph(source);

        // Assert - 不应该有循环依赖
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "GDI_D010"));
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
