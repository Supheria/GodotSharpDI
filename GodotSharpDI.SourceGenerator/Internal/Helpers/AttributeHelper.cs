using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// 特性辅助类 - 用于处理特性相关的操作
/// </summary>
internal static class AttributeHelper
{
    /// <summary>
    /// 获取 Provide 成员暴露的服务类型
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetMemberExposedTypes(
        ISymbol member,
        CachedSymbols symbols
    )
    {
        var provideAttr = member.GetAttribute(symbols.ProvideAttribute);
        var exposedTypes = GetTypesFromAttribute(provideAttr, ShortNames.ExposedTypes);

        // 如果没有指定服务类型，使用成员的类型
        if (exposedTypes.IsEmpty)
        {
            ITypeSymbol? memberType = null;
            if (member is IFieldSymbol field)
            {
                memberType = field.Type;
            }
            else if (member is IPropertySymbol property)
            {
                memberType = property.Type;
            }
            else if (member is IMethodSymbol method)
            {
                memberType = method.ReturnType;
            }
            if (memberType is INamedTypeSymbol namedType)
            {
                // 异步成员（Task<T> / ValueTask<T>）未指定 ExposedTypes 时，
                // 服务类型应为内部的 T，而非 Task<T> 本身
                if (
                    symbols.IsAsyncType(namedType)
                    && namedType.IsGenericType
                    && namedType.TypeArguments[0] is INamedTypeSymbol innerType
                )
                {
                    return ImmutableArray.Create(innerType);
                }
                return ImmutableArray.Create(namedType);
            }
        }

        return exposedTypes;
    }

    /// <summary>
    /// 从特性参数中提取类型数组
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetTypesFromAttribute(
        AttributeData? attr,
        string propertyName
    )
    {
        if (attr == null)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        // 构造函数参数
        if (attr.ConstructorArguments.Length > 0)
        {
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var item in arg.Values)
                    {
                        if (item.Value is INamedTypeSymbol type)
                            builder.Add(type);
                    }
                }
            }
        }

        // 命名参数
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key == propertyName && namedArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (var item in namedArg.Value.Values)
                {
                    if (item.Value is INamedTypeSymbol type)
                        builder.Add(type);
                }
            }
        }

        return builder.ToImmutable();
    }
}
