using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Helpers;

internal static class ClassTypeInfoHelper
{
    // ============================================================
    // ServiceTypes（从类上收集服务接口）
    // ============================================================
    public static ImmutableArray<ITypeSymbol> CollectServiceTypes(
        INamedTypeSymbol type,
        CachedSymbols symbols,
        ServiceLifetime lifetime
    )
    {
        var attrSymbol =
            lifetime == ServiceLifetime.Singleton
                ? symbols.SingletonAttribute
                : symbols.TransientAttribute;

        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();

        // 获取 [Singleton(...)] 或 [Transient(...)]
        var attr = AttributeHelper.GetAttribute(type, attrSymbol);

        // 如果没有指定服务类型 → 默认暴露自身类型
        if (attr is null || attr.ConstructorArguments.Length == 0)
        {
            builder.Add(type);
            return builder.ToImmutable();
        }

        // 解析构造参数中的 typeof(...)
        foreach (var arg in attr.ConstructorArguments)
        {
            foreach (var v in arg.Values)
            {
                if (v.Value is ITypeSymbol t)
                    builder.Add(t);
            }
        }

        // 如果用户写了 [Singleton()] 但没有传类型 → 仍然默认自身类型
        if (builder.Count == 0)
            builder.Add(type);

        return builder.ToImmutable();
    }

    // ============================================================
    // InjectConstructor（唯一构造函数注入）
    // ============================================================
    public static InjectConstructorDescriptor? CollectInjectConstructor(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var ctors = type.InstanceConstructors.Where(c => !c.IsStatic).ToArray();
        if (ctors.Length == 0)
            return null;

        // 多个构造函数 → 必须唯一 InjectConstructor
        var injectCtors = ctors
            .Where(ctor => AttributeHelper.HasAttribute(ctor, symbols.InjectConstructorAttribute))
            .ToArray();

        IMethodSymbol ctorSymbol;

        if (ctors.Length == 1)
        {
            ctorSymbol = ctors[0];
        }
        else
        {
            if (injectCtors.Length != 1)
                return null;

            ctorSymbol = injectCtors[0];
        }

        var parameters = ctorSymbol
            .Parameters.Select(p => new InjectTypeDescriptor(p.Type, p.Name))
            .ToImmutableArray();

        return new InjectConstructorDescriptor(ctorSymbol, parameters);
    }

    // ============================================================
    // InjectedMembers（User / Host+User）
    // ============================================================
    public static ImmutableArray<InjectTypeDescriptor> CollectInjectedMembers(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var builder = ImmutableArray.CreateBuilder<InjectTypeDescriptor>();

        foreach (var member in type.GetMembers())
        {
            if (!AttributeHelper.HasAttribute(member, symbols.InjectAttribute))
                continue;

            ITypeSymbol? memberType = member switch
            {
                IPropertySymbol p => p.Type,
                IFieldSymbol f => f.Type,
                _ => null,
            };

            if (memberType is null)
                continue;

            builder.Add(new InjectTypeDescriptor(memberType, member.Name));
        }

        return builder.ToImmutable();
    }

    // ============================================================
    // ProvidedServices（Host / Host+User）
    // 支持多接口暴露： [Singleton(typeof(A), typeof(B))]
    // ============================================================
    public static ImmutableArray<ProvidedServiceDescriptor> CollectProvidedServices(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var builder = ImmutableArray.CreateBuilder<ProvidedServiceDescriptor>();

        foreach (var member in type.GetMembers())
        {
            var attr = AttributeHelper.GetAttribute(member, symbols.SingletonAttribute);
            if (attr is null)
                continue;

            ITypeSymbol? memberType = member switch
            {
                IPropertySymbol p => p.Type,
                IFieldSymbol f => f.Type,
                _ => null,
            };

            if (memberType is null)
                continue;

            // 解析 [Singleton(typeof(...), typeof(...))]
            var serviceTypes = attr
                .ConstructorArguments.SelectMany(arg => arg.Values)
                .Select(v => v.Value as ITypeSymbol)
                .Where(t => t is not null)
                .Cast<ITypeSymbol>()
                .ToImmutableArray();

            // 如果没有指定接口 → 默认暴露成员类型
            if (serviceTypes.Length == 0)
                serviceTypes = ImmutableArray.Create(memberType);

            builder.Add(
                new ProvidedServiceDescriptor(
                    ServiceTypes: serviceTypes,
                    MemberType: memberType,
                    MemberName: member.Name
                )
            );
        }

        return builder.ToImmutable();
    }

