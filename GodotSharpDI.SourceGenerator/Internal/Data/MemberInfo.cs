using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// Member information
/// </summary>
internal sealed record MemberInfo(
    ISymbol Symbol,
    Location Location,
    MemberKind Kind,
    INamedTypeSymbol MemberType,
    ImmutableArray<INamedTypeSymbol> ExposedTypes,
    bool HasFailureCallback,
    bool HasReadyCallback,
    ImmutableArray<string> WaitFor,
    bool IsAsync = false
)
{
    public bool IsInjectMember { get; } =
        Kind == MemberKind.InjectField || Kind == MemberKind.InjectProperty;
    public bool IsProvideMember { get; } =
        Kind == MemberKind.ProvideField || Kind == MemberKind.ProvideProperty || Kind == MemberKind.ProvideMethod;

    /// <summary>
    /// Get this member's name
    /// </summary>
    public string Name => Symbol.Name;

    /// <summary>
    /// Whether this member has WaitFor dependencies
    /// </summary>
    public bool HasWaitFor => !WaitFor.IsEmpty;
}
