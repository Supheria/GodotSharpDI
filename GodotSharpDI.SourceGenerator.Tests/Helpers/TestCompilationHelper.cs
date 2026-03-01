using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GodotSharpDI.SourceGenerator.Tests.Helpers;

/// <summary>
/// Helper for creating test compilations
/// </summary>
internal static class TestCompilationHelper
{
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "TestAssembly"
    )
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        // Add System.Runtime reference
        var systemRuntime = RuntimeEnvironment.GetRuntimeDirectory();
        references.Add(
            MetadataReference.CreateFromFile(Path.Combine(systemRuntime, "System.Runtime.dll"))
        );

        return CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    public static CSharpCompilation CreateCompilationWithDI(
        string source,
        string assemblyName = "TestAssembly"
    )
    {
        var diSource = GetDIAttributesSource();
        var syntaxTrees = new[]
        {
            CSharpSyntaxTree.ParseText(source),
            CSharpSyntaxTree.ParseText(diSource),
        };

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        };

        var systemRuntime = RuntimeEnvironment.GetRuntimeDirectory();
        references.Add(
            MetadataReference.CreateFromFile(Path.Combine(systemRuntime, "System.Runtime.dll"))
        );

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    /// <summary>
    /// 运行源生成器并获取生成的诊断
    /// </summary>
    /// <param name="compilation">要分析的编译</param>
    /// <returns>源生成器产生的所有诊断</returns>
    public static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(Compilation compilation)
    {
        // 创建源生成器实例
        var generator = new DiSourceGenerator();

        // 创建生成器驱动
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // 运行生成器
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        // 返回所有诊断
        return diagnostics;
    }

    /// <summary>
    /// 运行源生成器并获取指定类型的诊断
    /// </summary>
    /// <param name="compilation">要分析的编译</param>
    /// <param name="severity">诊断严重级别过滤器</param>
    /// <returns>过滤后的诊断</returns>
    public static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(
        Compilation compilation,
        DiagnosticSeverity severity
    )
    {
        var allDiagnostics = GetGeneratorDiagnostics(compilation);
        return allDiagnostics.Where(d => d.Severity == severity).ToImmutableArray();
    }

    /// <summary>
    /// 运行源生成器并获取错误诊断
    /// </summary>
    public static ImmutableArray<Diagnostic> GetGeneratorErrors(Compilation compilation)
    {
        return GetGeneratorDiagnostics(compilation, DiagnosticSeverity.Error);
    }

    /// <summary>
    /// 运行源生成器并获取警告诊断
    /// </summary>
    public static ImmutableArray<Diagnostic> GetGeneratorWarnings(Compilation compilation)
    {
        return GetGeneratorDiagnostics(compilation, DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// 运行源生成器并检查是否有特定ID的诊断
    /// </summary>
    public static bool HasDiagnostic(Compilation compilation, string diagnosticId)
    {
        var diagnostics = GetGeneratorDiagnostics(compilation);
        return diagnostics.Any(d => d.Id == diagnosticId);
    }

    /// <summary>
    /// 运行源生成器并获取特定ID的所有诊断
    /// </summary>
    public static ImmutableArray<Diagnostic> GetDiagnosticsById(
        Compilation compilation,
        string diagnosticId
    )
    {
        var diagnostics = GetGeneratorDiagnostics(compilation);
        return diagnostics.Where(d => d.Id == diagnosticId).ToImmutableArray();
    }

    /// <summary>
    /// 运行源生成器并获取生成的所有源代码
    /// </summary>
    public static ImmutableArray<GeneratedSourceResult> GetGeneratedSources(Compilation compilation)
    {
        var generator = new DiSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();
        return runResult.Results[0].GeneratedSources;
    }

    /// <summary>
    /// 运行源生成器并检查是否生成了特定名称的源文件
    /// </summary>
    public static bool HasGeneratedSource(Compilation compilation, string hintName)
    {
        var sources = GetGeneratedSources(compilation);
        return sources.Any(s => s.HintName == hintName);
    }

    private static string GetDIAttributesSource()
    {
        return @"
using System;

namespace GodotSharpDI.Abstractions
{
    /// <summary>标记一个 Node 为 DI 服务提供者</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HostAttribute : Attribute { }

    /// <summary>标记一个 Node 为 DI 服务消费者</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UserAttribute : Attribute { }

    /// <summary>标记一个字段/属性需要注入</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute
    {
        /// <summary>注入失败时是否调用回调</summary>
        public bool FailureCallback { get; set; }
        /// <summary>注入就绪时是否调用回调</summary>
        public bool ReadyCallback { get; set; }
    }

    /// <summary>标记一个属性/方法对外暴露服务</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ProvideAttribute : Attribute
    {
        /// <summary>暴露的服务类型列表</summary>
        public Type[] ExposedTypes { get; set; } = Array.Empty<Type>();
        /// <summary>该 Provide 成员需等待哪些 Inject 字段先完成</summary>
        public string[] WaitFor { get; set; } = Array.Empty<string>();
    }

    /// <summary>标记一个 Node 为 DI Scope（服务注册表）</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ModulesAttribute : Attribute
    {
        public Type[] Services { get; set; } = Array.Empty<Type>();
        public Type[] Hosts { get; set; } = Array.Empty<Type>();
    }

    public interface IScope
    {
        void RegisterService<T>(T instance) where T : notnull;
        void UnregisterService<T>() where T : notnull;
        void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
    }

    /// <summary>所有依赖注入完成后的回调接口</summary>
    public interface IDependenciesResolved
    {
        void OnDependenciesResolved(bool isAllDependenciesReady);
    }
}

namespace Godot
{
    public class Node
    {
        public Node? GetParent() => null;
        public virtual void _Notification(int what) { }
        protected const int NotificationEnterTree = 10;
        protected const int NotificationExitTree = 11;
        protected const int NotificationReady = 13;
        protected const int NotificationPredelete = 1;
    }

    public static class GD
    {
        public static void PushError(string message) { }
        public static void PushError(Exception ex) { }
        public static void PrintErr(string message) { }
        public static void Print(string message) { }
    }
}
";
    }

    public static INamedTypeSymbol? GetTypeSymbol(
        Compilation compilation,
        string fullyQualifiedName
    )
    {
        return compilation.GetTypeByMetadataName(fullyQualifiedName);
    }
}
