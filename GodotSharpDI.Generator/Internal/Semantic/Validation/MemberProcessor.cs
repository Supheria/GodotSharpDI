using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Semantic.Validation;

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

        // 验证类型
        if (hasInject)
        {
            if (!ValidateInjectMemberType(memberType, member, location))
                return null;
        }

        // 获取暴露类型
        var exposedTypes = ImmutableArray<INamedTypeSymbol>.Empty;
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

                // 检查是否实现了暴露的接口
                if (exposedType.TypeKind == TypeKind.Interface)
                {
                    if (!memberType.ImplementsInterface(exposedType))
                    {
                        _diagnostics.Add(
                            DiagnosticBuilder.Create(
                                DiagnosticDescriptors.HostMemberExposedTypeNotImplemented,
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
                                DiagnosticDescriptors.HostMemberExposedTypeNotImplemented,
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
}
