using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using GodotSharpDI.Abstractions;
using GodotSharpDI.Runtime;
using GodotSharpDI.SourceGenerator.Tests.Helpers.Mocks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GodotSharpDI.SourceGenerator.Tests.Helpers;

/// <summary>
/// End-to-end test helper: runs the source generator, extracts generated code,
/// compiles it with real Runtime/Abstractions DLLs and Godot mocks, returns an assembly.
/// </summary>
internal static class E2ETestHelper
{
    /// <summary>
    /// Run the source generator on <paramref name="userSource"/>, then compile the
    /// generated code together with Godot mocks and real Runtime/Abstractions DLLs.
    /// Returns the compiled assembly.
    /// </summary>
    public static Assembly GenerateAndCompile(string userSource)
    {
        // Step 1: Run the generator to produce source files
        var generatedSources = RunGenerator(userSource);

        if (generatedSources.IsEmpty)
            throw new InvalidOperationException("Source generator produced no output.");

        // Step 2: Build the final compilation with user source + generated code + mocks + real DLLs
        var syntaxTrees = new List<SyntaxTree>();

        // Add user source (defines types referenced by generated code)
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(userSource));

        // Add Godot mock source (provides Node, Callable, Timer, GD)
        syntaxTrees.Add(CSharpSyntaxTree.ParseText(E2EGodotMocks.GetSource()));

        // Add all generated sources
        foreach (var src in generatedSources)
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(src.SourceText.ToString(), path: src.HintName));

        // References: real Abstractions + Runtime DLLs + standard .NET
        var references = GetReferences();

        var compilation = CSharpCompilation.Create(
            "E2EAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        // Compile
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();
            throw new InvalidOperationException(
                $"E2E compilation failed:\n{string.Join("\n", errors)}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(ms.ToArray());
    }

    /// <summary>
    /// Run the source generator and return all generated source files.
    /// Uses real Abstractions DLL + Godot mocks as the generator input compilation.
    /// </summary>
    private static ImmutableArray<GeneratedSourceResult> RunGenerator(string userSource)
    {
        // Create compilation with user source + Godot mocks + real Abstractions DLL
        var syntaxTrees = new List<SyntaxTree>
        {
            CSharpSyntaxTree.ParseText(userSource),
            CSharpSyntaxTree.ParseText(E2EGodotMocks.GetSource()),
        };

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Action).Assembly.Location),
        };

        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var name in new[] { "System.Runtime.dll", "System.Collections.dll", "netstandard.dll" })
        {
            var path = Path.Combine(runtimeDir, name);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        // Real Abstractions DLL (provides IScope, IDependenciesResolved, attributes)
        references.Add(MetadataReference.CreateFromFile(typeof(IScope).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "GeneratorInput",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var generator = new DiSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // Check for generator errors
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Source generator errors:\n{string.Join("\n", errors.Select(d => d.ToString()))}");
        }

        var runResult = driver.GetRunResult();
        return runResult.Results[0].GeneratedSources;
    }

    /// <summary>
    /// Get all metadata references for the final compilation.
    /// </summary>
    private static List<MetadataReference> GetReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Action).Assembly.Location),
        };

        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var systemRefs = new[]
        {
            "System.Runtime.dll",
            "System.Collections.dll",
            "System.Threading.dll",
            "System.Threading.Tasks.dll",
            "System.Threading.Thread.dll",
            "netstandard.dll",
        };

        foreach (var name in systemRefs)
        {
            var path = Path.Combine(runtimeDir, name);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        // Real Abstractions and Runtime DLLs
        references.Add(MetadataReference.CreateFromFile(typeof(IScope).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(InjectionExecutor).Assembly.Location));

        var unsafePath = Path.Combine(runtimeDir, "System.Runtime.CompilerServices.Unsafe.dll");
        if (File.Exists(unsafePath))
            references.Add(MetadataReference.CreateFromFile(unsafePath));

        return references;
    }

    // ─── Reflection helpers ─────────────────────────────────────────

    /// <summary>
    /// Instantiate a type from the compiled assembly by full name.
    /// </summary>
    public static object Instantiate(Assembly asm, string fullTypeName)
    {
        var type = asm.GetType(fullTypeName)
            ?? throw new InvalidOperationException($"Type '{fullTypeName}' not found in assembly.");
        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"Failed to create instance of '{fullTypeName}'.");
    }

    /// <summary>
    /// Get a non-public instance field value.
    /// </summary>
    public static object? GetFieldValue(object instance, string fieldName)
    {
        var type = instance.GetType();
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {type.Name}.");
        return field.GetValue(instance);
    }

    /// <summary>
    /// Set a non-public instance field value.
    /// </summary>
    public static void SetFieldValue(object instance, string fieldName, object? value)
    {
        var type = instance.GetType();
        var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {type.Name}.");
        field.SetValue(instance, value);
    }

    /// <summary>
    /// Get a non-public instance property value.
    /// </summary>
    public static object? GetPropertyValue(object instance, string propertyName)
    {
        var type = instance.GetType();
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {type.Name}.");
        return prop.GetValue(instance);
    }

    /// <summary>
    /// Invoke a non-public instance method.
    /// </summary>
    public static object? InvokeMethod(object instance, string methodName, params object?[] args)
    {
        var type = instance.GetType();
        var method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {type.Name}.");
        return method.Invoke(instance, args);
    }

    /// <summary>
    /// Invoke a public instance method.
    /// </summary>
    public static object? InvokePublicMethod(object instance, string methodName, params object?[] args)
    {
        var type = instance.GetType();
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {type.Name}.");
        return method.Invoke(instance, args);
    }

    /// <summary>
    /// Call _Notification on a Godot.Node (uses the generated override).
    /// </summary>
    public static void Notify(object node, int what)
    {
        InvokePublicMethod(node, "_Notification", what);
    }

    /// <summary>
    /// Wire a child node to a parent scope by setting the parent reference.
    /// </summary>
    public static void WireParent(object child, object parent)
    {
        var type = child.GetType();
        var currentType = type;
        while (currentType != null)
        {
            var method = currentType.GetMethod("SetParent", BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(child, new[] { parent });
                return;
            }
            currentType = currentType.BaseType;
        }
        throw new InvalidOperationException($"SetParent not found on {type.Name} or its base types.");
    }
}
