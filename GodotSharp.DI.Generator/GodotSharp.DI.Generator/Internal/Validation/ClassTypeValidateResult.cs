using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Validation;

internal sealed record ClassTypeValidateResult(
    ClassTypeRoles Roles,
    ImmutableArray<Diagnostic> Diagnostics
)
{
    public bool HasErrors { get; } = Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
}
