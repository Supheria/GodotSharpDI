using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharp.DI.Generator.Internal.Data;

internal sealed record ClassTypeInfo(
    INamedTypeSymbol Symbol,
    ClassDeclarationSyntax DeclarationSyntax,
    // -------------------------
    // 角色标记
    // -------------------------
    ClassTypeRoles Roles,
    // -------------------------
    // Service（仅当 IsService = true）
    // -------------------------
    ServiceLifetime Lifetime = ServiceLifetime.Singleton,
    ImmutableArray<ITypeSymbol> ServiceTypes = default,
    InjectConstructorDescriptor? ServiceConstructor = null,
    // -------------------------
    // User / Host+User（成员注入）
    // -------------------------
    ImmutableArray<InjectTypeDescriptor> InjectedMembers = default,
    // -------------------------
    // Host（成员提供服务）
    // -------------------------
    ImmutableArray<ProvidedServiceDescriptor> ProvidedServices = default,
    // -------------------------
    // Scope（Modules.Instantiate / Modules.Expect）
    // -------------------------
    ImmutableArray<INamedTypeSymbol> ScopeInstantiate = default,
    ImmutableArray<INamedTypeSymbol> ScopeExpect = default,
    // -------------------------
    // Scope（GraphBuilder 生成的最终结构）
    // -------------------------
    ImmutableArray<ScopeServiceDescriptor> ScopeServices = default,
    ImmutableHashSet<ITypeSymbol>? ScopeSingletonTypes = null,
    ImmutableDictionary<ITypeSymbol, INamedTypeSymbol>? ScopeTransientFactories = null
)
{
    public string Namespace { get; } = Symbol.ContainingNamespace.ToDisplayString();
}
