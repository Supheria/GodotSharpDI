using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.DiBuild;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharp.DI.Generator.Internal.Validation;

internal static class ConstructorValidator
{
    public static ImmutableArray<Diagnostic> Validate(
        ClassTypeRoles roles,
        ClassType type,
        CachedSymbols symbols
    )
    {
        var validator = new Impl(type, symbols, roles);
        validator.ValidateInjectConstructorUsage();
        validator.ValidateServiceConstructors();
        return validator.GetDiagnostics();
    }

    private sealed class Impl : TypeValidatorBase
    {
        public Impl(ClassType type, CachedSymbols symbols, ClassTypeRoles roles)
            : base(type, symbols, roles) { }

        public void ValidateInjectConstructorUsage()
        {
            if (Roles.IsService)
            {
                return;
            }
            var hasInjectConstructor = Type.Symbol.InstanceConstructors.Any(c =>
                AttributeHelper.HasAttribute(c, Symbols.InjectConstructorAttribute)
            );
            if (hasInjectConstructor)
            {
                Report(DiagnosticDescriptors.InvalidInjectConstructorAttribute, null, Type.Name);
            }
        }

        public void ValidateServiceConstructors()
        {
            if (!Roles.IsService)
            {
                return;
            }
            var constructors = Type.Symbol.InstanceConstructors.Where(c => !c.IsStatic).ToArray();
            if (constructors.Length <= 0)
            {
                Report(DiagnosticDescriptors.NoPublicConstructor, null, Type.Name);
                return;
            }
            if (constructors.Length == 1)
            {
                return;
            }
            var injectConstructors = constructors
                .Where(c => AttributeHelper.HasAttribute(c, Symbols.InjectConstructorAttribute))
                .ToArray();
            if (injectConstructors.Length == 1)
            {
                return;
            }
            Report(DiagnosticDescriptors.AmbiguousConstructor, null, Type.Name);
            foreach (var ctor in constructors)
            {
                var syntax = ctor.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                if (syntax is ConstructorDeclarationSyntax ctorSyntax)
                {
                    Report(
                        DiagnosticDescriptors.AmbiguousConstructor,
                        ctorSyntax.Identifier.GetLocation(),
                        null,
                        Type.Name
                    );
                }
            }
        }
    }
}
