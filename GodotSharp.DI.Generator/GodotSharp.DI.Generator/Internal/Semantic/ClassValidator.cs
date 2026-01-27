using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Semantic;

/// <summary>
/// 类验证器 - 负责验证和分类 DI 相关的类
/// </summary>
internal sealed class ClassValidator
{
    private readonly RawClassSemanticInfo _raw;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public ClassValidator(RawClassSemanticInfo raw, CachedSymbols symbols)
    {
        _raw = raw;
        _symbols = symbols;
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    }

    /// <summary>
    /// 执行验证并返回结果
    /// </summary>
    public ClassValidationResult Validate()
    {
        // 1. 验证 partial
        if (!ValidatePartial())
            return CreateFailureResult();

        // 2. 确定角色和生命周期
        var (role, lifetime) = DetermineRoleAndLifetime();
        if (role == TypeRole.None)
            return CreateFailureResult();

        // 3. 验证角色约束
        ValidateRoleConstraints(role);

        // 4. 处理成员
        var members = ProcessMembers(role);

        // 5. 处理构造函数
        var constructor = ProcessConstructor(role, lifetime);

        // 6. 处理 Modules
        var modulesInfo = ProcessModules();

        return CreateSuccessResult(role, lifetime, members, constructor, modulesInfo);
    }

    private bool ValidatePartial()
    {
        if (!_raw.IsPartial)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.DiClassMustBePartial,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
            return false;
        }
        return true;
    }

    private (TypeRole Role, ServiceLifetime Lifetime) DetermineRoleAndLifetime()
    {
        var role = TypeRole.None;
        var lifetime = ServiceLifetime.None;

        // 检查生命周期冲突
        if (_raw.HasSingletonAttribute && _raw.HasTransientAttribute)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ServiceLifetimeConflict,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
            return (TypeRole.None, ServiceLifetime.None);
        }

        // Scope
        if (_raw.ImplementsIScope)
        {
            role = TypeRole.Scope;

            if (
                _raw.HasSingletonAttribute
                || _raw.HasTransientAttribute
                || _raw.HasHostAttribute
                || _raw.HasUserAttribute
            )
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeInvalidAttribute,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }

            return (role, lifetime);
        }

        // Service
        if (_raw.HasSingletonAttribute || _raw.HasTransientAttribute)
        {
            role = TypeRole.Service;
            lifetime = _raw.HasSingletonAttribute
                ? ServiceLifetime.Singleton
                : ServiceLifetime.Transient;

            if (_raw.HasHostAttribute || _raw.HasUserAttribute)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.HostInvalidAttribute,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }

            return (role, lifetime);
        }

        // Host + User
        if (_raw.HasHostAttribute && _raw.HasUserAttribute)
        {
            role = TypeRole.HostAndUser;
            return (role, lifetime);
        }

        // Host only
        if (_raw.HasHostAttribute)
        {
            role = TypeRole.Host;
            return (role, lifetime);
        }

        // User only
        if (_raw.HasUserAttribute)
        {
            role = TypeRole.User;
            return (role, lifetime);
        }

        return (TypeRole.None, ServiceLifetime.None);
    }

    private void ValidateRoleConstraints(TypeRole role)
    {
        switch (role)
        {
            case TypeRole.Service:
                ValidateServiceConstraints();
                break;

            case TypeRole.Host:
            case TypeRole.HostAndUser:
                ValidateHostConstraints();
                break;

            case TypeRole.Scope:
                ValidateScopeConstraints();
                break;
        }

        // 验证 IServicesReady
        if (_raw.ImplementsIServicesReady && role != TypeRole.User && role != TypeRole.HostAndUser)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ServiceReadyNeedUser,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
    }

    private void ValidateServiceConstraints()
    {
        if (_raw.IsNode)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ServiceCannotBeNode,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
        if (!_raw.Symbol.IsValidServiceType(_symbols))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ServiceTypeIsInvalid,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
    }

    private void ValidateHostConstraints()
    {
        if (!_raw.IsNode)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.HostMustBeNode,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
    }

    private void ValidateScopeConstraints()
    {
        if (!_raw.IsNode)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ScopeMustBeNode,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
        if (!_raw.HasModulesAttribute)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ScopeMissingModules,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
    }

    private ImmutableArray<MemberInfo> ProcessMembers(TypeRole role)
    {
        var processor = new MemberProcessor(_raw, role, _symbols, _diagnostics);
        return processor.Process();
    }

    private ConstructorInfo? ProcessConstructor(TypeRole role, ServiceLifetime lifetime)
    {
        var processor = new ConstructorProcessor(_raw, role, lifetime, _symbols, _diagnostics);
        return processor.Process();
    }

    private ModulesInfo? ProcessModules()
    {
        if (!_raw.HasModulesAttribute)
            return null;

        var modulesAttr = _raw
            .Symbol.GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, _symbols.ModulesAttribute)
            );

        if (modulesAttr == null)
            return null;

        var services = AttributeHelper.GetTypesFromAttribute(modulesAttr, ArgumentNames.Services);
        var hosts = AttributeHelper.GetTypesFromAttribute(modulesAttr, ArgumentNames.Hosts);

        return new ModulesInfo(services, hosts);
    }

    private ClassValidationResult CreateFailureResult()
    {
        return new ClassValidationResult(null, _diagnostics.ToImmutable());
    }

    private ClassValidationResult CreateSuccessResult(
        TypeRole role,
        ServiceLifetime lifetime,
        ImmutableArray<MemberInfo> members,
        ConstructorInfo? constructor,
        ModulesInfo? modulesInfo
    )
    {
        var typeInfo = new TypeInfo(
            Symbol: _raw.Symbol,
            Location: _raw.Location,
            Role: role,
            Lifetime: lifetime,
            ImplementsIServicesReady: _raw.ImplementsIServicesReady,
            IsNode: _raw.IsNode,
            Members: members,
            Constructor: constructor,
            ModulesInfo: modulesInfo
        );

        return new ClassValidationResult(typeInfo, _diagnostics.ToImmutable());
    }
}

