using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeInfo = Microsoft.CodeAnalysis.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Validation;

internal static class ClassTypeValidator
{
    public static ClassTypeValidateResult ValidateType(ClassType type, CachedSymbols symbols)
    {
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var roles = DetectRoles(type, symbols);

        diagnostics.AddRange(RoleValidator.Validate(roles, type, symbols));
        diagnostics.AddRange(ConstructorValidator.Validate(roles, type, symbols));
        diagnostics.AddRange(MemberValidator.Validate(roles, type, symbols));

        return new ClassTypeValidateResult(roles, diagnostics.ToImmutable());
    }

    private static ClassTypeRoles DetectRoles(ClassType type, CachedSymbols symbols)
    {
        return new ClassTypeRoles(
            IsSingleton: AttributeHelper.HasAttribute(type.Symbol, symbols.SingletonAttribute),
            IsTransient: AttributeHelper.HasAttribute(type.Symbol, symbols.TransientAttribute),
            IsHost: AttributeHelper.HasAttribute(type.Symbol, symbols.HostAttribute),
            IsUser: AttributeHelper.HasAttribute(type.Symbol, symbols.UserAttribute),
            IsScope: TypeHelper.ImplementsInterface(type.Symbol, symbols.ScopeInterface),
            IsNode: TypeHelper.Inherits(type.Symbol, symbols.GodotNodeType),
            IsServicesReady: TypeHelper.ImplementsInterface(
                type.Symbol,
                symbols.ServicesReadyInterface
            )
        );
    }
}
