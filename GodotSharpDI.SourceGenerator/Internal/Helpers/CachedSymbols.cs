using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// Cache commonly used symbol references
/// </summary>
internal sealed class CachedSymbols
{
    // DI
    public INamedTypeSymbol? HostAttribute { get; }
    public INamedTypeSymbol? UserAttribute { get; }
    public INamedTypeSymbol? InjectAttribute { get; }
    public INamedTypeSymbol? ProvideAttribute { get; }
    public INamedTypeSymbol? ModulesAttribute { get; }
    public INamedTypeSymbol? IScope { get; }
    public INamedTypeSymbol? IDependenciesResolved { get; }
    public INamedTypeSymbol? GodotNode { get; }

    // System
    public INamedTypeSymbol? IDisposable { get; }
    public INamedTypeSymbol? GenericTask { get; }
    public INamedTypeSymbol? GenericValueTask { get; }

    public CachedSymbols(Compilation compilation)
    {
        // DI
        ProvideAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ProvideAttribute);
        HostAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.HostAttribute);
        UserAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.UserAttribute);
        InjectAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.InjectAttribute);
        ModulesAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ModulesAttribute);
        IScope = compilation.GetTypeByMetadataName(TypeNamesFull.IScope);
        IDependenciesResolved = compilation.GetTypeByMetadataName(
            TypeNamesFull.IDependenciesResolved
        );
        GodotNode = compilation.GetTypeByMetadataName(TypeNamesFull.GodotNode);

        // System
        IDisposable = compilation.GetTypeByMetadataName(TypeNamesFull.IDisposable);
        GenericTask = compilation.GetTypeByMetadataName(TypeNamesFull.GenericTask);
        GenericValueTask = compilation.GetTypeByMetadataName(TypeNamesFull.GenericValueTask);
    }

    public bool IsNode(ITypeSymbol type)
    {
        if (GodotNode is null)
            return false;
        // Use SymbolExtensions' InheritsFrom method
        return SymbolEqualityComparer.Default.Equals(type, GodotNode)
            || type.InheritsFrom(GodotNode);
    }

    public bool ImplementsIScope(ITypeSymbol type)
    {
        if (IScope is null)
            return false;
        // Use SymbolExtensions' ImplementsInterface method
        return type.ImplementsInterface(IScope);
    }

    public bool ImplementsIDependenciesResolved(ITypeSymbol type)
    {
        if (IDependenciesResolved is null)
            return false;
        // Use SymbolExtensions' ImplementsInterface method
        return type.ImplementsInterface(IDependenciesResolved);
    }

    public bool IsHostType(ITypeSymbol type)
    {
        // Use SymbolExtensions' HasAttribute method
        return type.HasAttribute(HostAttribute);
    }

    public bool IsUserType(ITypeSymbol type)
    {
        // Use SymbolExtensions' HasAttribute method
        return type.HasAttribute(UserAttribute);
    }

    /// <summary>
    /// Check if a member has Inject attribute
    /// </summary>
    public bool HasInjectAttribute(ISymbol member)
    {
        return member.HasAttribute(InjectAttribute);
    }

    /// <summary>
    /// Check if a member has Inject attribute and FailureCallback = true
    /// </summary>
    public bool HasInjectWithFailureCallback(ISymbol member)
    {
        var injectAttr = member.GetAttribute(InjectAttribute);
        if (injectAttr == null)
            return false;

        // Check FailureCallback property
        foreach (var namedArg in injectAttr.NamedArguments)
        {
            if (namedArg.Key == "FailureCallback" && namedArg.Value.Value is bool value)
            {
                return value;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a member has Inject attribute and ReadyCallback = true
    /// </summary>
    public bool HasInjectWithReadyCallback(ISymbol member)
    {
        var injectAttr = member.GetAttribute(InjectAttribute);
        if (injectAttr == null)
            return false;

        // Check ReadyCallback property
        foreach (var namedArg in injectAttr.NamedArguments)
        {
            if (namedArg.Key == "ReadyCallback" && namedArg.Value.Value is bool value)
            {
                return value;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a type is Task&lt;T&gt; (ValueTask&lt;T&gt;)
    /// </summary>
    public bool IsAsyncType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        // Compare original definition (not affected by generic parameters)
        var original = named.OriginalDefinition;

        return SymbolEqualityComparer.Default.Equals(original, GenericTask)
            || SymbolEqualityComparer.Default.Equals(original, GenericValueTask);
    }
}