/// <summary>
/// 成员处理器 - 负责处理和验证类成员
/// </summary>
internal sealed class MemberProcessor
{
    private readonly RawClassSemanticInfo _raw;
    private readonly TypeRole _role;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public MemberProcessor(
        RawClassSemanticInfo raw,
        TypeRole role,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        _raw = raw;
        _role = role;
        _symbols = symbols;
        _diagnostics = diagnostics;
    }

    public ImmutableArray<MemberInfo> Process()
    {
        var members = ImmutableArray.CreateBuilder<MemberInfo>();

        foreach (var member in _raw.Members)
        {
            var hasInject = member.HasAttribute(_symbols.InjectAttribute);
            var hasSingleton = member.HasAttribute(_symbols.SingletonAttribute);

            // 检查是否是 User 类型成员（自动识别）
            ITypeSymbol? memberType = null;
            if (member is IFieldSymbol field)
                memberType = field.Type;
            else if (member is IPropertySymbol property)
                memberType = property.Type;

            var isUserMember = memberType != null && _symbols.IsUserType(memberType);

            // 跳过既没有特性也不是 User 类型的成员
            if (!hasInject && !hasSingleton && !isUserMember)
                continue;

            if (hasInject && hasSingleton)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberConflictWithSingletonAndInject,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasInject && _role != TypeRole.User && _role != TypeRole.HostAndUser)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberHasInjectButNotInUser,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasSingleton && _role != TypeRole.Host && _role != TypeRole.HostAndUser)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberHasSingletonButNotInHost,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            // 处理 User 成员
            if (isUserMember)
            {
                // User 类型不能使用 [Inject]
                if (hasInject)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.InjectMemberIsUserType,
                            member.Locations.FirstOrDefault() ?? _raw.Location,
                            member.Name,
                            memberType.ToDisplayString()
                        )
                    );
                    continue;
                }

                var userMemberInfo = ProcessUserMember(member, memberType!);
                if (userMemberInfo != null)
                    members.Add(userMemberInfo);
                continue;
            }

            var memberInfo = ProcessSingleMember(member, hasInject, hasSingleton);
            if (memberInfo != null)
                members.Add(memberInfo);
        }

        return members.ToImmutable();
    }

    private MemberInfo? ProcessSingleMember(ISymbol member, bool hasInject, bool hasSingleton)
    {
        var location = member.Locations.FirstOrDefault() ?? Location.None;

        // 检查 static 成员
        if (member.IsStatic)
        {
            if (hasInject)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectMemberIsStatic,
                        location,
                        member.Name
                    )
                );
                return null;
            }
            if (hasSingleton)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.SingletonMemberIsStatic,
                        location,
                        member.Name
                    )
                );
                return null;
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
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.InjectMemberNotAssignable,
                            location,
                            member.Name
                        )
                    );
                    return null;
                }
                kind = MemberKind.InjectProperty;
            }
            else
            {
                if (property.GetMethod == null)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.SingletonPropertyNotAccessible,
                            location,
                            member.Name
                        )
                    );
                    return null;
                }
                kind = MemberKind.SingletonProperty;
            }
        }

        if (memberType == null)
            return null;

        // 验证类型
        if (hasInject)
        {
            if (!ValidateInjectMemberType(memberType, member, location))
                return null;
        }

        // 获取暴露类型
        var exposedTypes = ImmutableArray<ITypeSymbol>.Empty;
        if (hasSingleton)
        {
            // 检查成员类型是否是 Service 类型（Host 不应直接持有 Service 实例）
            if (_symbols.IsServiceType(memberType))
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.HostSingletonMemberIsServiceType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return null;
            }

            exposedTypes = AttributeHelper.GetExposedTypes(member, _symbols);

            // 检查暴露类型是否是接口（Warning）
            foreach (var exposedType in exposedTypes)
            {
                if (exposedType.TypeKind != TypeKind.Interface)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ExposedTypeShouldBeInterface,
                            location,
                            exposedType.ToDisplayString()
                        )
                    );
                }
            }
        }

        return new MemberInfo(
            Symbol: member,
            Location: location,
            Kind: kind,
            MemberType: memberType,
            ExposedTypes: exposedTypes
        );
    }

    private bool ValidateInjectMemberType(ITypeSymbol memberType, ISymbol member, Location location)
    {
        // 检查是否是 Host 类型
        if (_symbols.IsHostType(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsHostType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 检查是否是 User 类型
        if (_symbols.IsUserType(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsUserType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 检查是否是 Scope 类型
        if (_symbols.ImplementsIScope(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsScopeType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        if (!memberType.IsValidInjectType(_symbols))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberInvalidType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        return true;
    }

    private MemberInfo? ProcessUserMember(ISymbol member, ITypeSymbol memberType)
    {
        var location = member.Locations.FirstOrDefault() ?? Location.None;

        // 规则1: User 成员不能是 Node
        if (_symbols.IsNode(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.UserMemberCannotBeNode,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return null;
        }

        // 规则2: 只有 Node User 可以包含非 Node User
        if (!_raw.IsNode)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.NonNodeUserCannotContainUserMember,
                    location,
                    _raw.Symbol.Name,
                    member.Name
                )
            );
            return null;
        }

        // 规则3: 检查是否是 static
        if (member.IsStatic)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsStatic,
                    location,
                    member.Name
                )
            );
            return null;
        }

        MemberKind kind = MemberKind.None;
        bool hasInitializer = false;

        if (member is IFieldSymbol field)
        {
            kind = MemberKind.UserMemberField;
            hasInitializer = MemberInitializationChecker.HasInitializer(field);
        }
        else if (member is IPropertySymbol property)
        {
            kind = MemberKind.UserMemberProperty;
            if (property.GetMethod == null)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.SingletonPropertyNotAccessible,
                        location,
                        member.Name
                    )
                );
                return null;
            }

            hasInitializer = MemberInitializationChecker.HasInitializer(property);
        }

        // 规则4: User 成员必须初始化
        if (!hasInitializer)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.UserMemberMustBeInitialized,
                    location,
                    member.Name
                )
            );
            return null;
        }

        return new MemberInfo(
            Symbol: member,
            Location: location,
            Kind: kind,
            MemberType: memberType,
            ExposedTypes: ImmutableArray<ITypeSymbol>.Empty
        );
    }
}

