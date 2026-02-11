using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

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
            var hasProvides = member.HasAttribute(_symbols.ProvidesAttribute);

            if (!hasInject && !hasSingleton && !hasProvides)
                continue;

            // 检查冲突的特性组合
            if ((hasInject ? 1 : 0) + (hasSingleton ? 1 : 0) + (hasProvides ? 1 : 0) > 1)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberConflictWithSingletonAndInject,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name,
                        "[Inject]、[Singleton] 和 [Provides] 不能同时使用"
                    )
                );
                continue;
            }

            if (
                hasInject
                && _role != TypeRole.User
                && _role != TypeRole.Host
                && _role != TypeRole.Provider
            )
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberHasInjectButNotInUser,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name,
                        "[Inject] 只能用于 User、Host 或 Provider 类型"
                    )
                );
                continue;
            }

            if (hasSingleton && _role != TypeRole.Host)
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

            if (hasProvides && _role != TypeRole.Host && _role != TypeRole.Provider)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberHasSingletonButNotInHost,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name,
                        "[Provides] 只能用于 Host 或 Provider 类型"
                    )
                );
                continue;
            }

            var memberInfo = ProcessSingleMember(member, hasInject, hasSingleton, hasProvides);
            if (memberInfo != null)
                members.Add(memberInfo);
        }

        CheckMembersEmpty(members);

        // 验证 WaitFor 依赖
        var validator = new WaitForValidator(members.ToImmutable(), _diagnostics);
        validator.ValidateAll();

        return members.ToImmutable();
    }

    private MemberInfo? ProcessSingleMember(
        ISymbol member,
        bool hasInject,
        bool hasSingleton,
        bool hasProvides
    )
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
            if (hasSingleton || hasProvides)
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

        INamedTypeSymbol? memberType = null;
        MemberKind kind = MemberKind.None;
        bool isAsync = false;

        // 确定成员类型和Kind
        if (member is IFieldSymbol field && field.Type is INamedTypeSymbol)
        {
            memberType = (INamedTypeSymbol)field.Type;
            kind = hasInject ? MemberKind.InjectField : MemberKind.SingletonField;
        }
        else if (member is IPropertySymbol property && property.Type is INamedTypeSymbol)
        {
            memberType = (INamedTypeSymbol)property.Type;

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
            else if (hasProvides)
            {
                if (property.GetMethod == null)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.SingletonPropertyNotAccessible,
                            location,
                            member.Name,
                            "[Provides] 属性必须有 getter"
                        )
                    );
                    return null;
                }
                kind = MemberKind.ProvidesProperty;

                // 检查是否是 Task<T>
                isAsync = IsAsyncType(property.Type);
                if (isAsync && property.Type is INamedTypeSymbol taskType && taskType.IsGenericType)
                {
                    // Task<T> 的 T 就是实际类型
                    memberType = taskType.TypeArguments[0] as INamedTypeSymbol;
                }
            }
            else // hasSingleton
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
        else if (member is IMethodSymbol method && hasProvides)
        {
            // [Provides] 可以用在方法上
            if (method.ReturnsVoid)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.SingletonPropertyNotAccessible,
                        location,
                        member.Name,
                        "[Provides] 方法不能返回 void"
                    )
                );
                return null;
            }

            if (method.Parameters.Length > 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.SingletonPropertyNotAccessible,
                        location,
                        member.Name,
                        "[Provides] 方法不能有参数"
                    )
                );
                return null;
            }

            kind = MemberKind.ProvidesMethod;
            isAsync = IsAsyncType(method.ReturnType);

            if (method.ReturnType is INamedTypeSymbol returnType)
            {
                memberType = returnType;
                if (isAsync && returnType.IsGenericType)
                {
                    // Task<T> 的 T 就是实际类型
                    memberType = returnType.TypeArguments[0] as INamedTypeSymbol;
                }
            }
        }

        if (memberType == null)
            return null;

        // 验证 Inject 成员
        bool hasFailureCallback = false;
        if (hasInject)
        {
            if (!ValidateInjectMemberType(memberType, member, location))
                return null;
            // 读取 FailureCallback 属性
            var injectAttr = member.GetAttribute(_symbols.InjectAttribute);
            foreach (var namedArg in injectAttr!.NamedArguments)
            {
                if (
                    namedArg.Key == "FailureCallback"
                    && namedArg.Value.Value is bool failureCallbackValue
                )
                {
                    hasFailureCallback = failureCallbackValue;
                    break;
                }
            }
        }

        // 验证和提取 Singleton/Provides 成员
        var exposedTypes = ImmutableArray<INamedTypeSymbol>.Empty;
        var waitFor = ImmutableArray<string>.Empty;

        if (hasSingleton)
        {
            if (!ValidateSingletonMemberType(memberType, member, location))
                return null;
            exposedTypes = AttributeHelper.GetMemberExposedTypes(member, _symbols);
            ValidateSingletonMemberExposedTypes(memberType, member, location, exposedTypes);
        }
        else if (hasProvides)
        {
            if (!ValidateSingletonMemberType(memberType, member, location))
                return null;

            // 从 Provides 特性提取信息
            var providesAttr = member.GetAttribute(_symbols.ProvidesAttribute);
            if (providesAttr != null)
            {
                // 提取 ServiceType
                exposedTypes = AttributeHelper.GetMemberExposedTypes(member, _symbols);

                // 提取 WaitFor
                foreach (var namedArg in providesAttr.NamedArguments)
                {
                    if (namedArg.Key == "WaitFor" && namedArg.Value.Kind == TypedConstantKind.Array)
                    {
                        var waitForList = ImmutableArray.CreateBuilder<string>();
                        foreach (var element in namedArg.Value.Values)
                        {
                            if (
                                element.Value is string fieldName
                                && !string.IsNullOrWhiteSpace(fieldName)
                            )
                            {
                                waitForList.Add(fieldName);
                            }
                        }
                        if (waitForList.Count > 0)
                        {
                            waitFor = waitForList.ToImmutable();
                        }
                    }
                }
            }

            if (exposedTypes.IsEmpty)
            {
                // 如果没有显式指定，使用成员类型
                exposedTypes = ImmutableArray.Create(memberType);
            }

            ValidateSingletonMemberExposedTypes(memberType, member, location, exposedTypes);
        }

        return new MemberInfo(
            Symbol: member,
            Location: location,
            Kind: kind,
            MemberType: memberType,
            ExposedTypes: exposedTypes,
            HasFailureCallback: hasFailureCallback,
            WaitFor: waitFor,
            IsAsync: isAsync,
            UsesProvides: hasProvides
        );
    }

    /// <summary>
    /// 检查类型是否是 Task 或 Task&lt;T&gt;
    /// </summary>
    private bool IsAsyncType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        var fullName = namedType.ToFullyQualifiedName();
        return fullName.StartsWith("System.Threading.Tasks.Task");
    }

    private bool ValidateInjectMemberType(ITypeSymbol memberType, ISymbol member, Location location)
    {
        // 必须是接口或具体类
        if (!memberType.IsInterfaceOrConcreteClass())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberTypeIsInvalid,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 不能是开放泛型类型（封闭泛型是允许的，如 List<int>）
        if (memberType is INamedTypeSymbol namedType && namedType.IsUnboundGenericType())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberTypeCannotBeGeneric,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 可以是 Host 类型，但不推荐并产生警告
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
            return true;
        }

        // 不能是 User 类型
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

        // 不能是 Scope 类型
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

        // 不能是普通 Node
        if (_symbols.IsNode(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberIsRegularNode,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 可以是非接口，但不推荐并产生警告
        if (memberType.TypeKind != TypeKind.Interface)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectMemberTypeShouldBeInterface,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
        }

        return true;
    }

    private bool ValidateSingletonMemberType(
        ITypeSymbol memberType,
        ISymbol member,
        Location location
    )
    {
        // 必须是接口或具体类
        if (!memberType.IsInterfaceOrConcreteClass())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.SingletonMemberTypeIsInvalid,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 不能是开放泛型类型（封闭泛型是允许的，如 List<int>）
        if (memberType is INamedTypeSymbol namedType && namedType.IsUnboundGenericType())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.SingletonMemberTypeCannotBeGeneric,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 不能是 Service 类型（Host 不应直接持有 Service 实例）
        if (_symbols.IsServiceType(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.SingletonMemberIsServiceType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 检查 Host 类型
        if (_symbols.IsHostType(memberType))
        {
            // 不允许除自身类型之外的 Host 类型
            if (!SymbolEqualityComparer.Default.Equals(memberType, _raw.Symbol))
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.SingletonMemberIsHostType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return false;
            }
            return true;
        }

        // 不能是 User 类型
        if (_symbols.IsUserType(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.SingletonMemberIsUserType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 不能是 Scope 类型
        if (_symbols.ImplementsIScope(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.SingletonMemberIsScopeType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        // 不能是普通 Node
        if (_symbols.IsNode(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.SingletonMemberIsRegularNode,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        return true;
    }

    private void ValidateSingletonMemberExposedTypes(
        ITypeSymbol memberType,
        ISymbol member,
        Location location,
        ImmutableArray<INamedTypeSymbol> exposedTypes
    )
    {
        foreach (var exposedType in exposedTypes)
        {
            // 不能是开放泛型类型（封闭泛型是允许的，如 IList<int>）
            if (exposedType.IsUnboundGenericType())
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.SingletonMemberExposedTypeCannotBeGeneric,
                        location,
                        member.Name,
                        exposedType.ToDisplayString()
                    )
                );
            }

            // 可以是非接口，但不推荐并产生警告
            if (exposedType.TypeKind != TypeKind.Interface)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.SingletonMemberExposedTypeShouldBeInterface,
                        location,
                        exposedType.ToDisplayString()
                    )
                );
            }

            // 检查是否实现了暴露的接口
            if (exposedType.TypeKind == TypeKind.Interface)
            {
                if (!memberType.ImplementsInterface(exposedType))
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.SingletonMemberExposedTypeNotImplemented,
                            location,
                            member.Name,
                            exposedType.ToDisplayString(),
                            memberType.ToDisplayString()
                        )
                    );
                }
            }
            // 检查是否是继承关系
            else if (exposedType.TypeKind == TypeKind.Class)
            {
                if (
                    !SymbolEqualityComparer.Default.Equals(memberType, exposedType)
                    && !memberType.InheritsFrom(exposedType)
                )
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.SingletonMemberExposedTypeNotImplemented,
                            location,
                            member.Name,
                            exposedType.ToDisplayString(),
                            memberType.ToDisplayString()
                        )
                    );
                }
            }
        }
    }

    private void CheckMembersEmpty(ImmutableArray<MemberInfo>.Builder memberInfos)
    {
        if (_role == TypeRole.Host)
        {
            // Host 需要至少有一个 Singleton 或 Provides 成员
            var provideMembers = memberInfos
                .Where(m => m.IsSingletonMember || m.IsProvidesMember)
                .ToArray();
            if (provideMembers.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.HostMissingSingletonMember,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }
        }

        if (_role == TypeRole.Provider)
        {
            // Provider 需要至少有一个 Provides 成员
            var providesMembers = memberInfos.Where(m => m.IsProvidesMember).ToArray();
            if (providesMembers.Length == 0)
            {
                // 可以添加一个警告，但不强制要求
                // Provider 可能只做依赖注入而不提供服务（虽然这不太常见）
            }
        }

        if (_role == TypeRole.User)
        {
            var injectMembers = memberInfos.Where(m => m.IsInjectMember).ToArray();
            if (injectMembers.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.UserMissingInjectMember,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }
        }
    }
}
