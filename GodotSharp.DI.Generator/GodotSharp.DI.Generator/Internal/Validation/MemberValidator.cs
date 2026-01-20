using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.DiBuild;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Validation;

internal static class MemberValidator
{
    public static ImmutableArray<Diagnostic> Validate(
        ClassTypeRoles roles,
        ClassType type,
        CachedSymbols symbols
    )
    {
        var validator = new Impl(type, symbols, roles);
        foreach (var member in type.Symbol.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol f:
                    validator.ValidateMember(f, f.Type);
                    break;
                case IPropertySymbol p:
                    validator.ValidateMember(p, p.Type);
                    break;
            }
        }
        return validator.GetDiagnostics();
    }

    private sealed class Impl : TypeValidatorBase
    {
        public Impl(ClassType type, CachedSymbols symbols, ClassTypeRoles roles)
            : base(type, symbols, roles) { }

        public void ValidateMember(ISymbol member, ITypeSymbol memberType)
        {
            var hasSingleton = AttributeHelper.HasAttribute(member, Symbols.SingletonAttribute);
            var hasInject = AttributeHelper.HasAttribute(member, Symbols.InjectAttribute);
            if (hasSingleton && !Roles.IsHost)
            {
                Report(
                    DiagnosticDescriptors.MemberHasSingletonButNotInHost,
                    GetMemberLocation(member),
                    null,
                    Type.Name
                );
            }
            if (hasInject && !Roles.IsUser)
            {
                Report(
                    DiagnosticDescriptors.MemberHasInjectButNotInUser,
                    GetMemberLocation(member),
                    null,
                    Type.Name
                );
            }
            if (hasSingleton && hasInject)
            {
                var memberLocation = GetMemberLocation(member);
                var singletonAttr = AttributeHelper.GetAttribute(
                    member,
                    Symbols.SingletonAttribute
                );
                var injectAttr = AttributeHelper.GetAttribute(member, Symbols.InjectAttribute);
                Report(
                    DiagnosticDescriptors.MemberConflictWithSingletonAndInject,
                    memberLocation,
                    [
                        AttributeHelper.GetAttributeLocation(singletonAttr, memberLocation),
                        AttributeHelper.GetAttributeLocation(injectAttr, memberLocation),
                    ]
                );
            }
            if (hasInject)
            {
                var isAssignable = member switch
                {
                    IFieldSymbol field => !field.IsReadOnly,
                    IPropertySymbol property => property.SetMethod is not null,
                    _ => false,
                };
                if (!isAssignable)
                {
                    Report(
                        DiagnosticDescriptors.InjectMemberNotAssignable,
                        GetMemberLocation(member),
                        null
                    );
                }
            }
            if (hasSingleton)
            {
                if (member is IPropertySymbol property && property.GetMethod is null)
                {
                    Report(
                        DiagnosticDescriptors.SingletonPropertyNotAccessible,
                        GetMemberLocation(member),
                        null
                    );
                }
            }
            if (
                memberType is INamedTypeSymbol
                && AttributeHelper.HasAttribute(memberType, Symbols.HostAttribute)
            )
            {
                Report(DiagnosticDescriptors.HostCannotBeMember, GetMemberLocation(member), null);
            }
            // 成员类型是否为 Service / User 等，放到图级验证
        }
    }
}
