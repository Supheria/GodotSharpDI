using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// 缓存常用的符号引用
/// </summary>
internal sealed class CachedSymbols
{
    // 新增特性
    public INamedTypeSymbol? ProviderAttribute { get; }
    public INamedTypeSymbol? ProvidesAttribute { get; }

    // 现有特性
    public INamedTypeSymbol? SingletonAttribute { get; }
    public INamedTypeSymbol? HostAttribute { get; }
    public INamedTypeSymbol? UserAttribute { get; }
    public INamedTypeSymbol? InjectAttribute { get; }
    public INamedTypeSymbol? InjectConstructorAttribute { get; }
    public INamedTypeSymbol? ModulesAttribute { get; }
    public INamedTypeSymbol? IScope { get; }
    public INamedTypeSymbol? IDependenciesResolved { get; }
    public INamedTypeSymbol? GodotNode { get; }
    public INamedTypeSymbol? IDisposable { get; }

    public CachedSymbols(Compilation compilation)
    {
        // 新增特性
        ProviderAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ProviderAttribute);
        ProvidesAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ProvidesAttribute);

        // 现有特性
        SingletonAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.SingletonAttribute);
        HostAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.HostAttribute);
        UserAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.UserAttribute);
        InjectAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.InjectAttribute);
        InjectConstructorAttribute = compilation.GetTypeByMetadataName(
            TypeNamesFull.InjectConstructorAttribute
        );
        ModulesAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ModulesAttribute);
        IScope = compilation.GetTypeByMetadataName(TypeNamesFull.IScope);
        IDependenciesResolved = compilation.GetTypeByMetadataName(
            TypeNamesFull.IDependenciesResolved
        );
        GodotNode = compilation.GetTypeByMetadataName(TypeNamesFull.GodotNode);
        IDisposable = compilation.GetTypeByMetadataName("System.IDisposable");
    }

    public bool IsNode(ITypeSymbol type)
    {
        if (GodotNode is null)
            return false;
        // 使用 SymbolExtensions 的 InheritsFrom 方法
        return SymbolEqualityComparer.Default.Equals(type, GodotNode)
            || type.InheritsFrom(GodotNode);
    }

    public bool ImplementsIScope(ITypeSymbol type)
    {
        if (IScope is null)
            return false;
        // 使用 SymbolExtensions 的 ImplementsInterface 方法
        return type.ImplementsInterface(IScope);
    }

    public bool ImplementsIDependenciesResolved(ITypeSymbol type)
    {
        if (IDependenciesResolved is null)
            return false;
        // 使用 SymbolExtensions 的 ImplementsInterface 方法
        return type.ImplementsInterface(IDependenciesResolved);
    }

    public bool IsHostType(ITypeSymbol type)
    {
        // 使用 SymbolExtensions 的 HasAttribute 方法
        return type.HasAttribute(HostAttribute);
    }

    public bool IsUserType(ITypeSymbol type)
    {
        // 使用 SymbolExtensions 的 HasAttribute 方法
        return type.HasAttribute(UserAttribute);
    }

    public bool IsServiceType(ITypeSymbol type)
    {
        return type.HasAttribute(SingletonAttribute);
    }

    public bool IsProviderType(ITypeSymbol type)
    {
        // 使用 SymbolExtensions 的 HasAttribute 方法
        return type.HasAttribute(ProviderAttribute);
    }
}
