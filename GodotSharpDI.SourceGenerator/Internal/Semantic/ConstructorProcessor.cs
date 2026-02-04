using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

/// <summary>
/// 构造函数处理器
/// </summary>
internal sealed class ConstructorProcessor
{
    private readonly RawClassSemanticInfo _raw;
    private readonly TypeRole _role;
    private readonly CachedSymbols _symbols;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public ConstructorProcessor(
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

    public ConstructorInfo? Process()
    {
        var injectConstructors = _raw
            .Constructors.Where(c =>
                SymbolExtensions.HasAttribute(c, _symbols.InjectConstructorAttribute)
            )
            .ToImmutableArray();

        if (_role != TypeRole.Service)
        {
            if (injectConstructors.Length > 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectConstructorAttributeIsInvalid,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
            }
            return null;
        }

        IMethodSymbol? selectedCtor = null;

        if (injectConstructors.Length > 1)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.AmbiguousConstructor,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
            return null;
        }
        else if (injectConstructors.Length == 1)
        {
            selectedCtor = injectConstructors[0];
        }
        else
        {
            var constructors = _raw.Constructors.Where(c => !c.IsStatic).ToImmutableArray();

            if (constructors.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NoNonStaticConstructor,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
                return null;
            }

            // 如果有多个公共构造函数且没有 [InjectConstructor] 标记，必须报告歧义错误
            if (constructors.Length > 1)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.AmbiguousConstructor,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
                return null;
            }

            selectedCtor = constructors[0];
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterInfo>();
        var hasInvalidParameter = false;

        foreach (var param in selectedCtor.Parameters)
        {
            if (!ValidateInjectCtorParamType(param))
            {
                hasInvalidParameter = true;
                continue;
            }

            parameters.Add(
                new ParameterInfo(
                    Symbol: param,
                    Location: param.Locations.FirstOrDefault() ?? Location.None,
                    Type: param.Type
                )
            );
        }

        // 如果存在无效参数，返回 null
        if (hasInvalidParameter)
        {
            return null;
        }

        return new ConstructorInfo(
            Symbol: selectedCtor,
            Location: selectedCtor.Locations.FirstOrDefault() ?? _raw.Location,
            Parameters: parameters.ToImmutable()
        );
    }

    private bool ValidateInjectCtorParamType(IParameterSymbol param)
    {
        var location = param.Locations.FirstOrDefault() ?? _raw.Location;
        var paramType = param.Type;

        // 必须是接口或有效类
        if (!paramType.IsValidInterfaceOrConcreteClass())
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectCtorParamTypeInvalid,
                    location,
                    param.Name,
                    paramType.ToDisplayString()
                )
            );
            return false;
        }

        // 可以是 Host 类型吗，但不推荐并产生警告
        if (_symbols.IsHostType(paramType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectCtorParamIsHostType,
                    location,
                    param.Name,
                    paramType.ToDisplayString()
                )
            );
            return true;
        }

        // 不能是 User 类型
        if (_symbols.IsUserType(paramType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectCtorParamIsUserType,
                    location,
                    param.Name,
                    paramType.ToDisplayString()
                )
            );
            return false;
        }

        // 不能是 Scope 类型
        if (_symbols.ImplementsIScope(paramType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectCtorParamIsScopeType,
                    location,
                    param.Name,
                    paramType.ToDisplayString()
                )
            );
            return false;
        }

        // 不能是普通 Node
        if (_symbols.IsNode(paramType))
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectCtorParamIsRegularNode,
                    location,
                    param.Name,
                    paramType.ToDisplayString()
                )
            );
            return false;
        }

        // 可以是非接口，但不推荐并产生警告
        if (paramType.TypeKind != TypeKind.Interface)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.InjectCtorParamTypeShouldBeInterface,
                    location,
                    param.Name,
                    paramType.ToDisplayString()
                )
            );
        }

        return true;
    }
}
