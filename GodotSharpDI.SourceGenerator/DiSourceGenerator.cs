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
            // If a critical error occurs during initialization, report diagnostic
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
        // 1. Syntax filtering (enhanced fault tolerance)
        var candidateClasses = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) =>
            {
                try
                {
                    return node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0;
                }
                catch
                {
                    // Skip this node if syntax analysis fails
                    return false;
                }
            },
            static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
        );

        // 2. CachedSymbols (created once globally, with exception protection)
        var symbolsProvider = context.CompilationProvider.Select(
            static (c, _) =>
            {
                try
                {
                    return new CachedSymbols(c);
                }
                catch
                {
                    // Return null if symbol cache creation fails
                    // Subsequent processing will detect and report
                    return null;
                }
            }
        );

        // 3. Raw construction (class-level incremental) + Raw diagnostics (enhanced exception handling)
        var rawClassInfoResults = candidateClasses
            .Combine(context.CompilationProvider)
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    try
                    {
                        var ((syntax, compilation), symbols) = pair;
                        return symbols != null
                            ? RawClassSemanticInfoFactory.CreateWithDiagnostics(
                                compilation, syntax, symbols)
                            : RawClassSemanticInfoFactory.CreateWithDiagnostics(
                                compilation, syntax);
                    }
                    catch (Exception ex)
                    {
                        // Catch exception for individual class processing
                        var (syntax, compilation) = pair.Left;
                        var className = syntax.Identifier.Text;
                        var diagnostic = DiagnosticBuilder.Create(
                            DiagnosticDescriptors.ClassAnalysisFailed,
                            syntax.Identifier.GetLocation(),
                            className,
                            ex.Message
                        );
                        return (Info: null, Diagnostics: ImmutableArray.Create(diagnostic));
                    }
                }
            );

        // Filter out valid Raw information
        var validRawInfos = rawClassInfoResults
            .Where(static r => r.Info is not null)
            .Select(static (r, _) => r.Info!);

        // Raw diagnostic output (only when diagnostics exist)
        var rawDiagnostics = rawClassInfoResults
            .Where(static r => r.Diagnostics.Length > 0)
            .SelectMany(static (r, _) => r.Diagnostics);

        context.RegisterSourceOutput(
            rawDiagnostics,
            static (spc, diag) => spc.ReportDiagnostic(diag)
        );

        // 4. Class-level validation (class-level incremental, enhanced exception handling)
        var classValidationResults = validRawInfos
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    try
                    {
                        var (raw, symbols) = pair;

                        // Check if symbols are valid
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
                        // Catch exception during validation process
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

        // Class-level diagnostic output (only when diagnostics exist)
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

        // 5. Phased graph construction
        // 5.1 Collect by role classification (reduce global Collect)
        var hostTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.Host)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        var userTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.User)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        var scopeTypes = classValidationResults
            .Where(static r => r.TypeInfo?.Role == TypeRole.Scope)
            .Select(static (r, _) => r.TypeInfo!)
            .Collect();

        // 5.2 Combine all type information
        var allTypesProvider = hostTypes
            .Combine(userTypes)
            .Combine(scopeTypes)
            .Select(
                static (tuple, _) =>
                {
                    var ((hosts, users), scopes) = tuple;
                    return (Hosts: hosts, Users: users, Scopes: scopes);
                }
            );

        // 5.3 Build dependency graph (only rebuild when all type information changes, enhanced exception handling)
        var graphResult = allTypesProvider
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    try
                    {
                        var (types, symbols) = pair;

                        // Check if symbols are valid
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

                        // Return empty result if no types exist
                        if (types.Hosts.IsEmpty && types.Users.IsEmpty && types.Scopes.IsEmpty)
                        {
                            return DiGraphBuildResult.Empty;
                        }

                        // Merge all types
                        var allClasses = types
                            .Hosts.Concat(types.Users)
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
                        // Catch exception during graph construction
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

        // 6. Graph-level diagnostics + source output (enhanced exception handling)
        context.RegisterSourceOutput(
            graphResult,
            static (spc, result) =>
            {
                try
                {
                    // Report diagnostics
                    foreach (var d in result.Diagnostics)
                        spc.ReportDiagnostic(d);

                    // Generate source code
                    if (result.Graph is not null)
                        SourceEmitter.GenerateAll(spc, result.Graph);
                }
                catch (Exception ex)
                {
                    // Catch exception during source output phase
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
