using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Semantic.Validation;

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
        var injectCtors = _raw
            .Constructors.Where(c =>
                SymbolExtensions.HasAttribute(c, _symbols.InjectConstructorAttribute)
            )
            .ToImmutableArray();

        if (_role != TypeRole.Service)
        {
            if (injectCtors.Length > 0)
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

        if (injectCtors.Length > 1)
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
        else if (injectCtors.Length == 1)
        {
            selectedCtor = injectCtors[0];
        }
        else
        {
            var publicCtors = _raw.Constructors.Where(c => c.IsPublic()).ToImmutableArray();

            if (publicCtors.Length == 0)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.NoPublicConstructor,
                        _raw.Location,
                        _raw.Symbol.Name
                    )
                );
                return null;
            }

            // 如果有多个公共构造函数且没有 [InjectConstructor] 标记，必须报告歧义错误
            if (publicCtors.Length > 1)
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

            selectedCtor = publicCtors[0];
        }

        var parameters = ImmutableArray.CreateBuilder<ParameterInfo>();
        var hasInvalidParameter = false;

        foreach (var param in selectedCtor.Parameters)
        {
            if (!param.Type.IsValidInjectType(_symbols))
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.InjectConstructorParameterTypeInvalid,
                        param.Locations.FirstOrDefault() ?? _raw.Location,
                        param.Name,
                        param.Type.ToDisplayString()
                    )
                );
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
}
