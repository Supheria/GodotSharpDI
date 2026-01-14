using System;
using System.Collections.Generic;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class SymbolExtensions
{
    // 1. 提取服务类型（[Singleton]/[Transient] 的参数）
    public static IReadOnlyList<ITypeSymbol> ExtractServiceTypes(
        this INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var attr =
            SymbolHelper.GetAttribute(type, symbols.SingletonAttribute)
            ?? SymbolHelper.GetAttribute(type, symbols.TransientAttribute);

        if (attr is null || attr.ConstructorArguments.Length == 0)
        {
            // 无参数 → 默认使用实现类型本身
            return new[] { type };
        }

        var list = new List<ITypeSymbol>();
        foreach (var arg in attr.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol t)
                list.Add(t);
        }
        return list;
    }

    // 2. 提取 [InjectConstructor] 或自动选择构造函数
    public static InjectConstructorDescriptor? ExtractInjectConstructor(
        this INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var ctors = type.InstanceConstructors.Where(c => !c.IsStatic).ToArray();

        if (ctors.Length == 0)
            return null;

        // 优先找 [InjectConstructor]
        var injectCtor = ctors.FirstOrDefault(ctor =>
            ctor.GetAttributes()
                .Any(a =>
                    SymbolEqualityComparer.Default.Equals(
                        a.AttributeClass,
                        symbols.InjectConstructorAttribute
                    )
                )
        );

        if (injectCtor is null)
        {
            // 没有标记，若只有一个构造函数则自动使用
            if (ctors.Length == 1)
                injectCtor = ctors[0];
            else
                return null; // 交给 Analyzer 报 GDI002
        }

        var paramTypes = injectCtor.Parameters.Select(p => p.Type).ToArray();

        return new InjectConstructorDescriptor(injectCtor, paramTypes);
    }

    // 3. 提取 [Inject] 字段/属性
    public static IReadOnlyList<InjectedMemberDescriptor> ExtractInjectedMembers(
        this INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var list = new List<InjectedMemberDescriptor>();

        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol field)
            {
                if (!HasInjectAttribute(field, symbols))
                    continue;

                list.Add(new InjectedMemberDescriptor(field.Name, field.Type));
            }
            else if (member is IPropertySymbol prop)
            {
                if (!HasInjectAttribute(prop, symbols))
                    continue;

                list.Add(new InjectedMemberDescriptor(prop.Name, prop.Type));
            }
        }

        return list;
    }

    private static bool HasInjectAttribute(ISymbol member, SymbolCache symbols)
    {
        if (symbols.InjectAttribute is null)
            return false;

        return member
            .GetAttributes()
            .Any(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.InjectAttribute)
            );
    }

    // 4. 提取 Host 提供的服务（字段/属性上的 [Singleton]/[Transient]）
    public static IReadOnlyList<ProvidedServiceDescriptor> ExtractProvidedServices(
        this INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var list = new List<ProvidedServiceDescriptor>();

        foreach (var member in type.GetMembers())
        {
            ITypeSymbol? memberType = null;
            AttributeData? attr = null;

            if (member is IFieldSymbol field)
            {
                memberType = field.Type;
                attr = field
                    .GetAttributes()
                    .FirstOrDefault(a =>
                        SymbolEqualityComparer.Default.Equals(
                            a.AttributeClass,
                            symbols.SingletonAttribute
                        )
                        || SymbolEqualityComparer.Default.Equals(
                            a.AttributeClass,
                            symbols.TransientAttribute
                        )
                    );
            }
            else if (member is IPropertySymbol prop)
            {
                memberType = prop.Type;
                attr = prop.GetAttributes()
                    .FirstOrDefault(a =>
                        SymbolEqualityComparer.Default.Equals(
                            a.AttributeClass,
                            symbols.SingletonAttribute
                        )
                        || SymbolEqualityComparer.Default.Equals(
                            a.AttributeClass,
                            symbols.TransientAttribute
                        )
                    );
            }

            if (attr is null || memberType is null)
                continue;

            // 若特性有参数，则以参数为服务类型，否则以成员类型为服务类型
            if (attr.ConstructorArguments.Length > 0)
            {
                foreach (var arg in attr.ConstructorArguments)
                {
                    if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol serviceType)
                    {
                        list.Add(
                            new ProvidedServiceDescriptor(serviceType, memberType, member.Name)
                        );
                    }
                }
            }
            else
            {
                list.Add(new ProvidedServiceDescriptor(memberType, memberType, member.Name));
            }
        }

        return list;
    }

    // 5. Scope 的 Instantiate / Expect（Modules + AutoModules）
    public static IReadOnlyList<INamedTypeSymbol> ExtractInstantiate(
        this INamedTypeSymbol type,
        AttributeData? modulesAttr,
        AttributeData? autoModulesAttr
    )
    {
        // 这里只处理 Modules.Instantiate，AutoModules 的自动扫描逻辑可以在别处补充
        if (modulesAttr is null || modulesAttr.ConstructorArguments.Length == 0)
            return Array.Empty<INamedTypeSymbol>();

        var list = new List<INamedTypeSymbol>();
        foreach (var arg in modulesAttr.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Array)
            {
                foreach (var item in arg.Values)
                {
                    if (item.Kind == TypedConstantKind.Type && item.Value is INamedTypeSymbol t)
                        list.Add(t);
                }
            }
            else if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol t)
            {
                list.Add(t);
            }
        }
        return list;
    }

    public static IReadOnlyList<INamedTypeSymbol> ExtractExpect(
        this INamedTypeSymbol type,
        AttributeData? modulesAttr,
        AttributeData? autoModulesAttr
    )
    {
        // 同上，先只处理 Modules.Expect，AutoModules 的递归约束可以后续扩展
        if (modulesAttr is null)
            return Array.Empty<INamedTypeSymbol>();

        var namedArgs = modulesAttr.NamedArguments;
        var list = new List<INamedTypeSymbol>();

        foreach (var kv in namedArgs)
        {
            if (kv.Key != "Expect")
                continue;

            var tc = kv.Value;
            if (tc.Kind != TypedConstantKind.Array)
                continue;

            foreach (var item in tc.Values)
            {
                if (item.Kind == TypedConstantKind.Type && item.Value is INamedTypeSymbol t)
                    list.Add(t);
            }
        }

        return list;
    }
}
