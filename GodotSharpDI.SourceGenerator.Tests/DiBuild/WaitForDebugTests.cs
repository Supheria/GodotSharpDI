using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace GodotSharpDI.SourceGenerator.Tests.DiBuild;

public class WaitForDebugTests
{
    private readonly ITestOutputHelper _output;

    public WaitForDebugTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_SimpleCircularDependency()
    {
        var source =
            @"
using GodotSharpDI.Abstractions;
using Godot;

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
        
        [Provide(ExposedTypes = [typeof(IServiceA)], WaitFor = [nameof(_serviceB)])]
        public ServiceA CreateA()
        {
            return new ServiceA();
        }
        
        [Provide(ExposedTypes = [typeof(IServiceB)], WaitFor = [nameof(_serviceA)])]
        public ServiceB CreateB()
        {
            return new ServiceB();
        }
    }
}
";
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
                    _output.WriteLine($"\n=== Processing class: {raw.Info.Symbol.Name} ===");

                    var result = ClassPipeline.ValidateAndClassify(raw.Info, symbols);

                    if (result.TypeInfo != null)
                    {
                        _output.WriteLine($"Role: {result.TypeInfo.Role}");
                        _output.WriteLine($"Members: {result.TypeInfo.Members.Length}");

                        foreach (var member in result.TypeInfo.Members)
                        {
                            _output.WriteLine($"  Member: {member.Name}");
                            _output.WriteLine($"    Kind: {member.Kind}");
                            _output.WriteLine($"    IsProvide: {member.IsProvideMember}");
                            _output.WriteLine($"    IsInject: {member.IsInjectMember}");
                            _output.WriteLine($"    ExposedTypes: {member.ExposedTypes.Length}");
                            foreach (var et in member.ExposedTypes)
                            {
                                _output.WriteLine($"      - {et.Name}");
                            }
                            _output.WriteLine($"    WaitFor: {member.WaitFor.Length}");
                            foreach (var wf in member.WaitFor)
                            {
                                _output.WriteLine($"      - {wf}");
                            }

                            // 调试特性信息
                            if (member.IsProvideMember)
                            {
                                var provideAttr = member
                                    .Symbol.GetAttributes()
                                    .FirstOrDefault(a =>
                                        a.AttributeClass?.Name == "ProvideAttribute"
                                    );
                                if (provideAttr != null)
                                {
                                    _output.WriteLine($"    Provide Attribute Debug:");
                                    _output.WriteLine(
                                        $"      NamedArguments count: {provideAttr.NamedArguments.Length}"
                                    );
                                    foreach (var arg in provideAttr.NamedArguments)
                                    {
                                        _output.WriteLine(
                                            $"      - {arg.Key}: {arg.Value.Kind} = {arg.Value.Value}"
                                        );
                                        if (
                                            arg.Value.Kind
                                            == Microsoft.CodeAnalysis.TypedConstantKind.Array
                                        )
                                        {
                                            _output.WriteLine(
                                                $"        Array length: {arg.Value.Values.Length}"
                                            );
                                            foreach (var val in arg.Value.Values)
                                            {
                                                _output.WriteLine(
                                                    $"          - {val.Kind}: {val.Value}"
                                                );
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    classResults.Add(result);

                    _output.WriteLine($"Diagnostics from this class: {result.Diagnostics.Length}");
                    foreach (var diag in result.Diagnostics)
                    {
                        _output.WriteLine($"  [{diag.Id}] {diag.GetMessage()}");
                    }
                }
            }
        }

        _output.WriteLine("\n=== Building Graph ===");
        var graphResult = DiGraphBuilder.Build(classResults.ToImmutable(), symbols);

        // 输出所有诊断信息
        _output.WriteLine($"\nTotal diagnostics: {graphResult.Diagnostics.Length}");
        foreach (var diag in graphResult.Diagnostics)
        {
            _output.WriteLine($"[{diag.Id}] {diag.GetMessage()}");
        }

        // 输出图结构
        _output.WriteLine($"\nService nodes: {graphResult.Graph.HostNodes.Length}");
        foreach (var node in graphResult.Graph.HostNodes)
        {
            _output.WriteLine($"Node: {node.ValidatedTypeInfo.Symbol.Name}");
            _output.WriteLine($"  Dependencies: {node.Dependencies.Length}");
            foreach (var dep in node.Dependencies)
            {
                _output.WriteLine($"    - {dep.TargetType.Name} ({dep.Source})");
            }
            _output.WriteLine($"  Provided: {node.ProvidedServices.Length}");
            foreach (var svc in node.ProvidedServices)
            {
                _output.WriteLine($"    - {svc.Name}");
            }
        }
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
