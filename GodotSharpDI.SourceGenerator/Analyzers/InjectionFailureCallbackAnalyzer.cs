using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSharpDI.SourceGenerator.Analyzers;

/// <summary>
/// Analyzer: Detects missing injection callback method implementations (FailureCallback and ReadyCallback)
/// Uses CachedSymbols to optimize symbol lookup
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectionFailureCallbackAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingInjectionFailureCallbackImplementation,
            DiagnosticDescriptors.MissingInjectionReadyCallbackImplementation
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Use CompilationStartAction to initialize CachedSymbols
        context.RegisterCompilationStartAction(compilationContext =>
        {
            try
            {
                var cachedSymbols = new CachedSymbols(compilationContext.Compilation);

                // If UserAttribute or HostAttribute doesn't exist, the project doesn't use GodotSharpDI
                if (cachedSymbols.UserAttribute == null && cachedSymbols.HostAttribute == null)
                    return;

                // Register symbol analysis, pass CachedSymbols
                compilationContext.RegisterSymbolAction(
                    ctx => SafeAnalyze(ctx, cachedSymbols, AnalyzeNamedType),
                    SymbolKind.NamedType
                );
            }
            catch (Exception)
            {
                // Initialization failed, silently ignore
            }
        });
    }

    /// <summary>
    /// Safe wrapper: Catches exceptions during analysis to prevent analyzer crashes
    /// </summary>
    private static void SafeAnalyze(
        SymbolAnalysisContext context,
        CachedSymbols cachedSymbols,
        Action<SymbolAnalysisContext, CachedSymbols> analyze
    )
    {
        try
        {
            analyze(context, cachedSymbols);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is normal, no need to report
            throw;
        }
        catch (Exception)
        {
            // Analyzer should not crash
            // Silently ignore errors because analyzer failure should not prevent compilation
        }
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, CachedSymbols cachedSymbols)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        // Only check classes marked with [User] or [Host]
        if (!cachedSymbols.IsUserType(typeSymbol) && !cachedSymbols.IsHostType(typeSymbol))
            return;

        // Collect all members marked with [Inject]
        var allInjectMembers = typeSymbol
            .GetMembers()
            .Where(m => m is IFieldSymbol or IPropertySymbol)
            .Where(m => cachedSymbols.HasInjectAttribute(m))
            .ToArray();

        if (allInjectMembers.Length == 0)
            return;

        // Collect all partial methods in the class
        var partialMethods = typeSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsPartialDefinition || m.PartialImplementationPart != null)
            .ToArray();

        // Check each Inject member
        foreach (var member in allInjectMembers)
        {
            try
            {
                // Check FailureCallback
                if (cachedSymbols.HasInjectWithFailureCallback(member))
                {
                    AnalyzeMemberFailureCallback(context, member, partialMethods, typeSymbol);
                }

                // Check ReadyCallback
                if (cachedSymbols.HasInjectWithReadyCallback(member))
                {
                    AnalyzeMemberReadyCallback(context, member, partialMethods, typeSymbol);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Single member analysis failure should not affect other members
                // Silently ignore, continue processing next member
            }
        }
    }

    /// <summary>
    /// Analyze a single member's failure callback implementation
    /// </summary>
    private static void AnalyzeMemberFailureCallback(
        SymbolAnalysisContext context,
        ISymbol member,
        IMethodSymbol[] partialMethods,
        INamedTypeSymbol typeSymbol
    )
    {
        var expectedMethodName = NamingHelper.GetFailureCallbackMethodName(member.Name);

        // Check if corresponding partial method implementation exists
        var hasImplementation = partialMethods.Any(m =>
        {
            try
            {
                // Must have the same method name
                if (m.Name != expectedMethodName)
                    return false;

                // Must have an implementation part
                if (m.PartialImplementationPart == null)
                    return false;

                // Check if signature matches: partial void OnXxxInjectionFailed()
                return m.ReturnsVoid && m.Parameters.Length == 0;
            }
            catch
            {
                // Check failed, conservatively: consider as not matching
                return false;
            }
        });

        if (!hasImplementation)
        {
            // Report diagnostic - use member's location
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MissingInjectionFailureCallbackImplementation,
                member.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                member.Name,
                expectedMethodName
            );

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Analyze a single member's ready callback implementation
    /// </summary>
    private static void AnalyzeMemberReadyCallback(
        SymbolAnalysisContext context,
        ISymbol member,
        IMethodSymbol[] partialMethods,
        INamedTypeSymbol typeSymbol
    )
    {
        var expectedMethodName = NamingHelper.GetReadyCallbackMethodName(member.Name);

        // Get member type for checking parameter signature
        INamedTypeSymbol? memberType = null;
        if (member is IFieldSymbol field)
            memberType = field.Type as INamedTypeSymbol;
        else if (member is IPropertySymbol prop)
            memberType = prop.Type as INamedTypeSymbol;

        // Check if corresponding partial method implementation exists
        var hasImplementation = partialMethods.Any(m =>
        {
            try
            {
                // Must have the same method name
                if (m.Name != expectedMethodName)
                    return false;

                // Must have an implementation part
                if (m.PartialImplementationPart == null)
                    return false;

                // Check if signature matches: partial void OnXxxInjectionReady(TypeA a)
                // Parameter type must be compatible with member type (ignoring nullable annotations)
                if (!m.ReturnsVoid || m.Parameters.Length != 1)
                    return false;

                if (memberType == null)
                    return true; // When type cannot be determined, conservatively pass

                var paramType = m.Parameters[0].Type;
                return SymbolEqualityComparer.Default.Equals(
                    paramType.WithNullableAnnotation(NullableAnnotation.None),
                    memberType.WithNullableAnnotation(NullableAnnotation.None)
                );
            }
            catch
            {
                // Check failed, conservatively: consider as not matching
                return false;
            }
        });

        if (!hasImplementation)
        {
            // Report diagnostic - use member's location
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MissingInjectionReadyCallbackImplementation,
                member.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                member.Name,
                expectedMethodName
            );

            context.ReportDiagnostic(diagnostic);
        }
    }
}
