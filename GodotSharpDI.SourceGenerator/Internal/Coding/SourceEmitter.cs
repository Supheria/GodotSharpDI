using System;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// 源代码生成器统一入口（增强版 - 带异常处理）
/// </summary>
internal static class SourceEmitter
{
    /// <summary>
    /// 生成所有代码
    /// </summary>
    public static void GenerateAll(SourceProductionContext context, DiGraph graph)
    {
        // 生成 Service 工厂
        foreach (var node in graph.ServiceNodes)
        {
            try
            {
                ServiceGenerator.Generate(context, node);
            }
            catch (Exception ex)
            {
                ReportCodeGenerationError(context, "Service", node.ValidatedTypeInfo, ex);
            }
        }

        // 生成 Host 代码
        foreach (var node in graph.HostNodes)
        {
            try
            {
                HostGenerator.Generate(context, node);
            }
            catch (Exception ex)
            {
                ReportCodeGenerationError(context, "Host", node.ValidatedTypeInfo, ex);
            }
        }

        // 生成 User 代码
        foreach (var node in graph.UserNodes)
        {
            try
            {
                UserGenerator.Generate(context, node);
            }
            catch (Exception ex)
            {
                ReportCodeGenerationError(context, "User", node.ValidatedTypeInfo, ex);
            }
        }

        // 生成 Scope 代码
        foreach (var node in graph.ScopeNodes)
        {
            try
            {
                ScopeGenerator.Generate(context, node, graph);
            }
            catch (Exception ex)
            {
                ReportCodeGenerationError(context, "Scope", node.ValidatedTypeInfo, ex);
            }
        }
    }

    /// <summary>
    /// 报告代码生成错误
    /// </summary>
    private static void ReportCodeGenerationError(
        SourceProductionContext context,
        string nodeType,
        ValidatedTypeInfo typeInfo,
        Exception exception
    )
    {
        context.ReportDiagnostic(
            DiagnosticBuilder.Create(
                DiagnosticDescriptors.CodeGenerationFailed,
                typeInfo.Location,
                nodeType,
                typeInfo.Symbol.Name,
                exception.Message
            )
        );
    }
}
