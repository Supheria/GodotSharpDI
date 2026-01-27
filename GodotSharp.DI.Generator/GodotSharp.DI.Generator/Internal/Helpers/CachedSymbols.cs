using System.Linq;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Helpers;

/// <summary>
/// 缓存常用的符号引用
/// </summary>
internal sealed class CachedSymbols
{
    public INamedTypeSymbol? SingletonAttribute { get; }
    public INamedTypeSymbol? TransientAttribute { get; }
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
        TransientAttribute = compilation.GetTypeByMetadataName(TypeNamesFull.TransientAttribute);
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
        // 检查类型本身是否是 Node，或者是否继承自 Node
        return SymbolEqualityComparer.Default.Equals(type, GodotNode)
            || type.InheritsFrom(GodotNode);
    }

    public bool ImplementsIScope(ITypeSymbol type)
    {
        if (IScope is null)
            return false;
        return type.AllInterfaces.Contains(IScope, SymbolEqualityComparer.Default);
    }

    public bool ImplementsIServicesReady(ITypeSymbol type)
    {
        if (IServicesReady is null)
            return false;
        return type.AllInterfaces.Contains(IServicesReady, SymbolEqualityComparer.Default);
    }

    public bool IsHostType(ITypeSymbol type)
    {
        if (HostAttribute is null)
            return false;
        return type.GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, HostAttribute));
    }

    public bool IsUserType(ITypeSymbol type)
    {
        if (UserAttribute is null)
            return false;
        return type.GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, UserAttribute));
    }

    public bool IsServiceType(ITypeSymbol type)
    {
        if (SingletonAttribute is null && TransientAttribute is null)
            return false;
        return type.GetAttributes()
            .Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, SingletonAttribute)
                || SymbolEqualityComparer.Default.Equals(attr.AttributeClass, TransientAttribute)
            );
    }
}

internal static class TypeSymbolExtensions
{
    public static bool InheritsFrom(this ITypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    public static bool IsValidInjectType(this ITypeSymbol type, CachedSymbols symbols)
    {
        // 不能是 Node、Host、User、Scope
        if (symbols.IsNode(type))
            return false;

        // 不能是抽象类、静态类
        if (type is INamedTypeSymbol named)
        {
            if (named.IsAbstract && named.TypeKind == TypeKind.Class)
                return false;
            if (named.IsStatic)
                return false;
        }

        // 不能是开放泛型
        if (type is INamedTypeSymbol generic && generic.IsUnboundGenericType)
            return false;

        // 必须是 interface 或 class
        return type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.Class;
    }

    public static bool IsValidServiceType(this ITypeSymbol type, CachedSymbols symbols)
    {
        // 必须是 class
        if (type.TypeKind != TypeKind.Class)
            return false;

        var named = (INamedTypeSymbol)type;

        // 不能是 Node
        if (symbols.IsNode(type))
            return false;

        // 不能是抽象类、静态类
        if (named.IsAbstract || named.IsStatic)
            return false;

        // 不能是开放泛型
        if (named.IsUnboundGenericType)
            return false;

        return true;
    }

    public static bool IsValidExposedType(this ITypeSymbol type)
    {
        // 必须是 interface
        return type.TypeKind == TypeKind.Interface;
    }
}
