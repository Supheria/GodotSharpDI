using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GodotSharpDI.Generator.Tests.Helpers;

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
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
        };

        // Add System.Runtime reference
        var systemRuntime = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        references.Add(
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(systemRuntime, "System.Runtime.dll")
            )
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
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
        };

        var systemRuntime = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        references.Add(
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(systemRuntime, "System.Runtime.dll")
            )
        );

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    private static string GetDIAttributesSource()
    {
        return @"
using System;

namespace GodotSharpDI.Abstractions
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SingletonAttribute : Attribute
    {
        public Type[] ServiceTypes { get; }
        public SingletonAttribute(params Type[] serviceTypes)
        {
            ServiceTypes = serviceTypes;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HostAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class UserAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class InjectAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class InjectConstructorAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ModulesAttribute : Attribute
    {
        public Type[] Services { get; set; } = [];
        public Type[] Hosts { get; set; } = [];
    }

    public interface IScope
    {
        void RegisterService<T>(T instance) where T : notnull;
        void UnregisterService<T>() where T : notnull;
        void ResolveDependency<T>(Action<T> onResolved) where T : notnull;
    }

    public interface IServicesReady
    {
        void OnServicesReady();
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
