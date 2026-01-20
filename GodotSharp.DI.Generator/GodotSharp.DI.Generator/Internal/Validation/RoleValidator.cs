using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.DiBuild;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Validation;

internal static class RoleValidator
{
    public static ImmutableArray<Diagnostic> Validate(
        ClassTypeRoles roles,
        ClassType type,
        CachedSymbols symbols
    )
    {
        var validator = new Impl(type, symbols, roles);
        validator.ValidateServiceRole();
        validator.ValidateScopeRole();
        validator.ValidateHostRole();
        validator.ValidateUserRole();
        return validator.GetDiagnostics();
    }

    private sealed class Impl : TypeValidatorBase
    {
        public Impl(ClassType type, CachedSymbols symbols, ClassTypeRoles roles)
            : base(type, symbols, roles) { }

        public void ValidateServiceRole()
        {
            if (!Roles.IsService)
            {
                return;
            }
            if (Roles.IsSingleton && Roles.IsTransient)
            {
                Report(
                    DiagnosticDescriptors.ServiceLifetimeConflict,
                    GetAttributesLocation(Symbols.SingletonAttribute, Symbols.TransientAttribute),
                    Type.Name
                );
            }
            if (!Roles.IsNode)
            {
                return;
            }
            if (Roles.IsSingleton)
            {
                Report(
                    DiagnosticDescriptors.ServiceCannotBeNode,
                    GetAttributesLocation(Symbols.SingletonAttribute),
                    TypeNames.Singleton,
                    Type.Name
                );
            }
            if (Roles.IsTransient)
            {
                Report(
                    DiagnosticDescriptors.ServiceCannotBeNode,
                    GetAttributesLocation(Symbols.TransientAttribute),
                    TypeNames.Transient,
                    Type.Name
                );
            }
        }

        public void ValidateScopeRole()
        {
            var hasModules = AttributeHelper.HasAttribute(Type.Symbol, Symbols.ModulesAttribute);
            var hasAutoModules = AttributeHelper.HasAttribute(
                Type.Symbol,
                Symbols.AutoModulesAttribute
            );
            if (!Roles.IsScope)
            {
                if (hasModules || hasAutoModules)
                {
                    Report(
                        DiagnosticDescriptors.InvalidModuleAttribute,
                        GetAttributesLocation(
                            Symbols.ModulesAttribute,
                            Symbols.AutoModulesAttribute
                        ),
                        Type.Name
                    );
                }
                return;
            }
            if (hasModules && hasAutoModules)
            {
                Report(
                    DiagnosticDescriptors.ScopeModulesConflict,
                    GetAttributesLocation(Symbols.ModulesAttribute, Symbols.AutoModulesAttribute),
                    Type.Name
                );
            }
            if (!hasModules && !hasAutoModules)
            {
                Report(DiagnosticDescriptors.ScopeMissingModules, null, Type.Name);
            }
            if (!Roles.IsNode)
            {
                Report(DiagnosticDescriptors.ScopeMustBeNode, null, Type.Name);
            }
            InvalidAttributeIf(Roles.IsSingleton, Symbols.SingletonAttribute, TypeNames.Singleton);
            InvalidAttributeIf(Roles.IsTransient, Symbols.TransientAttribute, TypeNames.Transient);
            InvalidAttributeIf(Roles.IsHost, Symbols.HostAttribute, TypeNames.Host);
            InvalidAttributeIf(Roles.IsUser, Symbols.UserAttribute, TypeNames.User);

            return;

            void InvalidAttributeIf(bool condition, INamedTypeSymbol? attr, string attrName)
            {
                if (condition)
                {
                    Report(
                        DiagnosticDescriptors.ScopeInvalidAttribute,
                        GetAttributesLocation(attr),
                        Type.Name,
                        attrName
                    );
                }
            }
        }

        public void ValidateHostRole()
        {
            if (!Roles.IsHost)
            {
                return;
            }
            if (!Roles.IsNode)
            {
                Report(DiagnosticDescriptors.HostMustBeNode, null, Type.Name);
            }
            InvalidAttributeIf(Roles.IsSingleton, Symbols.SingletonAttribute, TypeNames.Singleton);
            InvalidAttributeIf(Roles.IsTransient, Symbols.TransientAttribute, TypeNames.Transient);

            return;

            void InvalidAttributeIf(bool condition, INamedTypeSymbol? attr, string attrName)
            {
                if (condition)
                {
                    Report(
                        DiagnosticDescriptors.HostInvalidAttribute,
                        GetAttributesLocation(attr),
                        Type.Name,
                        attrName
                    );
                }
            }
        }

        public void ValidateUserRole()
        {
            if (!Roles.IsUser)
            {
                if (Roles.IsServicesReady)
                {
                    Report(DiagnosticDescriptors.ServiceReadyNeedUser, null, Type.Name);
                }
                return;
            }

            InvalidAttributeIf(Roles.IsSingleton, Symbols.SingletonAttribute, TypeNames.Singleton);
            InvalidAttributeIf(Roles.IsTransient, Symbols.TransientAttribute, TypeNames.Transient);

            return;

            void InvalidAttributeIf(bool condition, INamedTypeSymbol? attr, string attrName)
            {
                if (condition)
                {
                    Report(
                        DiagnosticDescriptors.UserInvalidAttribute,
                        GetAttributesLocation(attr),
                        Type.Name,
                        attrName
                    );
                }
            }
        }
    }
}
