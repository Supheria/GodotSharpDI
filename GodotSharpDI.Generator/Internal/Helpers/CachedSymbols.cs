using System.Linq;
using GodotSharpDI.Generator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Helpers;

/// <summary>
/// 缓存常用的符号引用
/// </summary>
internal sealed class CachedSymbols
{
    public INamedTypeSymbol? SingletonAttribute { get; }
    public INamedTypeSymbol? HostAttribute { get; }
    public INamedTypeSymbol? UserAttribute { get; }
    public INamedTypeSymbol? InjectAttribute { get; }
    public INamedTypeSymbol? InjectConstructorAttribute { get; }
    public INamedTypeSymbol? ModulesAttribute { get; }
    public INamedTypeSymbol? IScope { get; }
    public INamedTypeSymbol? IServicesReady { get; }
    public INamedTypeSymbol? GodotNode { get; }
    public INamedTypeSymbol? IDisposable { get; }

    public CachedSymbols(Compilation compilation)
    {
        SingletonAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.SingletonAttribute);
        HostAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.HostAttribute);
        UserAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.UserAttribute);
        InjectAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.InjectAttribute);
        InjectConstructorAttribute = compilation.GetTypeByMetadataName(
            TypeNamesFull.InjectConstructorAttribute
        );
        ModulesAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.ModulesAttribute);
        IScope = compilation.GetTypeByMetadataName(TypeNamesFull.ScopeInterface);
        IServicesReady = compilation.GetTypeByMetadataName(TypeNamesFull.ServicesReadyInterface);
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

    public bool ImplementsIServicesReady(ITypeSymbol type)
    {
        if (IServicesReady is null)
            return false;
        // 使用 SymbolExtensions 的 ImplementsInterface 方法
        return type.ImplementsInterface(IServicesReady);
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
}
