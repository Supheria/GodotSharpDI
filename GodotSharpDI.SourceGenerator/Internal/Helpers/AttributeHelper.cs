// 文件: GodotSharpDI.SourceGenerator/Internal/Helpers/AttributeHelper.cs
// 修复: 支持 ProvidesAttribute

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
    /// 获取成员暴露的服务类型
    /// 支持 [Provides] 和 [Singleton] 特性
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetMemberExposedTypes(
        ISymbol member,
        CachedSymbols symbols
    )
    {
        // 优先尝试 Provides 特性（新架构）
        var providesAttr = member.GetAttribute(symbols.ProvidesAttribute);
        var exposedTypes = GetTypesFromAttribute(providesAttr, ShortNames.ExposedTypes);
        if (exposedTypes.IsEmpty)
        {
            // TODO: Remove in rc.2
            // 回退到 Singleton 特性（向后兼容）
            var singletonAttr = member.GetAttribute(symbols.SingletonAttribute);
            exposedTypes = GetTypesFromAttribute(singletonAttr, ShortNames.ExposedTypes);
        }

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
            else if (member is IMethodSymbol method) // ← 修复：添加方法支持
            {
                memberType = method.ReturnType;
            }
            if (memberType is INamedTypeSymbol namedType)
            {
                return ImmutableArray.Create(namedType);
            }
        }

        return exposedTypes;
    }

    /// <summary>
    /// 获取 Service 类型暴露的服务类型
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetServiceExposedTypes(
        INamedTypeSymbol service,
        CachedSymbols symbols
    )
    {
        // Service 使用 Singleton 特性（旧架构，但仍然支持）
        var singletonAttr = service.GetAttribute(symbols.SingletonAttribute);
        var exposedTypes = GetTypesFromAttribute(singletonAttr, ShortNames.ExposedTypes);

        // 如果没有指定服务类型，使用自身类型
        return exposedTypes.IsEmpty ? ImmutableArray.Create(service) : exposedTypes;
    }

    /// <summary>
    /// 从特性提取类型数组（用于 Singleton 等传统特性）
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
