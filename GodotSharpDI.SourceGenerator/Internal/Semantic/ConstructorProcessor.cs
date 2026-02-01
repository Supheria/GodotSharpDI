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
