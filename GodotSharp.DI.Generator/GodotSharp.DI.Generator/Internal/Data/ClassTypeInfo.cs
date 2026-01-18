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
    bool IsSingleton = false,
    bool IsTransient = false,
    bool IsHost = false,
    bool IsUser = false,
    bool IsScope = false,
    bool IsNode = false,
    bool IsServicesReady = false,
    // -------------------------
    // Service（仅当 IsService = true）
    // -------------------------
    ServiceLifetime ServiceLifetime = ServiceLifetime.Singleton,
    ImmutableArray<ITypeSymbol> ServiceExposedTypes = default,
    InjectConstructorDescriptor? ServiceConstructor = null,
    // -------------------------
    // Host（成员提供服务）
    // -------------------------
    ImmutableArray<ProvidedServiceDescriptor> HostSingletonServices = default,
    // -------------------------
    // User（成员注入）
    // -------------------------
    ImmutableArray<InjectTypeDescriptor> UserInjectMembers = default,
    // -------------------------
    // Scope
    // -------------------------
    AttributeData? Modules = null,
    AttributeData? AutoModules = null
)
{
    public string Namespace { get; } = Symbol.ContainingNamespace.ToDisplayString();
    public bool IsService => IsSingleton || IsTransient;
}
