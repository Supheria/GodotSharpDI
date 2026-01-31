using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Helpers;

/// <summary>
/// 特性辅助类 - 用于处理特性相关的操作
/// </summary>
internal static class AttributeHelper
{
    public static ImmutableArray<INamedTypeSymbol> GetExposedTypes(
        ISymbol member,
        CachedSymbols symbols
    )
    {
        var singletonAttr = member
            .GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.SingletonAttribute)
            );
        var exposedTypes = GetTypesFromAttribute(singletonAttr, ArgumentNames.ServiceTypes);

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

            if (memberType is INamedTypeSymbol namedType)
            {
                return ImmutableArray.Create(namedType);
            }
        }

        return exposedTypes;
    }

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
