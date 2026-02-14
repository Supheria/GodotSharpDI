using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

/// <summary>
/// 验证 WaitFor 依赖的正确性
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
    /// 执行所有 WaitFor 验证
    /// </summary>
    public void ValidateAll()
    {
        // 获取所有有 WaitFor 的成员
        var membersWithWaitFor = _members
            .Where(m => m.WaitFor != null && m.WaitFor.Length > 0)
            .ToImmutableArray();

        if (membersWithWaitFor.IsEmpty)
            return;

        // 验证每个成员的 WaitFor 引用
        foreach (var member in membersWithWaitFor)
        {
            ValidateMemberWaitFor(member);
        }

        // 检测循环依赖
        DetectCircularDependencies(membersWithWaitFor);
    }

    /// <summary>
    /// 验证单个成员的 WaitFor 依赖
    /// </summary>
    private void ValidateMemberWaitFor(MemberInfo member)
    {
        if (member.WaitFor == null || member.WaitFor.Length == 0)
            return;

        foreach (var depName in member.WaitFor)
        {
            // 检查字段是否存在
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

            // 警告：如果引用的不是 [Inject] 字段
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
    /// 检测 WaitFor 循环依赖
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
    /// 递归检查是否存在循环依赖
    /// </summary>
    private bool HasCircularDependency(MemberInfo member, HashSet<string> visited)
    {
        if (!visited.Add(member.Symbol.Name))
            return true; // 发现循环

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
