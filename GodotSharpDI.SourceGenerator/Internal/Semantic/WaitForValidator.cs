using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

/// <summary>
/// Validates the correctness of WaitFor dependencies
/// </summary>
internal sealed class WaitForValidator
{
    private readonly ImmutableArray<MemberInfo> _members;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics;

    public WaitForValidator(
        ImmutableArray<MemberInfo> members,
        ImmutableArray<Diagnostic>.Builder diagnostics
    )
    {
        _members = members;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Execute all WaitFor validations
    /// </summary>
    public void ValidateAll()
    {
        // Get all members with WaitFor
        var membersWithWaitFor = _members
            .Where(m => m.WaitFor != null && m.WaitFor.Length > 0)
            .ToImmutableArray();

        if (membersWithWaitFor.IsEmpty)
            return;

        // Validate each member's WaitFor references
        foreach (var member in membersWithWaitFor)
        {
            ValidateMemberWaitFor(member);
        }

        // Detect circular dependencies
        DetectCircularDependencies(membersWithWaitFor);
    }

    /// <summary>
    /// Validate a single member's WaitFor dependencies
    /// </summary>
    private void ValidateMemberWaitFor(MemberInfo member)
    {
        if (member.WaitFor == null || member.WaitFor.Length == 0)
            return;

        foreach (var depName in member.WaitFor)
        {
            // Check if field exists
            var field = _members.FirstOrDefault(m => m.Symbol.Name == depName);

            if (field == null)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.WaitForFieldNotFound,
                        member.Location,
                        depName,
                        member.Symbol.Name
                    )
                );
                continue;
            }

            // Warning: if the referenced field is not an [Inject] field
            if (!field.IsInjectMember)
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.WaitForFieldNotInjected,
                        member.Location,
                        depName
                    )
                );
            }
        }
    }

    /// <summary>
    /// Detect WaitFor circular dependencies
    /// </summary>
    private void DetectCircularDependencies(ImmutableArray<MemberInfo> membersWithWaitFor)
    {
        foreach (var member in membersWithWaitFor)
        {
            var visited = new HashSet<string>();
            if (HasCircularDependency(member, visited))
            {
                _diagnostics.Add(
                    DiagnosticBuilder.Create(
                        DiagnosticDescriptors.WaitForCircularDependency,
                        member.Location,
                        member.Symbol.Name
                    )
                );
            }
        }
    }

    /// <summary>
    /// Recursively check for circular dependencies
    /// </summary>
    private bool HasCircularDependency(MemberInfo member, HashSet<string> visited)
    {
        if (!visited.Add(member.Symbol.Name))
            return true; // Cycle detected

        if (member.WaitFor != null)
        {
            foreach (var depName in member.WaitFor)
            {
                var depMember = _members.FirstOrDefault(m => m.Symbol.Name == depName);
                if (depMember != null && depMember.WaitFor != null && depMember.WaitFor.Length > 0)
                {
                    if (HasCircularDependency(depMember, visited))
                        return true;
                }
            }
        }

        visited.Remove(member.Symbol.Name);
        return false;
    }
}
