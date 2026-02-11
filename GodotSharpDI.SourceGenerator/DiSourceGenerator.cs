using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Coding;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharpDI.SourceGenerator;

[Generator]
public sealed class DiSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        try
        {
            InitializeInternal(context);
        }
        catch (Exception ex)
        {
            // 如果初始化阶段发生严重错误，报告诊断
            context.RegisterSourceOutput(
                context.CompilationProvider,
                (spc, _) =>
                {
                    spc.ReportDiagnostic(
                        DiagnosticBuilder.CreateAtNone(
                            DiagnosticDescriptors.GeneratorInitializationFailed,
                            ex.ToString()
                        )
                    );
                }
            );
        }
    }

    private static void InitializeInternal(IncrementalGeneratorInitializationContext context)
    {
        // 1. 语法筛选（增强容错）
        var candidateClasses = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) =>
            {
                try
                {
                    return node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0;
                }
                catch
                {
                    // 如果语法分析出错，跳过该节点
                    return false;
                }
            },
            static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
        );

        // 2. CachedSymbols（全局一次，提前创建，带异常保护）
        var symbolsProvider = context.CompilationProvider.Select(
            static (c, _) =>
            {
                try
                {
                    return new CachedSymbols(c);
                }
                catch
                {
                    // 如果符号缓存创建失败，返回null
                    // 后续处理会检测并报告
                    return null;
                }
            }
        );

        // 3. Raw 构建（类级增量）+ Raw 诊断（增强异常处理）
        var rawClassInfoResults = candidateClasses
            .Combine(context.CompilationProvider)
            .Select(
                static (pair, _) =>
                {
                    try
                    {
                        var (syntax, compilation) = pair;
                        return RawClassSemanticInfoFactory.CreateWithDiagnostics(
                            compilation,
                            syntax
                        );
                    }
                    catch (Exception ex)
                    {
                        // 捕获单个类处理的异常
                        var className = pair.Item1.Identifier.Text;
                        var diagnostic = DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ClassAnalysisFailed,
                            pair.Item1.Identifier.GetLocation(),
                            className,
                            ex.Message
                        );
                        return (Info: null, Diagnostics: ImmutableArray.Create(diagnostic));
                    }
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

        // 4. 类级验证（类级增量，增强异常处理）
        var classValidationResults = validRawInfos
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    try
                    {
                        var (raw, symbols) = pair;

                        // 检查symbols是否有效
                        if (symbols == null)
                        {
                            var diagnostic = DiagnosticBuilder.Create(
                                DiagnosticDescriptors.SymbolCacheUnavailable,
                                raw.Location,
                                raw.Symbol.Name
                            );
                            return new ClassValidationResult(
                                TypeInfo: null,
                                Diagnostics: ImmutableArray.Create(diagnostic)
                            );
                        }

                        return ClassPipeline.ValidateAndClassify(raw, symbols);
                    }
                    catch (Exception ex)
                    {
                        // 捕获验证过程的异常
                        var diagnostic = DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ClassValidationFailed,
                            pair.Item1.Location,
                            pair.Item1.Symbol.Name,
                            ex.Message
                        );
                        return new ClassValidationResult(
                            TypeInfo: null,
                            Diagnostics: ImmutableArray.Create(diagnostic)
                        );
                    }
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
            .Where(static r => r.TypeInfo?.Role == TypeRole.Service || r.TypeInfo?.Role == TypeRole.Provider)
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

        // 5.3 构建依赖图（只在所有类型信息变化时重新构建，增强异常处理）
        var graphResult = allTypesProvider
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    try
                    {
                        var (types, symbols) = pair;

                        // 检查symbols是否有效
                        if (symbols == null)
                        {
                            var diagnostic = DiagnosticBuilder.CreateAtNone(
                                DiagnosticDescriptors.GraphBuildFailed,
                                "Symbol cache unavailable"
                            );
                            return new DiGraphBuildResult(
                                Graph: null,
                                Diagnostics: ImmutableArray.Create(diagnostic)
                            );
                        }

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
                            .Select(t => new ClassValidationResult(
                                t,
                                ImmutableArray<Diagnostic>.Empty
                            ))
                            .ToImmutableArray();

                        return DiGraphBuilder.Build(allClasses, symbols);
                    }
                    catch (Exception ex)
                    {
                        // 捕获图构建的异常
                        var diagnostic = DiagnosticBuilder.CreateAtNone(
                            DiagnosticDescriptors.GraphBuildFailed,
                            ex.Message
                        );
                        return new DiGraphBuildResult(
                            Graph: null,
                            Diagnostics: ImmutableArray.Create(diagnostic)
                        );
                    }
                }
            );

        // 6. 图级诊断 + 源码输出（增强异常处理）
        context.RegisterSourceOutput(
            graphResult,
            static (spc, result) =>
            {
                try
                {
                    // 报告诊断
                    foreach (var d in result.Diagnostics)
                        spc.ReportDiagnostic(d);

                    // 生成源码
                    if (result.Graph is not null)
                        SourceEmitter.GenerateAll(spc, result.Graph);
                }
                catch (Exception ex)
                {
                    // 捕获源码输出阶段的异常
                    spc.ReportDiagnostic(
                        DiagnosticBuilder.CreateAtNone(
                            DiagnosticDescriptors.SourceOutputFailed,
                            ex.Message
                        )
                    );
                }
            }
        );
    }
}
