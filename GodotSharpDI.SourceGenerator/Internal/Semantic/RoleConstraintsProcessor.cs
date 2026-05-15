using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

internal sealed class RoleConstraintsProcessor
{
    private readonly RawClassSemanticInfo _raw;
    private readonly TypeRole _role;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public RoleConstraintsProcessor(
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

    public void Process()
    {
        switch (_role)
        {
            case TypeRole.Host:
                ValidateHostConstraints();
                ValidateNotificationMethod();
                break;

            case TypeRole.User:
                ValidateUserConstraints();
                ValidateNotificationMethod();
                break;

            case TypeRole.Scope:
                ValidateScopeConstraints();
                ValidateNotificationMethod();
                break;
        }

        // Validate IDependenciesResolved
        if (
            _raw.ImplementsIDependenciesResolved
            && _role != TypeRole.User
            && _role != TypeRole.Host
        )
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.IDependenciesResolvedInvalid,
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

        // Cannot be a generic type
        if (_raw.Symbol.IsGenericType)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.HostCannotBeGenericType,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
    }

    private void ValidateUserConstraints()
    {
        if (!_raw.IsNode)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.UserMustBeNode,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }

        // Cannot be a generic type
        if (_raw.Symbol.IsGenericType)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.UserCannotBeGenericType,
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

        // Cannot be a generic type
        if (_raw.Symbol.IsGenericType)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ScopeCannotBeGenericType,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
    }

    /// <summary>
    /// Validate that user code contains the _Notification method
    /// Host, User, Scope must define public override partial void _Notification(int what); in user code
    /// </summary>
    private void ValidateNotificationMethod()
    {
        // Find user-defined _Notification method
        var notificationMethod = _raw
            .Symbol.GetMembers("_Notification")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Name == "_Notification"
                && m.Parameters.Length == 1
                && m.Parameters[0].Type.SpecialType == SpecialType.System_Int32
                && m.IsPartialDefinition
            ); // Must be a partial definition

        if (notificationMethod == null)
        {
            // User-defined _Notification method not found, report error
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.MissingNotificationMethod,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
        else
        {
            // Method found, validate signature correctness
            bool isValid = true;

            // Check if it is public
            if (notificationMethod.DeclaredAccessibility != Accessibility.Public)
            {
                isValid = false;
            }

            // Check if it is override
            if (!notificationMethod.IsOverride)
            {
                isValid = false;
            }

            // Check if it is partial
            if (!notificationMethod.IsPartialDefinition)
            {
                isValid = false;
            }

            // Check if return type is void
            if (notificationMethod.ReturnsVoid == false)
            {
                isValid = false;
            }

            if (!isValid)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InvalidNotificationMethodSignature,
                        notificationMethod.Locations.FirstOrDefault() ?? _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }
        }
    }
}
