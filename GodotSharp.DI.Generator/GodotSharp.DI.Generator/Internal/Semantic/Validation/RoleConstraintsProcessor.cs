using System.Collections.Immutable;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Semantic.Validation;

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
            case TypeRole.Service:
                ValidateServiceConstraints();
                break;

            case TypeRole.Host:
            case TypeRole.HostAndUser:
                ValidateHostConstraints();
                break;

            case TypeRole.User:
                ValidateUserConstraints();
                break;

            case TypeRole.Scope:
                ValidateScopeConstraints();
                break;
        }

        // 验证 IServicesReady
        if (
            _raw.ImplementsIServicesReady
            && _role != TypeRole.User
            && _role != TypeRole.HostAndUser
        )
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
}
