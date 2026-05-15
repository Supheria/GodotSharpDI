using System;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Source code generator unified entry point (enhanced version - with exception handling)
/// </summary>
internal static class SourceEmitter
{
    /// <summary>
    /// Generate all code
    /// </summary>
    public static void GenerateAll(SourceProductionContext context, DiGraph graph)
    {
        // Generate Host code
        foreach (var node in graph.HostNodes)
        {
            try
            {
                HostGenerator.Generate(context, node);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ReportCodeGenerationError(context, "Host", node.ValidatedTypeInfo, ex);
            }
        }

        // Generate User code
        foreach (var node in graph.UserNodes)
        {
            try
            {
                UserGenerator.Generate(context, node);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ReportCodeGenerationError(context, "User", node.ValidatedTypeInfo, ex);
            }
        }

        // Generate Scope code
        foreach (var node in graph.ScopeNodes)
        {
            try
            {
                ScopeGenerator.Generate(context, node, graph);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ReportCodeGenerationError(context, "Scope", node.ValidatedTypeInfo, ex);
            }
        }
    }

    /// <summary>
    /// Report code generation error
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