    // ============================================================
    // ScopeInstantiate（Modules / AutoModules）
    // ============================================================
    public static ImmutableArray<INamedTypeSymbol> CollectScopeInstantiate(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var modules = AttributeHelper.GetAttribute(type, symbols.ModulesAttribute);
        var auto = AttributeHelper.GetAttribute(type, symbols.AutoModulesAttribute);

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        if (modules is not null)
        {
            foreach (var arg in modules.ConstructorArguments)
            {
                foreach (var v in arg.Values)
                {
                    if (v.Value is INamedTypeSymbol t)
                        builder.Add(t);
                }
            }
        }

        // if (auto is not null)
        // {
        //     // AutoModules → 自动扫描同程序集所有类型
        //     var asm = type.ContainingAssembly;
        //     foreach (var t in asm.GetTypeMembers())
        //         builder.Add(t);
        // }

        return builder.ToImmutable();
    }

    // ============================================================
    // Scope AttributeInfo（Modules / AutoModules）
    // ============================================================
    public static ImmutableArray<INamedTypeSymbol> CollectScopeExpect(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var modules = AttributeHelper.GetAttribute(type, symbols.ModulesAttribute);
        var auto = AttributeHelper.GetAttribute(type, symbols.AutoModulesAttribute);

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        if (modules is not null)
        {
            foreach (var arg in modules.ConstructorArguments)
            {
                foreach (var v in arg.Values)
                {
                    if (v.Value is INamedTypeSymbol t)
                        builder.Add(t);
                }
            }
        }

        // if (auto is not null)
        // {
        //     // AutoModules → 自动扫描同程序集所有类型
        //     var asm = type.ContainingAssembly;
        //     foreach (var t in asm.GetTypeMembers())
        //         builder.Add(t);
        // }

        return builder.ToImmutable();
    }

    // ============================================================
    // Scope Services（Singleton / Transient / Singleton from Host）
    // ============================================================

    private static ImmutableArray<ScopeServiceDescriptor> BuildScopeServices(
        TypeInfo scopeInfo,
        IReadOnlyDictionary<INamedTypeSymbol, TypeInfo> types
    )
    {
        var builder = ImmutableArray.CreateBuilder<ScopeServiceDescriptor>();

        // 1. Modules.Instantiate → Scope 内部实例化的 Singleton
        foreach (var impl in scopeInfo.ScopeInstantiate)
        {
            if (!types.TryGetValue(impl, out var info))
                continue;

            builder.Add(
                new ScopeServiceDescriptor(
                    Implementation: impl,
                    ServiceTypes: info.ServiceTypes,
                    Lifetime: info.Lifetime, // 必须是 Singleton（诊断在别处做）
                    IsHostProvided: false
                )
            );
        }

        // 2. Modules.Expect → Host 提供的 Singleton
        foreach (var hostType in scopeInfo.ScopeExpect)
        {
            if (!types.TryGetValue(hostType, out var hostInfo))
                continue;

            foreach (var provided in hostInfo.ProvidedServices)
            {
                builder.Add(
                    new ScopeServiceDescriptor(
                        Implementation: hostType,
                        ServiceTypes: provided.ServiceTypes,
                        Lifetime: ServiceLifetime.Singleton,
                        IsHostProvided: true
                    )
                );
            }
        }

        // 3. 所有 Transient 服务
        foreach (var info in types.Values)
        {
            if (!info.IsService || info.Lifetime != ServiceLifetime.Transient)
                continue;

            builder.Add(
                new ScopeServiceDescriptor(
                    Implementation: info.Symbol,
                    ServiceTypes: info.ServiceTypes,
                    Lifetime: ServiceLifetime.Transient,
                    IsHostProvided: false
                )
            );
        }

        return builder.ToImmutable();
    }

    private static ImmutableHashSet<ITypeSymbol> BuildScopeSingletonTypes(
        ImmutableArray<ScopeServiceDescriptor> scopeServices
    )
    {
        var set = ImmutableHashSet.CreateBuilder<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var svc in scopeServices)
        {
            if (svc.Lifetime != ServiceLifetime.Singleton)
                continue;

            foreach (var st in svc.ServiceTypes)
                set.Add(st);
        }

        return set.ToImmutable();
    }

    private static ImmutableDictionary<ITypeSymbol, INamedTypeSymbol> BuildScopeTransientFactories(
        ImmutableArray<ScopeServiceDescriptor> scopeServices
    )
    {
        var dict = ImmutableDictionary.CreateBuilder<ITypeSymbol, INamedTypeSymbol>(
            SymbolEqualityComparer.Default
        );

        foreach (var svc in scopeServices)
        {
            if (svc.Lifetime != ServiceLifetime.Transient)
                continue;

            // key = 服务类型（PathFinder 本身），value = 实现类型
            foreach (var st in svc.ServiceTypes)
                dict[st] = svc.Implementation;
        }

        return dict.ToImmutable();
    }
}
