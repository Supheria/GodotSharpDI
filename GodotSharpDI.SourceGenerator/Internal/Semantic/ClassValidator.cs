using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.Shared;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

/// <summary>
/// Class validator - Responsible for validating and classifying DI-related classes
/// </summary>
internal sealed class ClassValidator
{
    private readonly RawClassSemanticInfo _raw;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public ClassValidator(RawClassSemanticInfo raw, CachedSymbols symbols)
    {
        _raw = raw;
        _symbols = symbols;
        _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    }

    /// <summary>
    /// Execute validation and return result
    /// </summary>
    public ClassValidationResult Validate()
    {
        // 1. Validate partial
        if (!ValidatePartial())
            return CreateFailureResult();

        // 2. Determine role and lifecycle
        var role = DetermineRole();
        if (role == TypeRole.None)
            return CreateFailureResult();

        // 3. Validate role constraints
        ValidateRoleConstraints(role);

        // 4. Process members
        var members = ProcessMembers(role);

        // 5. Process Modules
        var modulesInfo = ProcessModules();

        return CreateSuccessResult(role, members, modulesInfo);
    }

    private bool ValidatePartial()
    {
        if (!_raw.IsPartial)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.DiClassMustBePartial,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
            return false;
        }
        return true;
    }

    private TypeRole DetermineRole()
    {
        // Scope
        if (_raw.ImplementsIScope)
        {
            if (_raw.HasHostAttribute)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeInvalidAttribute,
                        _raw.Location,
                        _raw.Symbol.Name,
                        ShortNames.Host
                    )
                );
            }
            if (_raw.HasUserAttribute)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeInvalidAttribute,
                        _raw.Location,
                        _raw.Symbol.Name,
                        ShortNames.User
                    )
                );
            }

            return TypeRole.Scope;
        }

        if (_raw.HasModulesAttribute)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.OnlyScopeCanUseModules,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }

        // Host and User should not be used simultaneously
        if (_raw.HasHostAttribute && _raw.HasUserAttribute)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.HostInvalidAttribute,
                    _raw.Location,
                    _raw.Symbol.Name,
                    ShortNames.User
                )
            );
            // Prioritize as Host
            return TypeRole.Host;
        }

        // Host only
        if (_raw.HasHostAttribute)
        {
            return TypeRole.Host;
        }

        // User only
        if (_raw.HasUserAttribute)
        {
            return TypeRole.User;
        }

        return TypeRole.None;
    }

    private void ValidateRoleConstraints(TypeRole role)
    {
        var processor = new RoleConstraintsProcessor(_raw, role, _symbols, _diagnostics);
        processor.Process();
    }

    private ImmutableArray<MemberInfo> ProcessMembers(TypeRole role)
    {
        var processor = new MemberProcessor(_raw, role, _symbols, _diagnostics);
        return processor.Process();
    }

    private ModulesInfo? ProcessModules()
    {
        if (!_raw.HasModulesAttribute)
            return null;

        var modulesAttr = _raw
            .Symbol.GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, _symbols.ModulesAttribute)
            );

        var hosts = AttributeHelper.GetTypesFromAttribute(modulesAttr, ShortNames.Hosts);

        return new ModulesInfo(hosts);
    }

    private ClassValidationResult CreateFailureResult()
    {
        return new ClassValidationResult(null, _diagnostics.ToImmutable());
    }

    private ClassValidationResult CreateSuccessResult(
        TypeRole role,
        ImmutableArray<MemberInfo> members,
        ModulesInfo? modulesInfo
    )
    {
        var typeInfo = new ValidatedTypeInfo(
            Symbol: _raw.Symbol,
            Location: _raw.Location,
            Role: role,
            ImplementsIDependenciesResolved: _raw.ImplementsIDependenciesResolved,
            IsNode: _raw.IsNode,
            Members: members,
            ModulesInfo: modulesInfo
        );

        return new ClassValidationResult(typeInfo, _diagnostics.ToImmutable());
    }
}
