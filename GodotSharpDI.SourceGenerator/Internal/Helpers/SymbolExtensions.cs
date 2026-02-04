using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// 符号扩展方法
/// </summary>
internal static class SymbolExtensions
{
    /// <summary>
    /// 获取类型的完全限定名称（带 global:: 和命名空间）
    /// </summary>
    public static string ToFullyQualifiedName(this ITypeSymbol type)
    {
        return DisplayFormats.GetFullQualifiedName(type);
    }

    /// <summary>
    /// 检查类型是否实现指定接口
    /// </summary>
    public static bool ImplementsInterface(this ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        return type.AllInterfaces.Contains(interfaceType, SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// 检查类型是否继承自指定基类
    /// </summary>
    public static bool InheritsFrom(this ITypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// 检查符号是否有指定特性
    /// </summary>
    public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType == null)
            return false;

        return symbol
            .GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
    }

    /// <summary>
    /// 获取符号的指定特性
    /// </summary>
    public static AttributeData? GetAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType == null)
            return null;

        return symbol
            .GetAttributes()
            .FirstOrDefault(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType)
            );
    }

    /// <summary>
    /// 获取符号的所有指定特性
    /// </summary>
    public static IEnumerable<AttributeData> GetAttributes(
        this ISymbol symbol,
        INamedTypeSymbol? attributeType
    )
    {
        if (attributeType == null)
            return Enumerable.Empty<AttributeData>();

        return symbol
            .GetAttributes()
            .Where(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType)
            );
    }

    /// <summary>
    /// 检查类型是否是接口或有效类
    /// </summary>
    public static bool IsValidInterfaceOrConcreteClass(this ITypeSymbol type)
    {
        // 检查不能是抽象类、静态类
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

    /// <summary>
    /// 检查类型是否是有效的服务类型
    /// </summary>
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

    /// <summary>
    /// 检查类型是否是有效的暴露类型
    /// </summary>
    public static bool IsValidExposedType(this ITypeSymbol type)
    {
        // 必须是 interface
        return type.TypeKind == TypeKind.Interface;
    }

    /// <summary>
    /// 获取类型的所有成员（字段和属性）
    /// </summary>
    public static IEnumerable<ISymbol> GetFieldsAndProperties(this INamedTypeSymbol type)
    {
        return type.GetMembers()
            .Where(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property);
    }

    /// <summary>
    /// 获取类型的所有非静态构造函数
    /// </summary>
    public static IEnumerable<IMethodSymbol> GetInstanceConstructors(this INamedTypeSymbol type)
    {
        return type.Constructors.Where(c => !c.IsStatic);
    }

    /// <summary>
    /// 检查符号是否是公共的
    /// </summary>
    public static bool IsPublic(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Public;
    }

    /// <summary>
    /// 检查符号是否是私有的
    /// </summary>
    public static bool IsPrivate(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Private;
    }

    /// <summary>
    /// 检查符号是否是保护的
    /// </summary>
    public static bool IsProtected(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Protected;
    }

    /// <summary>
    /// 检查符号是否是内部的
    /// </summary>
    public static bool IsInternal(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Internal;
    }
}
