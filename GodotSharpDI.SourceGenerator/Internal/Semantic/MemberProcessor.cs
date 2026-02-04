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

            if (!hasInject && !hasSingleton)
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

            var memberInfo = ProcessSingleMember(member, hasInject, hasSingleton);
            if (memberInfo != null)
                members.Add(memberInfo);
        }

        CheckMembersEmpty(members);

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

        INamedTypeSymbol? memberType = null;
        MemberKind kind = MemberKind.None;

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

        // 验证 Inject 成员
        if (hasInject)
        {
            if (!ValidateInjectMemberType(memberType, member, location))
                return null;
        }

        var exposedTypes = ImmutableArray<INamedTypeSymbol>.Empty;

        // 验证 Inject 成员
        if (hasSingleton)
        {
            if (!ValidateSingletonMemberType(memberType, member, location))
                return null;
            exposedTypes = AttributeHelper.GetMemberExposedTypes(member, _symbols);
            ValidateSingletonMemberExposedTypes(memberType, member, location, exposedTypes);
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
        // 必须是接口或有效类
        if (!memberType.IsValidInterfaceOrConcreteClass())
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

        // 可以是 Host 类型吗，但不推荐并产生警告
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
        // 必须是接口或有效类
        if (!memberType.IsValidInterfaceOrConcreteClass())
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
        if (_role == TypeRole.Host || _role == TypeRole.HostAndUser)
        {
            var singletonMembers = memberInfos.Where(m => m.IsSingletonMember).ToArray();
            if (singletonMembers.Length == 0)
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
        if (_role == TypeRole.User || _role == TypeRole.HostAndUser)
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
