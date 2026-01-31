using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
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

        var singletonAttr = _raw.Symbol.GetAttribute(_symbols.SingletonAttribute);
        var exposedTypes = AttributeHelper.GetTypesFromAttribute(
            singletonAttr,
            ArgumentNames.ServiceTypes
        );

        foreach (var exposedType in exposedTypes)
        {
            if (exposedType.TypeKind != TypeKind.Interface)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ExposedTypeShouldBeInterface,
                        _raw.Location,
                        exposedType.ToDisplayString()
                    )
                );
            }

            // 检查是否实现了暴露的接口
            if (exposedType.TypeKind == TypeKind.Interface)
            {
                if (!_raw.Symbol.ImplementsInterface(exposedType))
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ServiceExposedTypeNotImplemented,
                            _raw.Location,
                            _raw.Symbol.Name,
                            exposedType.ToDisplayString()
                        )
                    );
                }
            }
            // 检查是否是继承关系
            else if (exposedType.TypeKind == TypeKind.Class)
            {
                if (
                    !SymbolEqualityComparer.Default.Equals(_raw.Symbol, exposedType)
                    && !_raw.Symbol.InheritsFrom(exposedType)
                )
                {
                    _diagnostics.Add(
                        DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ServiceExposedTypeNotImplemented,
                            _raw.Location,
                            _raw.Symbol.Name,
                            exposedType.ToDisplayString()
                        )
                    );
                }
            }
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
