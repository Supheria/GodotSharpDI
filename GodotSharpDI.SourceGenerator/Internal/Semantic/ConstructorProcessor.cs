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

    public void Process()
    {
        if (_role != TypeRole.Service)
        {
            return;
        }

        var publicConstructors = _raw
            .Constructors.Where(ctor =>
                ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public
            )
            .ToImmutableArray();

        // Service 必须有 public 无参构造函数
        if (publicConstructors.Length == 0)
        {
            _diagnostics.Add(
                DiagnosticBuilder.Create(
                    DiagnosticDescriptors.ServiceHasNoPublicParameterlessConstructor,
                    _raw.Location,
                    _raw.Symbol.Name
                )
            );
        }
    }
}
