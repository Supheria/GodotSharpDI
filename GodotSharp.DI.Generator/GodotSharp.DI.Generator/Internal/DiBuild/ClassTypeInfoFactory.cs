using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal static class ClassTypeInfoFactory
{
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

    private static ClassTypeInfo CreateService(
        ClassType type,
        ClassTypeRoles roles,
        CachedSymbols symbols
    )
    {
        var lifetime = roles.IsSingleton ? ServiceLifetime.Singleton : ServiceLifetime.Transient;
        var serviceTypes = ClassTypeInfoHelper.CollectServiceTypes(type.Symbol, symbols, lifetime);
        var ctor = ClassTypeInfoHelper.CollectInjectConstructor(type.Symbol, symbols);

        return new ClassTypeInfo(
            Symbol: type.Symbol,
            DeclarationSyntax: type.DeclarationSyntax,
            Roles: roles,
            Lifetime: lifetime,
            ServiceTypes: serviceTypes,
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
            ? ClassTypeInfoHelper.CollectProvidedServices(type.Symbol, symbols)
            : ImmutableArray<ProvidedServiceDescriptor>.Empty;
        var injectedMembers = roles.IsUser
            ? ClassTypeInfoHelper.CollectInjectedMembers(type.Symbol, symbols)
            : ImmutableArray<InjectTypeDescriptor>.Empty;

        return new ClassTypeInfo(
            Symbol: type.Symbol,
            DeclarationSyntax: type.DeclarationSyntax,
            Roles: roles,
            InjectedMembers: injectedMembers,
            ProvidedServices: providedServices
        );
    }

    private static ClassTypeInfo CreateScope(
        ClassType type,
        ClassTypeRoles roles,
        CachedSymbols symbols
    )
    {
        var instantiate = ClassTypeInfoHelper.CollectScopeInstantiate(type.Symbol, symbols);
        var expect = ClassTypeInfoHelper.CollectScopeExpect(type.Symbol, symbols);

        return new ClassTypeInfo(
            Symbol: type.Symbol,
            DeclarationSyntax: type.DeclarationSyntax,
            Roles: roles,
            ScopeInstantiate: instantiate,
            ScopeExpect: expect
        // ScopeServices / SingletonTypes / TransientFactories
        // 在后续的 ServiceGraphBuilder 中用 with {} 填充
        );
    }
}
