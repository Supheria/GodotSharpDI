using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Extensions;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal static class ClassTypeValidator
{
    public static ClassTypeValidateResult ValidateType(ClassType type, CachedSymbols symbols)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // 1. 角色冲突检查
        var roles = DetectRoles(type, symbols);
        var roleConflicts = ValidateRoleConflicts(roles, type, symbols);
        diagnostics.AddRange(roleConflicts);

        // 2. 构造函数验证（遍历所有构造函数）
        var ctorDiagnostics = ValidateConstructors(roles, type, symbols);
        diagnostics.AddRange(ctorDiagnostics);

        // 3. 成员验证（遍历所有成员）
        var memberDiagnostics = ValidateMembers(roles, type, symbols);
        diagnostics.AddRange(memberDiagnostics);

        // 4. Scope 验证
        var scopeDiagnostics = ValidateScopeRequirements(roles, type, symbols);
        diagnostics.AddRange(scopeDiagnostics);

        return new ClassTypeValidateResult(roles, diagnostics.ToImmutable());
    }

    private static ClassTypeRoles DetectRoles(ClassType type, CachedSymbols symbols)
    {
        return new ClassTypeRoles(
            IsSingleton: type.HasAttribute(symbols.SingletonAttribute),
            IsTransient: type.HasAttribute(symbols.TransientAttribute),
            IsHost: type.HasAttribute(symbols.HostAttribute),
            IsUser: type.HasAttribute(symbols.UserAttribute),
            IsScope: type.ImplementsInterface(symbols.ScopeInterface),
            IsNode: type.Inherits(symbols.GodotNodeType),
            IsServicesReady: type.ImplementsInterface(symbols.ServicesReadyInterface)
        );
    }

    private static ImmutableArray<Diagnostic> ValidateRoleConflicts(
        ClassTypeRoles roles,
        ClassType type,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = type.DeclarationSyntax.Identifier.GetLocation();

        if (roles.IsSingleton && roles.IsTransient) // Singleton 与 Transient 互斥
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.ServiceLifetimeConflict,
                location: location,
                additionalLocations:
                [
                    type.GetAttributeLocation(symbols.SingletonAttribute),
                    type.GetAttributeLocation(symbols.TransientAttribute),
                ],
                type.Name
            );
            diagnostics.Add(diagnostic);
        }
        if (roles.IsSingleton && roles.IsNode) // Singleton 不能是 Node
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.ServiceCannotBeNode,
                location: location,
                additionalLocations: [type.GetAttributeLocation(symbols.SingletonAttribute)],
                "Singleton",
                type.Name
            );
            diagnostics.Add(diagnostic);
        }
        if (roles.IsTransient && roles.IsNode) // Transient 不能是 Node
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.ServiceCannotBeNode,
                location: location,
                additionalLocations: [type.GetAttributeLocation(symbols.TransientAttribute)],
                "Transient",
                type.Name
            );
            diagnostics.Add(diagnostic);
        }
        if (roles.IsScope)
        {
            if (!roles.IsNode) // Scope 必须是 Node
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.ScopeMustBeNode,
                    location: location,
                    type.Name
                );
                diagnostics.Add(diagnostic);
            }
            if (roles.IsSingleton) // Scope 不能标记为 [Singleton]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.SingletonAttribute)],
                    "Scope",
                    type.Name,
                    "Singleton"
                );
                diagnostics.Add(diagnostic);
            }
            if (roles.IsTransient) // Scope 不能标记为 [Transient]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.TransientAttribute)],
                    "Scope",
                    type.Name,
                    "IsTransient"
                );
                diagnostics.Add(diagnostic);
            }
            if (roles.IsHost) // Scope 不能标记为 [Host]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.HostAttribute)],
                    "Scope",
                    type.Name,
                    "Host"
                );
                diagnostics.Add(diagnostic);
            }
            if (roles.IsUser) // Scope 不能标记为 [User]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.UserAttribute)],
                    "Scope",
                    type.Name,
                    "User"
                );
                diagnostics.Add(diagnostic);
            }
        }
        if (roles.IsHost)
        {
            if (roles.IsSingleton) // Host 不能标记为 [Singleton]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.SingletonAttribute)],
                    "Host",
                    type.Name,
                    "Singleton"
                );
                diagnostics.Add(diagnostic);
            }
            if (roles.IsTransient) // Host 不能标记为 [Transient]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.TransientAttribute)],
                    "Host",
                    type.Name,
                    "IsTransient"
                );
                diagnostics.Add(diagnostic);
            }
        }
        if (roles.IsUser)
        {
            if (roles.IsSingleton) // User 不能标记为 [Singleton]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.SingletonAttribute)],
                    "User",
                    type.Name,
                    "Singleton"
                );
                diagnostics.Add(diagnostic);
            }
            if (roles.IsTransient) // User 不能标记为 [Transient]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidAttribute,
                    location: location,
                    additionalLocations: [type.GetAttributeLocation(symbols.TransientAttribute)],
                    "User",
                    type.Name,
                    "IsTransient"
                );
                diagnostics.Add(diagnostic);
            }
        }
        if (roles.IsServicesReady && !roles.IsUser) // 实现 IServiceReady 只有标记为 [User] 才有意义
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.ServiceReadyNeedUser,
                location: location,
                type.Name
            );
            diagnostics.Add(diagnostic);
        }

        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> ValidateConstructors(
        ClassTypeRoles roles,
        ClassType type,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = type.DeclarationSyntax.Identifier.GetLocation();
        var constructors = type.Symbol.InstanceConstructors.Where(c => !c.IsStatic).ToArray();

        if (roles.IsService && constructors.Length == 0) // Service 必须至少有一个非静态构造函数
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.NoPublicConstructor,
                location: location,
                type.Name
            );
            diagnostics.Add(diagnostic);
        }

        var injectConstructors = constructors
            .Where(c => AttributeHelper.HasAttribute(c, symbols.InjectConstructorAttribute))
            .ToArray();
        if (!roles.IsService)
        {
            if (injectConstructors.Length > 0) // 只有 Service 才能标记 [InjectConstructor]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidInjectConstructorAttribute,
                    location: location,
                    type.Name
                );
                diagnostics.Add(diagnostic);
            }
        }
        else if (constructors.Length > 1 && injectConstructors.Length != 1) // 有多个构造函数时，没有标记或重复标记 [InjectConstructor]
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.AmbiguousConstructor,
                location: location,
                type.Name
            );
            diagnostics.Add(diagnostic);
            foreach (var ctor in constructors)
            {
                var syntax = ctor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (syntax is ConstructorDeclarationSyntax ctorSyntax)
                {
                    var dia = Diagnostic.Create(
                        descriptor: DiagnosticDescriptors.AmbiguousConstructor,
                        location: ctorSyntax.Identifier.GetLocation(),
                        type.Name
                    );
                    diagnostics.Add(dia);
                }
            }
        }
        // 构造函数参数验证放在图级验证中
        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> ValidateMembers(
        ClassTypeRoles roles,
        ClassType type,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = type.DeclarationSyntax.Identifier.GetLocation();

        foreach (var member in type.Symbol.GetMembers())
        {
            // 只验证字段和属性
            if (member is not IFieldSymbol && member is not IPropertySymbol)
                continue;

            var hasSingleton = AttributeHelper.HasAttribute(member, symbols.SingletonAttribute);
            var hasInject = AttributeHelper.HasAttribute(member, symbols.InjectAttribute);

            if (!roles.IsHost && hasSingleton) // 只有 Host 的成员能标记为 [Singleton]
            {
                var attr = AttributeHelper.GetAttribute(member, symbols.SingletonAttribute);
                var loc = AttributeHelper.GetAttributeLocation(attr, location);
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidMemberAttribute,
                    location: loc,
                    type.Name,
                    "Host",
                    "Singleton"
                );
                diagnostics.Add(diagnostic);
            }
            if (!roles.IsUser && hasInject) // 只有 User 的成员能标记为 [Inject]
            {
                var memberLocation = SymbolHelper.GetLocation(member, location);
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidMemberAttribute,
                    location: memberLocation,
                    type.Name,
                    "User",
                    "Inject"
                );
                diagnostics.Add(diagnostic);
            }
            if (hasSingleton && hasInject) // [Singleton] 与 [Inject] 互斥
            {
                var memberLocation = SymbolHelper.GetLocation(member, location);
                var singleton = AttributeHelper.GetAttribute(member, symbols.SingletonAttribute);
                var inject = AttributeHelper.GetAttribute(member, symbols.InjectAttribute);
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.MemberAttributeConflict,
                    location: memberLocation,
                    additionalLocations:
                    [
                        AttributeHelper.GetAttributeLocation(singleton, memberLocation),
                        AttributeHelper.GetAttributeLocation(inject, memberLocation),
                    ],
                    "Singleton",
                    "Inject"
                );
                diagnostics.Add(diagnostic);
            }
            if (hasInject) // [Inject] 成员不能是只读的
            {
                var isAssignable = member switch
                {
                    IFieldSymbol field => !field.IsReadOnly,
                    IPropertySymbol property => property.SetMethod is not null,
                    _ => false,
                };
                if (!isAssignable)
                {
                    var memberLocation = SymbolHelper.GetLocation(member, location);
                    var diagnostic = Diagnostic.Create(
                        descriptor: DiagnosticDescriptors.InjectMemberNotAssignable,
                        location: memberLocation
                    );
                    diagnostics.Add(diagnostic);
                }
            }
            if (hasSingleton) // [Singleton] 成员如果是属性必须要有 getter
            {
                if (member is IPropertySymbol property && property.GetMethod is null)
                {
                    var memberLocation = SymbolHelper.GetLocation(member, location);
                    var diagnostic = Diagnostic.Create(
                        descriptor: DiagnosticDescriptors.SingletonPropertyNotAccessible,
                        location: memberLocation
                    );
                    diagnostics.Add(diagnostic);
                }
            }
            // 成员类型验证放在图级验证中
        }

        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<Diagnostic> ValidateScopeRequirements(
        ClassTypeRoles roles,
        ClassType type,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = type.DeclarationSyntax.Identifier.GetLocation();

        var hasModules = type.HasAttribute(symbols.ModulesAttribute);
        var hasAutoModules = type.HasAttribute(symbols.AutoModulesAttribute);

        if (!roles.IsScope)
        {
            if (hasModules || hasAutoModules) // 只有 Scope 才能标记 [Modules] 或 [AutoModules]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.InvalidModuleAttribute,
                    location: location,
                    additionalLocations:
                    [
                        type.GetAttributeLocation(symbols.ModulesAttribute),
                        type.GetAttributeLocation(symbols.AutoModulesAttribute),
                    ],
                    type.Name
                );
                diagnostics.Add(diagnostic);
            }
        }
        else
        {
            if (hasModules && hasAutoModules) // [Modules] 与 [AutoModules] 互斥
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.ScopeModulesConflict,
                    location: location,
                    additionalLocations:
                    [
                        type.GetAttributeLocation(symbols.ModulesAttribute),
                        type.GetAttributeLocation(symbols.AutoModulesAttribute),
                    ],
                    type.Name
                );
                diagnostics.Add(diagnostic);
            }
            if (!hasModules && !hasAutoModules) // 必须有 [Modules] 或 [AutoModules]
            {
                var diagnostic = Diagnostic.Create(
                    descriptor: DiagnosticDescriptors.ScopeMissingModules,
                    location: location,
                    type.Name
                );
                diagnostics.Add(diagnostic);
            }
        }
        // Modules Singleton 和 Expect 验证放在图级验证中
        // AutoModules 命名空间分析、依赖分析、职责范围分析、依赖冲突分析放在图级验证中

        return diagnostics.ToImmutable();
    }
}
