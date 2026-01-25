using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using GodotSharp.DI.Generator.Internal;
using GodotSharp.DI.Generator.Internal.Coding;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Generator.Internal.DiBuild;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Generator.Internal.Semantic;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator;

[Generator]
public sealed class DiSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 语法筛选
        var candidateClasses = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
            static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
        );

        // 2. Raw 构建（类级增量）+ Raw 诊断
        var rawClassInfoResults = candidateClasses
            .Combine(context.CompilationProvider)
            .Select(
                static (pair, _) =>
                {
                    var (syntax, compilation) = pair;
                    return RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, syntax);
                }
            );

        var rawInfos = rawClassInfoResults
            .Select(static (r, _) => r.Info)
            .Where(static info => info is not null)
            .Select(static (info, _) => info!);

        var rawDiagnostics = rawClassInfoResults.SelectMany(static (r, _) => r.Diagnostics);

        // Raw 诊断输出
        context.RegisterSourceOutput(
            rawDiagnostics,
            static (spc, diag) => spc.ReportDiagnostic(diag)
        );

        // 3. CachedSymbols（全局一次）
        var symbolsProvider = context.CompilationProvider.Select(
            static (c, _) => new CachedSymbols(c)
        );

        // 4. 类级验证（类级增量）
        //    注意：ValidateAndClassify 返回的是“纯值”，不携带 symbols
        var classValidationResults = rawInfos
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    var (raw, symbols) = pair;
                    return ClassPipeline.ValidateAndClassify(raw, symbols);
                }
            );

        // 类级诊断输出（过滤无诊断）
        var classValidationWithDiags = classValidationResults.Where(static r =>
            r.Diagnostics.Length > 0
        );

        context.RegisterSourceOutput(
            classValidationWithDiags,
            static (spc, result) =>
            {
                foreach (var d in result.Diagnostics)
                    spc.ReportDiagnostic(d);
            }
        );

        // 5. 图级构建（全局 Collect + 一次 Combine）
        var graphResult = classValidationResults
            .Collect() // ← 纯 ClassValidationResult 数组
            .Combine(symbolsProvider) // ← 全局一次 Combine
            .Select(
                static (pair, _) =>
                {
                    var (classes, symbols) = pair;
                    if (classes.IsDefaultOrEmpty)
                        return DiGraphBuildResult.Empty;

                    return DiGraphBuilder.Build(classes, symbols);
                }
            );

        // 6. 图级诊断 + 源码输出
        context.RegisterSourceOutput(
            graphResult,
            static (spc, result) =>
            {
                foreach (var d in result.Diagnostics)
                    spc.ReportDiagnostic(d);

                if (result.Graph is not null)
                    SourceEmitter.GenerateAll(spc, result.Graph);
            }
        );
    }
}
