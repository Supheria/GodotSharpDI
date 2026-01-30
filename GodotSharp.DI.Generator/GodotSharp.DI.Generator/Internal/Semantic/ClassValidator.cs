using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Semantic.Validation;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Semantic;

/// <summary>
/// 类验证器 - 负责验证和分类 DI 相关的类
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
    /// 执行验证并返回结果
    /// </summary>
    public ClassValidationResult Validate()
    {
        // 1. 验证 partial
        if (!ValidatePartial())
            return CreateFailureResult();

        // 2. 确定角色和生命周期
        var role = DetermineRole();
        if (role == TypeRole.None)
            return CreateFailureResult();

        // 3. 验证角色约束
        ValidateRoleConstraints(role);

        // 4. 处理成员
        var members = ProcessMembers(role);

        // 5. 处理构造函数
        var constructor = ProcessConstructor(role);

        // 6. 处理 Modules
        var modulesInfo = ProcessModules();

        return CreateSuccessResult(role, members, constructor, modulesInfo);
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
            if (_raw.HasSingletonAttribute || _raw.HasHostAttribute || _raw.HasUserAttribute)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ScopeInvalidAttribute,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }

            return TypeRole.Scope;
        }

        // Service
        if (_raw.HasSingletonAttribute)
        {
            if (_raw.HasHostAttribute || _raw.HasUserAttribute)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.HostInvalidAttribute, // TODO: 分离 Host 和 User 在此处的诊断描述
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }

            return TypeRole.Service;
        }

        // Host + User
        if (_raw.HasHostAttribute && _raw.HasUserAttribute)
        {
            return TypeRole.HostAndUser;
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

    private ConstructorInfo? ProcessConstructor(TypeRole role)
    {
        var processor = new ConstructorProcessor(_raw, role, _symbols, _diagnostics);
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

        if (modulesAttr == null)
            return null;

        var services = AttributeHelper.GetTypesFromAttribute(modulesAttr, ArgumentNames.Services);
        var hosts = AttributeHelper.GetTypesFromAttribute(modulesAttr, ArgumentNames.Hosts);

        return new ModulesInfo(services, hosts);
    }

    private ClassValidationResult CreateFailureResult()
    {
        return new ClassValidationResult(null, _diagnostics.ToImmutable());
    }

    private ClassValidationResult CreateSuccessResult(
        TypeRole role,
        ImmutableArray<MemberInfo> members,
        ConstructorInfo? constructor,
        ModulesInfo? modulesInfo
    )
    {
        var typeInfo = new TypeInfo(
            Symbol: _raw.Symbol,
            Location: _raw.Location,
            Role: role,
            ImplementsIServicesReady: _raw.ImplementsIServicesReady,
            IsNode: _raw.IsNode,
            Members: members,
            Constructor: constructor,
            ModulesInfo: modulesInfo
        );

        return new ClassValidationResult(typeInfo, _diagnostics.ToImmutable());
    }
}