using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Semantic;

internal static class ClassPipeline
{
    public static ClassValidationResult ValidateAndClassify(
        RawClassSemanticInfo raw,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // 1. 验证 partial
        if (!raw.IsPartial)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.DiClassMustBePartial,
                    raw.Location,
                    raw.Symbol.Name
                )
            );
            return new ClassValidationResult(null, diagnostics.ToImmutable());
        }

        // 2. 确定角色和生命周期
        var (role, lifetime, roleDiags) = DetermineRoleAndLifetime(raw, symbols);
        diagnostics.AddRange(roleDiags);

        if (role == TypeRole.None)
            return new ClassValidationResult(null, diagnostics.ToImmutable());

        // 3. 验证角色约束
        var roleValidation = ValidateRoleConstraints(raw, role, symbols);
        diagnostics.AddRange(roleValidation);

        // 4. 处理成员
        var (members, memberDiags) = ProcessMembers(raw, role, symbols);
        diagnostics.AddRange(memberDiags);

        // 5. 处理构造函数
        var (constructor, ctorDiags) = ProcessConstructor(raw, role, lifetime, symbols);
        diagnostics.AddRange(ctorDiags);

        // 6. 处理 Modules
        var (modulesInfo, modulesDiags) = ProcessModules(raw, symbols);
        diagnostics.AddRange(modulesDiags);

        var typeInfo = new TypeInfo(
            Symbol: raw.Symbol,
            Location: raw.Location,
            Role: role,
            Lifetime: lifetime,
            ImplementsIServicesReady: raw.ImplementsIServicesReady,
            IsNode: raw.IsNode,
            Members: members,
            Constructor: constructor,
            ModulesInfo: modulesInfo
        );

        return new ClassValidationResult(typeInfo, diagnostics.ToImmutable());
    }

    private static (
        TypeRole Role,
        ServiceLifetime Lifetime,
        ImmutableArray<Diagnostic> Diagnostics
    ) DetermineRoleAndLifetime(RawClassSemanticInfo raw, CachedSymbols symbols)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var role = TypeRole.None;
        var lifetime = ServiceLifetime.None;

        // 检查生命周期冲突
        if (raw.HasSingletonAttribute && raw.HasTransientAttribute)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.ServiceLifetimeConflict,
                    raw.Location,
                    raw.Symbol.Name
                )
            );
            return (TypeRole.None, ServiceLifetime.None, diagnostics.ToImmutable());
        }

        // Scope
        if (raw.ImplementsIScope)
        {
            role = TypeRole.Scope;

            if (
                raw.HasSingletonAttribute
                || raw.HasTransientAttribute
                || raw.HasHostAttribute
                || raw.HasUserAttribute
            )
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.ScopeInvalidAttribute,
                        raw.Location,
                        raw.Symbol.Name
                    )
                );
            }

            return (role, lifetime, diagnostics.ToImmutable());
        }

        // Service
        if (raw.HasSingletonAttribute || raw.HasTransientAttribute)
        {
            role = TypeRole.Service;
            lifetime = raw.HasSingletonAttribute
                ? ServiceLifetime.Singleton
                : ServiceLifetime.Transient;

            if (raw.HasHostAttribute || raw.HasUserAttribute)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.HostInvalidAttribute,
                        raw.Location,
                        raw.Symbol.Name
                    )
                );
            }

            return (role, lifetime, diagnostics.ToImmutable());
        }

        // Host + User
        if (raw.HasHostAttribute && raw.HasUserAttribute)
        {
            role = TypeRole.HostAndUser;
            return (role, lifetime, diagnostics.ToImmutable());
        }

        // Host only
        if (raw.HasHostAttribute)
        {
            role = TypeRole.Host;
            return (role, lifetime, diagnostics.ToImmutable());
        }

        // User only
        if (raw.HasUserAttribute)
        {
            role = TypeRole.User;
            return (role, lifetime, diagnostics.ToImmutable());
        }

        return (TypeRole.None, ServiceLifetime.None, diagnostics.ToImmutable());
    }

    private static ImmutableArray<Diagnostic> ValidateRoleConstraints(
        RawClassSemanticInfo raw,
        TypeRole role,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        switch (role)
        {
            case TypeRole.Service:
                if (raw.IsNode)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ServiceCannotBeNode,
                            raw.Location,
                            raw.Symbol.Name
                        )
                    );
                }
                if (!raw.Symbol.IsValidServiceType(symbols))
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ServiceTypeIsInvalid,
                            raw.Location,
                            raw.Symbol.Name
                        )
                    );
                }
                break;

            case TypeRole.Host:
            case TypeRole.HostAndUser:
                if (!raw.IsNode)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.HostMustBeNode,
                            raw.Location,
                            raw.Symbol.Name
                        )
                    );
                }
                break;

            case TypeRole.Scope:
                if (!raw.IsNode)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ScopeMustBeNode,
                            raw.Location,
                            raw.Symbol.Name
                        )
                    );
                }
                if (!raw.HasModulesAttribute && !raw.HasAutoModulesAttribute)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ScopeMissingModules,
                            raw.Location,
                            raw.Symbol.Name
                        )
                    );
                }
                break;
        }

        // 验证 IServicesReady
        if (raw.ImplementsIServicesReady && role != TypeRole.User && role != TypeRole.HostAndUser)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.ServiceReadyNeedUser,
                    raw.Location,
                    raw.Symbol.Name
                )
            );
        }

        return diagnostics.ToImmutable();
    }

    private static (ImmutableArray<MemberInfo>, ImmutableArray<Diagnostic>) ProcessMembers(
        RawClassSemanticInfo raw,
        TypeRole role,
        CachedSymbols symbols
    )
    {
        var members = ImmutableArray.CreateBuilder<MemberInfo>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var member in raw.Members)
        {
            var hasInject = HasAttribute(member, symbols.InjectAttribute);
            var hasSingleton = HasAttribute(member, symbols.SingletonAttribute);

            if (!hasInject && !hasSingleton)
                continue;

            if (hasInject && hasSingleton)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.MemberConflictWithSingletonAndInject,
                        member.Locations.FirstOrDefault() ?? raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasInject && role != TypeRole.User && role != TypeRole.HostAndUser)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.MemberHasInjectButNotInUser,
                        member.Locations.FirstOrDefault() ?? raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasSingleton && role != TypeRole.Host && role != TypeRole.HostAndUser)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.MemberHasSingletonButNotInHost,
                        member.Locations.FirstOrDefault() ?? raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            var (memberInfo, memberDiags) = ProcessSingleMember(
                member,
                hasInject,
                hasSingleton,
                symbols
            );
            if (memberInfo != null)
                members.Add(memberInfo);
            diagnostics.AddRange(memberDiags);
        }

        return (members.ToImmutable(), diagnostics.ToImmutable());
    }

    private static (MemberInfo?, ImmutableArray<Diagnostic>) ProcessSingleMember(
        ISymbol member,
        bool hasInject,
        bool hasSingleton,
        CachedSymbols symbols
    )
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var location = member.Locations.FirstOrDefault() ?? Location.None;

        // 检查 static 成员
        if (member.IsStatic)
        {
            if (hasInject)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InjectMemberIsStatic,
                        location,
                        member.Name
                    )
                );
                return (null, diagnostics.ToImmutable());
            }
            if (hasSingleton)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.SingletonMemberIsStatic,
                        location,
                        member.Name
                    )
                );
                return (null, diagnostics.ToImmutable());
            }
        }

        ITypeSymbol? memberType = null;
        MemberKind kind = MemberKind.None;

        if (member is IFieldSymbol field)
        {
            memberType = field.Type;
            kind = hasInject ? MemberKind.InjectField : MemberKind.SingletonField;
        }
        else if (member is IPropertySymbol property)
        {
            memberType = property.Type;

            if (hasInject)
            {
                if (property.SetMethod == null)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.InjectMemberNotAssignable,
                            location,
                            member.Name
                        )
                    );
                    return (null, diagnostics.ToImmutable());
                }
                kind = MemberKind.InjectProperty;
            }
            else
            {
                if (property.GetMethod == null)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.SingletonPropertyNotAccessible,
                            location,
                            member.Name
                        )
                    );
                    return (null, diagnostics.ToImmutable());
                }
                kind = MemberKind.SingletonProperty;
            }
        }

        if (memberType == null)
            return (null, diagnostics.ToImmutable());

        // 验证类型
        if (hasInject)
        {
            // 检查是否是 Host 类型
            if (symbols.IsHostType(memberType))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InjectMemberIsHostType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return (null, diagnostics.ToImmutable());
            }

            // 检查是否是 User 类型
            if (symbols.IsUserType(memberType))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InjectMemberIsUserType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return (null, diagnostics.ToImmutable());
            }

            // 检查是否是 Scope 类型
            if (symbols.ImplementsIScope(memberType))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InjectMemberIsScopeType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return (null, diagnostics.ToImmutable());
            }

            if (!memberType.IsValidInjectType(symbols))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InjectMemberInvalidType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return (null, diagnostics.ToImmutable());
            }
        }

        // 获取暴露类型
        var exposedTypes = ImmutableArray<ITypeSymbol>.Empty;
        if (hasSingleton)
        {
            // 检查成员类型是否是 Service 类型（Host 不应直接持有 Service 实例）
            if (symbols.IsServiceType(memberType))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.HostSingletonMemberIsServiceType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return (null, diagnostics.ToImmutable());
            }

            exposedTypes = GetExposedTypes(member, symbols);

            // 检查暴露类型是否是接口（Warning）
            foreach (var exposedType in exposedTypes)
            {
                if (exposedType.TypeKind != TypeKind.Interface)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            DiagnosticDescriptors.ExposedTypeShouldBeInterface,
                            location,
                            exposedType.ToDisplayString()
                        )
                    );
                }
            }
        }

        var info = new MemberInfo(
            Symbol: member,
            Location: location,
            Kind: kind,
            MemberType: memberType,
            ExposedTypes: exposedTypes
        );

        return (info, diagnostics.ToImmutable());
    }

    private static (ConstructorInfo?, ImmutableArray<Diagnostic>) ProcessConstructor(
        RawClassSemanticInfo raw,
        TypeRole role,
        ServiceLifetime lifetime,
        CachedSymbols symbols
    )
    {
        if (role != TypeRole.Service)
            return (null, ImmutableArray<Diagnostic>.Empty);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        var injectCtors = raw
            .Constructors.Where(c => HasAttribute(c, symbols.InjectConstructorAttribute))
            .ToImmutableArray();

        IMethodSymbol? selectedCtor = null;

        if (injectCtors.Length > 1)
        {
            diagnostics.Add(
                Diagnostic.Create(
                    DiagnosticDescriptors.AmbiguousConstructor,
                    raw.Location,
                    raw.Symbol.Name
                )
            );
            return (null, diagnostics.ToImmutable());
        }
        else if (injectCtors.Length == 1)
        {
            selectedCtor = injectCtors[0];
        }
        else
        {
            var publicCtors = raw
                .Constructors.Where(c => c.DeclaredAccessibility == Accessibility.Public)
                .ToImmutableArray();

            if (publicCtors.Length == 0)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.NoPublicConstructor,
                        raw.Location,
                        raw.Symbol.Name
                    )
                );
                return (null, diagnostics.ToImmutable());
            }

            selectedCtor = publicCtors.OrderBy(c => c.Parameters.Length).First();
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterInfo>();
        foreach (var param in selectedCtor.Parameters)
        {
            if (!param.Type.IsValidInjectType(symbols))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DiagnosticDescriptors.InjectConstructorParameterTypeInvalid,
                        param.Locations.FirstOrDefault() ?? raw.Location,
                        param.Name,
                        param.Type.ToDisplayString()
                    )
                );
                continue;
            }

            parameters.Add(
                new ParameterInfo(
                    Symbol: param,
                    Location: param.Locations.FirstOrDefault() ?? Location.None,
                    Type: param.Type
                )
            );
        }

        var ctorInfo = new ConstructorInfo(
            Symbol: selectedCtor,
            Location: selectedCtor.Locations.FirstOrDefault() ?? raw.Location,
            Parameters: parameters.ToImmutable()
        );

        return (ctorInfo, diagnostics.ToImmutable());
    }

    private static (ModulesInfo?, ImmutableArray<Diagnostic>) ProcessModules(
        RawClassSemanticInfo raw,
        CachedSymbols symbols
    )
    {
        if (!raw.HasModulesAttribute && !raw.HasAutoModulesAttribute)
            return (null, ImmutableArray<Diagnostic>.Empty);

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        if (raw.HasAutoModulesAttribute)
        {
            // AutoModules 将在图构建阶段处理
            return (
                new ModulesInfo(
                    ImmutableArray<ITypeSymbol>.Empty,
                    ImmutableArray<ITypeSymbol>.Empty,
                    true
                ),
                diagnostics.ToImmutable()
            );
        }

        var modulesAttr = raw
            .Symbol.GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.ModulesAttribute)
            );

        if (modulesAttr == null)
            return (null, diagnostics.ToImmutable());

        var instantiate = GetTypesFromAttribute(modulesAttr, "Instantiate");
        var expect = GetTypesFromAttribute(modulesAttr, "Expect");

        return (new ModulesInfo(instantiate, expect, false), diagnostics.ToImmutable());
    }

    private static ImmutableArray<ITypeSymbol> GetExposedTypes(
        ISymbol member,
        CachedSymbols symbols
    )
    {
        var singletonAttr = member
            .GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.SingletonAttribute)
            );

        if (singletonAttr == null)
            return ImmutableArray<ITypeSymbol>.Empty;

        var exposedTypes = GetTypesFromAttribute(singletonAttr, "ServiceTypes");

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

            if (memberType != null)
            {
                return ImmutableArray.Create(memberType);
            }
        }

        return exposedTypes;
    }

    private static ImmutableArray<ITypeSymbol> GetTypesFromAttribute(
        AttributeData attr,
        string propertyName
    )
    {
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();

        // 构造函数参数
        if (attr.ConstructorArguments.Length > 0)
        {
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var item in arg.Values)
                    {
                        if (item.Value is ITypeSymbol type)
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
                    if (item.Value is ITypeSymbol type)
                        builder.Add(type);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return false;
        return symbol
            .GetAttributes()
            .Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol)
            );
    }
}