/// <summary>
/// 构造函数处理器
/// </summary>
internal sealed class ConstructorProcessor
{
    private readonly RawClassSemanticInfo _raw;
    private readonly TypeRole _role;
    private readonly ServiceLifetime _lifetime;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public ConstructorProcessor(
        RawClassSemanticInfo raw,
        TypeRole role,
        ServiceLifetime lifetime,
        CachedSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        _raw = raw;
        _role = role;
        _lifetime = lifetime;
        _symbols = symbols;
        _diagnostics = diagnostics;
    }

    public ConstructorInfo? Process()
    {
        var injectCtors = _raw
            .Constructors.Where(c => c.HasAttribute(_symbols.InjectConstructorAttribute))
            .ToImmutableArray();

        if (_role != TypeRole.Service)
        {
            if (injectCtors.Length > 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectConstructorAttributeIsInvalid,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }
            return null;
        }

        IMethodSymbol? selectedCtor = null;

        if (injectCtors.Length > 1)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.AmbiguousConstructor,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
            return null;
        }
        else if (injectCtors.Length == 1)
        {
            selectedCtor = injectCtors[0];
        }
        else
        {
            var publicCtors = _raw.Constructors.Where(c => c.IsPublic()).ToImmutableArray();

            if (publicCtors.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NoPublicConstructor,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
                return null;
            }

            // 如果有多个公共构造函数且没有 [InjectConstructor] 标记，必须报告歧义错误
            if (publicCtors.Length > 1)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.AmbiguousConstructor,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
                return null;
            }

