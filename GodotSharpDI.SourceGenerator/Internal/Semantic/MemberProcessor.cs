using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
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
            var hasProvide = member.HasAttribute(_symbols.ProvideAttribute);

            if (!hasInject && !hasProvide)
                continue;

            // 检查冲突的特性组合
            if (hasInject && hasProvide)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.MemberConflictWithProvideAndInject,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasInject && _role != TypeRole.User && _role != TypeRole.Host)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectMemberNotInServiceHostOrUser,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            if (hasProvide && _role != TypeRole.Host)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMemberNotInServiceOrHost,
                        member.Locations.FirstOrDefault() ?? _raw.Location,
                        member.Name
                    )
                );
                continue;
            }

            var memberInfo = ProcessSingleMember(member, hasInject, hasProvide);
            if (memberInfo != null)
                members.Add(memberInfo);
        }

        CheckMembersEmpty(members);

        // 验证 WaitFor 依赖
        var validator = new WaitForValidator(members.ToImmutable(), _diagnostics);
        validator.ValidateAll();

        return members.ToImmutable();
    }

    private MemberInfo? ProcessSingleMember(ISymbol member, bool hasInject, bool hasProvide)
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
            if (hasProvide)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMemberIsStatic,
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

            if (hasInject)
            {
                if (field.IsReadOnly)
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
                kind = MemberKind.InjectField;
            }
            else if (hasProvide)
            {
                kind = MemberKind.ProvideField;
            }
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
            else if (hasProvide)
            {
                if (property.GetMethod == null)
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ProvidePropertyNotAccessible,
                            location,
                            member.Name
                        )
                    );
                    return null;
                }
                kind = MemberKind.ProvideProperty;

                // 检查是否是 Task<T>
                isAsync = _symbols.IsAsyncType(property.Type);
                if (isAsync && property.Type is INamedTypeSymbol taskType && taskType.IsGenericType)
                {
                    // Task<T> 的 T 就是实际类型
                    memberType = taskType.TypeArguments[0] as INamedTypeSymbol;
                }
            }
        }
        else if (member is IMethodSymbol method && hasProvide)
        {
            // [Provide] 可以用在方法上
            if (method.ReturnsVoid)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMethodReturnVoid,
                        location,
                        member.Name
                    )
                );
                return null;
            }

            if (method.Parameters.Length > 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ProvideMethodNotParameterless,
                        location,
                        member.Name
                    )
                );
                return null;
            }

            kind = MemberKind.ProvideMethod;
            isAsync = _symbols.IsAsyncType(method.ReturnType);

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
        bool hasReadyCallback = false;
        if (hasInject)
        {
            if (!ValidateInjectMemberType(memberType, member, location))
                return null;
            // 读取 FailureCallback 和 ReadyCallback 属性
            var injectAttr = member.GetAttribute(_symbols.InjectAttribute);
            foreach (var namedArg in injectAttr!.NamedArguments)
            {
                if (
                    namedArg.Key == ShortNames.FailureCallback
                    && namedArg.Value.Value is bool failureCallbackValue
                )
                {
                    hasFailureCallback = failureCallbackValue;
                }
                else if (
                    namedArg.Key == ShortNames.ReadyCallback
                    && namedArg.Value.Value is bool readyCallbackValue
                )
                {
                    hasReadyCallback = readyCallbackValue;
                }
            }
        }

        // 验证和提取 Provide 成员
        var exposedTypes = ImmutableArray<INamedTypeSymbol>.Empty;
        var waitFor = ImmutableArray<string>.Empty;

        if (hasProvide)
        {
            if (!ValidateProvideMemberType(memberType, member, location))
                return null;

            // 从 Provide 特性提取信息
            var provideAttr = member.GetAttribute(_symbols.ProvideAttribute);
            if (provideAttr != null)
            {
                // 提取 ServiceType
                exposedTypes = AttributeHelper.GetMemberExposedTypes(member, _symbols);

                // 提取 WaitFor
                foreach (var namedArg in provideAttr.NamedArguments)
                {
                    if (
                        namedArg.Key == ShortNames.WaitFor
                        && namedArg.Value.Kind == TypedConstantKind.Array
                    )
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

            ValidateProvideMemberExposedTypes(memberType, member, location, exposedTypes);
        }

        return new MemberInfo(
            Symbol: member,
            Location: location,
            Kind: kind,
            MemberType: memberType,
            ExposedTypes: exposedTypes,
            HasFailureCallback: hasFailureCallback,
            HasReadyCallback: hasReadyCallback,
            WaitFor: waitFor,
            IsAsync: isAsync,
            UsesProvide: hasProvide
        );
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

    private bool ValidateProvideMemberType(
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
                    DiagnosticDescriptors.ProvideMemberTypeIsInvalid,
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
                    DiagnosticDescriptors.ProvideMemberTypeCannotBeGeneric,
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
                        DiagnosticDescriptors.ProvideMemberIsHostType,
                        location,
                        member.Name,
                        memberType.ToDisplayString()
                    )
                );
                return false;
            }
            return true;
        }

        // 不能是 Scope 类型
        if (_symbols.ImplementsIScope(memberType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ProvideMemberIsScopeType,
                    location,
                    member.Name,
                    memberType.ToDisplayString()
                )
            );
            return false;
        }

        return true;
    }

    private void ValidateProvideMemberExposedTypes(
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
                        DiagnosticDescriptors.ProvideMemberExposedTypeCannotBeGeneric,
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
                        DiagnosticDescriptors.ProvideMemberExposedTypeShouldBeInterface,
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
                            DiagnosticDescriptors.ProvideMemberExposedTypeNotImplemented,
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
                            DiagnosticDescriptors.ProvideMemberExposedTypeNotImplemented,
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
            // Host 需要至少有一个 Provide 成员
            var provideMembers = memberInfos.Where(m => m.IsProvideMember).ToArray();
            if (provideMembers.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.HostMissingProvideMember,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
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
