using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Extensions;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal static class ClassTypeInfoFactory
{
    private static ImmutableArray<ITypeSymbol> CollectServiceExposedTypes(
        INamedTypeSymbol type,
        AttributeData attribute
    )
    {
        var exposedTypes = ImmutableArray.CreateBuilder<ITypeSymbol>();
        foreach (var arg in attribute.ConstructorArguments)
        {
            foreach (var v in arg.Values)
            {
                if (v.Value is ITypeSymbol t && t.TypeKind != TypeKind.Error)
                {
                    exposedTypes.Add(t);
                }
            }
        }
        if (exposedTypes.Count == 0) // 如果没有指定服务类型，默认暴露自身类型
        {
            exposedTypes.Add(type);
        }
        return exposedTypes.ToImmutable();
    }

    private static InjectConstructorDescriptor CollectServiceInjectConstructor(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var constructors = type.InstanceConstructors.Where(c => !c.IsStatic).ToArray();
        var injectConstructors = constructors
            .Where(ctor => AttributeHelper.HasAttribute(ctor, symbols.InjectConstructorAttribute))
            .ToArray();
        IMethodSymbol? targetCtor = null;
        if (constructors.Length == 1)
        {
            targetCtor = constructors[0];
        }
        else if (injectConstructors.Length == 1)
        {
            targetCtor = injectConstructors[0];
        }
        var parameters = ImmutableArray.CreateBuilder<InjectTypeDescriptor>();
        if (targetCtor is not null)
        {
            foreach (var parameter in targetCtor.Parameters)
            {
                var descriptor = new InjectTypeDescriptor(parameter.Type, parameter.Name);
                parameters.Add(descriptor);
            }
        }
        return new InjectConstructorDescriptor(parameters.ToImmutable());
    }

    private static ImmutableArray<InjectTypeDescriptor> CollectUserInjectMembers(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var injectMembers = ImmutableArray.CreateBuilder<InjectTypeDescriptor>();
        foreach (var member in type.GetMembers())
        {
            if (!AttributeHelper.HasAttribute(member, symbols.InjectAttribute))
            {
                continue;
            }
            switch (member)
            {
                case IPropertySymbol p:
                    injectMembers.Add(new InjectTypeDescriptor(p.Type, p.Name));
                    break;
                case IFieldSymbol f:
                    injectMembers.Add(new InjectTypeDescriptor(f.Type, f.Name));
                    break;
            }
        }
        return injectMembers.ToImmutable();
    }

    private static ImmutableArray<ProvidedServiceDescriptor> CollectHostSingletonServices(
        INamedTypeSymbol type,
        CachedSymbols symbols
    )
    {
        var builder = ImmutableArray.CreateBuilder<ProvidedServiceDescriptor>();
        foreach (var member in type.GetMembers())
        {
            var attribute = AttributeHelper.GetAttribute(member, symbols.SingletonAttribute);
            if (attribute is null)
            {
                continue;
            }
            var exposedTypes = ImmutableArray.CreateBuilder<ITypeSymbol>();
            foreach (var arg in attribute.ConstructorArguments)
            {
                foreach (var v in arg.Values)
                {
                    if (v.Value is ITypeSymbol t)
                    {
                        exposedTypes.Add(t);
                    }
                }
            }
            if (exposedTypes.Count == 0) // 如果没有指定服务类型，默认暴露成员类型
            {
                switch (member)
                {
                    case IPropertySymbol p:
                        exposedTypes.Add(p.Type);
                        break;
                    case IFieldSymbol f:
                        exposedTypes.Add(f.Type);
                        break;
                }
            }
            builder.Add(new ProvidedServiceDescriptor(exposedTypes.ToImmutable(), member.Name));
        }
        return builder.ToImmutable();
    }

    private static ClassTypeInfo CreateService(
        ClassType type,
        ClassTypeRoles roles,
        CachedSymbols symbols
    )
    {
        var lifetime = roles.IsSingleton ? ServiceLifetime.Singleton : ServiceLifetime.Transient;
        var attribute = roles.IsSingleton
            ? type.GetAttribute(symbols.SingletonAttribute)
            : type.GetAttribute(symbols.TransientAttribute);
        var exposedTypes = CollectServiceExposedTypes(type.Symbol, attribute!);
        var ctor = CollectServiceInjectConstructor(type.Symbol, symbols);

        return new ClassTypeInfo(
            Symbol: type.Symbol,
            DeclarationSyntax: type.DeclarationSyntax,
            IsSingleton: roles.IsSingleton,
            IsTransient: roles.IsTransient,
            Lifetime: lifetime,
            ServiceExposedTypes: exposedTypes,
            ServiceConstructor: ctor
        );
    }

    private static ClassTypeInfo CreateHostOrUser(
        ClassType type,
        ClassTypeRoles roles,
        CachedSymbols symbols
    )
    {
        var providedServices = roles.IsHost
            ? CollectHostSingletonServices(type.Symbol, symbols)
            : ImmutableArray<ProvidedServiceDescriptor>.Empty;
        var injectedMembers = roles.IsUser
            ? CollectUserInjectMembers(type.Symbol, symbols)
            : ImmutableArray<InjectTypeDescriptor>.Empty;

        return new ClassTypeInfo(
            Symbol: type.Symbol,
            DeclarationSyntax: type.DeclarationSyntax,
            IsHost: roles.IsHost,
            IsUser: roles.IsUser,
            IsNode: roles.IsNode,
            IsServicesReady: roles.IsServicesReady,
            UserInjectMembers: injectedMembers,
            HostSingletonServices: providedServices
        );
    }

    private static ClassTypeInfo CreateScope(
        ClassType type,
        ClassTypeRoles roles,
        CachedSymbols symbols
    )
    {
        var modules = type.GetAttribute(symbols.ModulesAttribute);
        var autoModules = type.GetAttribute(symbols.AutoModulesAttribute);

        return new ClassTypeInfo(
            Symbol: type.Symbol,
            DeclarationSyntax: type.DeclarationSyntax,
            IsScope: roles.IsScope,
            Modules: modules,
            AutoModules: autoModules
        );
    }

    public static ClassTypeInfoBuildResult Create(ClassType type, CachedSymbols symbols)
    {
        var validateResult = ClassTypeValidator.ValidateType(type, symbols);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        diagnostics.AddRange(validateResult.Diagnostics);
        if (validateResult.HasErrors)
        {
            return ClassTypeInfoBuildResult.Failure(diagnostics.ToImmutable());
        }
        var roles = validateResult.Roles;
        if (roles.IsService)
        {
            var typeInfo = CreateService(type, roles, symbols);
            return new ClassTypeInfoBuildResult(typeInfo, diagnostics.ToImmutable());
        }
        if (roles.IsHost || roles.IsUser)
        {
            var typeInfo = CreateHostOrUser(type, roles, symbols);
            return new ClassTypeInfoBuildResult(typeInfo, diagnostics.ToImmutable());
        }
        if (roles.IsScope)
        {
            var typeInfo = CreateScope(type, roles, symbols);
            return new ClassTypeInfoBuildResult(typeInfo, diagnostics.ToImmutable());
        }
        var location = type.DeclarationSyntax.Identifier.GetLocation();
        var diagnostic = Diagnostic.Create(
            descriptor: DiagnosticDescriptors.UnknownTypeRole,
            location: location,
            type.Name
        );
        diagnostics.Add(diagnostic);
        return ClassTypeInfoBuildResult.Failure(diagnostics.ToImmutable());
    }
}