            selectedCtor = publicCtors[0];
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterInfo>();
        var hasInvalidParameter = false;

        foreach (var param in selectedCtor.Parameters)
        {
            if (!param.Type.IsValidInjectType(_symbols))
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectConstructorParameterTypeInvalid,
                        param.Locations.FirstOrDefault() ?? _raw.Location,
                        param.Name,
                        param.Type.ToDisplayString()
                    )
                );
                hasInvalidParameter = true;
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

        // 如果存在无效参数，返回 null
        if (hasInvalidParameter)
        {
            return null;
        }

        return new ConstructorInfo(
            Symbol: selectedCtor,
            Location: selectedCtor.Locations.FirstOrDefault() ?? _raw.Location,
            Parameters: parameters.ToImmutable()
        );
    }
}

/// <summary>
/// 成员初始化检查器 - 改进的初始化检测
/// </summary>
internal static class MemberInitializationChecker
{
    public static bool HasInitializer(IFieldSymbol field)
    {
        // 检查字段是否有初始化器（通过语法检查）
        var syntax = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax varDecl)
        {
            return varDecl.Initializer != null;
        }
        return false;
    }

    public static bool HasInitializer(IPropertySymbol property)
    {
        // 检查属性是否有初始化器
        var syntax = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        if (syntax is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax propDecl)
        {
            return propDecl.Initializer != null;
        }
        return false;
    }
}

/// <summary>
/// 特性辅助类 - 用于处理特性相关的操作
/// </summary>
internal static class AttributeHelper
{
    public static ImmutableArray<ITypeSymbol> GetExposedTypes(ISymbol member, CachedSymbols symbols)
    {
        var singletonAttr = member
            .GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, symbols.SingletonAttribute)
            );

        if (singletonAttr == null)
            return ImmutableArray<ITypeSymbol>.Empty;

        var exposedTypes = GetTypesFromAttribute(singletonAttr, ArgumentNames.ServiceTypes);

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

    public static ImmutableArray<ITypeSymbol> GetTypesFromAttribute(
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
}
