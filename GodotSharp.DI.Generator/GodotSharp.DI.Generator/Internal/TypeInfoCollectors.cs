using System;
using System.Collections.Generic;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class TypeInfoCollectors
{
    // ============================================================
    // 1. Service Types
    // ============================================================

    public static IReadOnlyList<ITypeSymbol> CollectServiceTypes(
        INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var attr =
            TypeHelper.GetAttribute(type, symbols.SingletonAttribute)
            ?? TypeHelper.GetAttribute(type, symbols.TransientAttribute);

        if (attr is null || attr.ConstructorArguments.Length == 0)
            return new[] { type };

        var list = new List<ITypeSymbol>();
        foreach (var arg in attr.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol t)
                list.Add(t);
        }
        return list;
    }

    // ============================================================
    // 2. Constructor Injection
    // ============================================================
    // ⭐ 支持 private constructor + [InjectConstructor]
    // ⭐ 若多个 ctor 且无标记 → 返回 null（交给 Analyzer 报错）

    public static InjectConstructorDescriptor? CollectInjectConstructor(
        INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var ctors = type.InstanceConstructors.Where(c => !c.IsStatic).ToArray();

        if (ctors.Length == 0)
            return null;

        // 优先找 [InjectConstructor]（允许 private）
        var injectCtor = ctors.FirstOrDefault(ctor =>
            TypeHelper.HasAttribute(ctor, symbols.InjectConstructorAttribute)
        );

        if (injectCtor is null)
        {
            // 若只有一个 ctor → 自动使用（允许 private）
            if (ctors.Length == 1)
                injectCtor = ctors[0];
            else
                return null; // 多 ctor 无标记 → 交给 Analyzer 报 GDI002
        }

        var paramTypes = injectCtor.Parameters.Select(p => p.Type).ToArray();
        return new InjectConstructorDescriptor(injectCtor, paramTypes);
    }

    // ============================================================
    // 3. Injected Members
    // ============================================================

    public static IReadOnlyList<InjectedMemberDescriptor> CollectInjectedMembers(
        INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var list = new List<InjectedMemberDescriptor>();

        foreach (var member in type.GetMembers())
        {
            if (!TypeHelper.HasAttribute(member, symbols.InjectAttribute))
                continue;

            if (member is IFieldSymbol f)
                list.Add(new InjectedMemberDescriptor(f.Name, f.Type));
            else if (member is IPropertySymbol p)
                list.Add(new InjectedMemberDescriptor(p.Name, p.Type));
        }

        return list;
    }

    // ============================================================
    // 4. Host Provided Services
    // ============================================================

    public static IReadOnlyList<ProvidedServiceDescriptor> CollectProvidedServices(
        INamedTypeSymbol type,
        SymbolCache symbols
    )
    {
        var list = new List<ProvidedServiceDescriptor>();

        foreach (var member in type.GetMembers())
        {
            var attr = GetServiceAttribute(member, symbols);
            if (attr is null)
                continue;

            var memberType = member switch
            {
                IFieldSymbol f => f.Type,
                IPropertySymbol p => p.Type,
                _ => null,
            };

            if (memberType is null)
                continue;

            var serviceTypes = ParseServiceTypes(attr, memberType);
            foreach (var st in serviceTypes)
                list.Add(new ProvidedServiceDescriptor(st, memberType, member.Name));
        }

        return list;
    }

    private static AttributeData? GetServiceAttribute(ISymbol member, SymbolCache symbols)
    {
        return member
            .GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.SingletonAttribute)
                || SymbolEqualityComparer.Default.Equals(
                    a.AttributeClass,
                    symbols.TransientAttribute
                )
            );
    }

    private static IReadOnlyList<ITypeSymbol> ParseServiceTypes(
        AttributeData attr,
        ITypeSymbol fallback
    )
    {
        var list = new List<ITypeSymbol>();

        if (attr.ConstructorArguments.Length == 0)
        {
            list.Add(fallback);
            return list;
        }

        foreach (var arg in attr.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol t)
                list.Add(t);
        }

        return list.Count > 0 ? list : new[] { fallback };
    }

    // ============================================================
    // 5. Scope Instantiate / Expect
    // ============================================================

    public static IReadOnlyList<INamedTypeSymbol> CollectInstantiate(
        INamedTypeSymbol type,
        AttributeData? modulesAttr,
        AttributeData? autoModulesAttr
    )
    {
        if (modulesAttr is null || modulesAttr.ConstructorArguments.Length == 0)
            return Array.Empty<INamedTypeSymbol>();

        var list = new List<INamedTypeSymbol>();

        foreach (var arg in modulesAttr.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Array)
            {
                foreach (var item in arg.Values)
                {
                    if (item.Value is INamedTypeSymbol t)
                        list.Add(t);
                }
            }
            else if (arg.Value is INamedTypeSymbol t)
            {
                list.Add(t);
            }
        }

        return list;
    }

    public static IReadOnlyList<INamedTypeSymbol> CollectExpect(
        INamedTypeSymbol type,
        AttributeData? modulesAttr,
        AttributeData? autoModulesAttr
    )
    {
        if (modulesAttr is null)
            return Array.Empty<INamedTypeSymbol>();

        var list = new List<INamedTypeSymbol>();

        foreach (var kv in modulesAttr.NamedArguments)
        {
            if (kv.Key != "Expect")
                continue;

            foreach (var item in kv.Value.Values)
            {
                if (item.Value is INamedTypeSymbol t)
                    list.Add(t);
            }
        }

        return list;
    }
}
