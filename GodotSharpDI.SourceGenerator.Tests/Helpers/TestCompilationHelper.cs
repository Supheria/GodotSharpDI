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
    /// Run the source generator and get the generated diagnostics
    /// </summary>
    /// <param name="compilation">The compilation to analyze</param>
    /// <returns>All diagnostics produced by the source generator</returns>
    public static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(Compilation compilation)
    {
        // Create source generator instance
        var generator = new DiSourceGenerator();

        // Create generator driver
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Run the generator
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics
        );

        // Return all diagnostics
        return diagnostics;
    }

    /// <summary>
    /// Run the source generator and get diagnostics filtered by severity
    /// </summary>
    /// <param name="compilation">The compilation to analyze</param>
    /// <param name="severity">Diagnostic severity filter</param>
    /// <returns>Filtered diagnostics</returns>
    public static ImmutableArray<Diagnostic> GetGeneratorDiagnostics(
        Compilation compilation,
        DiagnosticSeverity severity
    )
    {
        var allDiagnostics = GetGeneratorDiagnostics(compilation);
        return allDiagnostics.Where(d => d.Severity == severity).ToImmutableArray();
    }

    /// <summary>
    /// Run the source generator and get error diagnostics
    /// </summary>
    public static ImmutableArray<Diagnostic> GetGeneratorErrors(Compilation compilation)
    {
        return GetGeneratorDiagnostics(compilation, DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Run the source generator and get warning diagnostics
    /// </summary>
    public static ImmutableArray<Diagnostic> GetGeneratorWarnings(Compilation compilation)
    {
        return GetGeneratorDiagnostics(compilation, DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// Run the source generator and check if a diagnostic with a specific ID exists
    /// </summary>
    public static bool HasDiagnostic(Compilation compilation, string diagnosticId)
    {
        var diagnostics = GetGeneratorDiagnostics(compilation);
        return diagnostics.Any(d => d.Id == diagnosticId);
    }

    /// <summary>
    /// Run the source generator and get all diagnostics with a specific ID
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
    /// Run the source generator and get all generated source code
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
    /// Run the source generator and check if a source file with a specific name was generated
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
    /// <summary>Marks a Node as a DI service provider</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HostAttribute : Attribute { }

    /// <summary>Marks a Node as a DI service consumer</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UserAttribute : Attribute { }

    /// <summary>Marks a field/property for injection</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute
    {
        /// <summary>Whether to invoke callback on injection failure</summary>
        public bool FailureCallback { get; set; }
        /// <summary>Whether to invoke callback when injection is ready</summary>
        public bool ReadyCallback { get; set; }
    }

    /// <summary>Marks a property/method to expose a service externally</summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ProvideAttribute : Attribute
    {
        /// <summary>List of exposed service types</summary>
        public Type[] ExposedTypes { get; set; } = Array.Empty<Type>();
        /// <summary>Which Inject fields this Provide member must wait for before completing</summary>
        public string[] WaitFor { get; set; } = Array.Empty<string>();
    }

    /// <summary>Marks a Node as a DI Scope (service registry)</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ModulesAttribute : Attribute
    {
        public Type[] Hosts { get; }

        public ModulesAttribute(params Type[] hosts)
        {
            Hosts = hosts;
        }
    }

    public interface IScope
    {
        void RegisterService<T>(T instance) where T : notnull;
        void UnregisterService<T>() where T : notnull;
        void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
    }

    /// <summary>Callback interface invoked after all dependency injection is complete</summary>
    public interface IDependenciesResolved
    {
        void OnDependenciesResolved();
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
