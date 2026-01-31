using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using GodotSharpDI.Generator.Internal;
using GodotSharpDI.Generator.Internal.Coding;
using GodotSharpDI.Generator.Internal.Data;
using GodotSharpDI.Generator.Internal.DiBuild;
using GodotSharpDI.Generator.Internal.Helpers;
using GodotSharpDI.Generator.Internal.Semantic;
using GodotSharpDI.Generator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharpDI.Generator;

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

        // 2. CachedSymbols（全局一次，提前创建）
        var symbolsProvider = context.CompilationProvider.Select(
            static (c, _) => new CachedSymbols(c)
        );

        // 3. Raw 构建（类级增量）+ Raw 诊断
        var rawClassInfoResults = candidateClasses
            .Combine(context.CompilationProvider)
            .Select(
                static (pair, _) =>
                {
                    var (syntax, compilation) = pair;
                    return RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, syntax);
                }
            );

        // 过滤出有效的 Raw 信息
        var validRawInfos = rawClassInfoResults
            .Where(static r => r.Info is not null)
            .Select(static (r, _) => r.Info!);

        // Raw 诊断输出（仅在有诊断时）
        var rawDiagnostics = rawClassInfoResults
            .Where(static r => r.Diagnostics.Length > 0)
            .SelectMany(static (r, _) => r.Diagnostics);

        context.RegisterSourceOutput(
            rawDiagnostics,
            static (spc, diag) => spc.ReportDiagnostic(diag)
        );

        // 4. 类级验证（类级增量）
        var classValidationResults = validRawInfos
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    var (raw, symbols) = pair;
                    return ClassPipeline.ValidateAndClassify(raw, symbols);
                }
            );

        // 类级诊断输出（仅在有诊断时）
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

        // 5. 分阶段图构建
        // 5.1 按角色分类收集（减少全局 Collect）
        var serviceTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.Service)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        var hostTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.Host)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        var userTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.User)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        var hostAndUserTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.HostAndUser)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        var scopeTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.Scope)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        // 5.2 组合所有类型信息
        var allTypesProvider = serviceTypes
            .Combine(hostTypes)
            .Combine(userTypes)
            .Combine(hostAndUserTypes)
            .Combine(scopeTypes)
            .Select(
                static (tuple, _) =>
                {
                    var ((((services, hosts), users), hostAndUsers), scopes) = tuple;
                    return (
                        Services: services,
                        Hosts: hosts,
                        Users: users,
                        HostAndUsers: hostAndUsers,
                        Scopes: scopes
                    );
                }
            );

        // 5.3 构建依赖图（只在所有类型信息变化时重新构建）
        var graphResult = allTypesProvider
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    var (types, symbols) = pair;

                    // 如果没有任何类型，返回空结果
                    if (
                        types.Services.IsEmpty
                        && types.Hosts.IsEmpty
                        && types.Users.IsEmpty
                        && types.HostAndUsers.IsEmpty
                        && types.Scopes.IsEmpty
                    )
                    {
                        return DiGraphBuildResult.Empty;
                    }

                    // 合并所有类型
                    var allClasses = types
                        .Services.Concat(types.Hosts)
                        .Concat(types.Users)
                        .Concat(types.HostAndUsers)
                        .Concat(types.Scopes)
                        .Select(t => new ClassValidationResult(t, ImmutableArray<Diagnostic>.Empty))
                        .ToImmutableArray();

                    return DiGraphBuilder.Build(allClasses, symbols);
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
