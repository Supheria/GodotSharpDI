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
                ValidateNotificationMethod(); // 新增：Host 需要 _Notification
                break;

            case TypeRole.User:
                ValidateUserConstraints();
                ValidateNotificationMethod(); // 新增：User 需要 _Notification
                break;

            case TypeRole.Scope:
                ValidateScopeConstraints();
                ValidateNotificationMethod(); // 新增：Scope 需要 _Notification
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

        var exposedTypes = AttributeHelper.GetServiceExposedTypes(_raw.Symbol, _symbols);

        foreach (var exposedType in exposedTypes)
        {
            // 检查暴露类型是否是接口（Warning）
            if (exposedType.TypeKind != TypeKind.Interface)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.ServiceExposedTypeShouldBeInterface,
                        _raw.Location,
                        _raw.Symbol.Name,
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

    /// <summary>
    /// 验证用户代码中是否包含 _Notification 方法
    /// Host、User、Scope 必须在用户代码中定义 public override partial void _Notification(int what);
    /// </summary>
    private void ValidateNotificationMethod()
    {
        // 查找用户定义的 _Notification 方法
        var notificationMethod = _raw
            .Symbol.GetMembers("_Notification")
            .OfType<IMethodSymbol>()
            .FirstOrDefault(m =>
                m.Name == "_Notification"
                && m.Parameters.Length == 1
                && m.Parameters[0].Type.SpecialType == SpecialType.System_Int32
                && m.IsPartialDefinition
            ); // 必须是 partial 定义

        if (notificationMethod == null)
        {
            // 未找到用户定义的 _Notification 方法，报告错误
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
            // 找到了方法，验证签名是否正确
            bool isValid = true;

            // 检查是否是 public
            if (notificationMethod.DeclaredAccessibility != Accessibility.Public)
            {
                isValid = false;
            }

            // 检查是否是 override
            if (!notificationMethod.IsOverride)
            {
                isValid = false;
            }

            // 检查是否是 partial
            if (!notificationMethod.IsPartialDefinition)
            {
                isValid = false;
            }

            // 检查返回类型是否是 void
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
