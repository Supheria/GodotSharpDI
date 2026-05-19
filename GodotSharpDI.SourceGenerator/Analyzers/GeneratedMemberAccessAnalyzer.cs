using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSharpDI.SourceGenerator.Analyzers;

/// <summary>
/// Analyzer: Detects manual access to framework-generated members (methods, fields, properties)
/// Uses CachedSymbols to optimize symbol lookup
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedMemberAccessAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// List of method names that are forbidden to call manually
    /// </summary>
    private static readonly ImmutableHashSet<string> ForbiddenMethodNames = ImmutableHashSet.Create(
        // Node lifecycle (all roles)
        "GetParentScope",
        "ResetInjectionState",
        // Host lifecycle
        "ProvideServices",
        "ResolveDependencies",
        // User lifecycle
        // (ResolveDependencies shared with Host)
        // Scope lifecycle
        "StartDependencyMonitoring",
        "StopDependencyMonitoring",
        "CheckPendingDependencies",
        "ReportUnresolvedDependencies",
        // Injection callback
        "OnDependencyResolved",
        // Scope static initializers
        "CreateServiceCache",
        "CreateServiceImplementationMap",
        "CreateDeadlockDetector",
        // Scope IScope interface methods
        "ProvideService",
        "ResolveDependency"
    );

    /// <summary>
    /// List of field names that are forbidden to access manually
    /// </summary>
    private static readonly ImmutableHashSet<string> ForbiddenFieldNames = ImmutableHashSet.Create(
        // Node lifecycle (Host/User)
        "__parentScope",
        // Async provider cancellation (Host/User)
        "__lifetime_cancellation_tokens",
        // Injection dependency tracking (Host/User with IDependenciesResolved)
        "__unresolvedDependencies",
        // Scope service container
        "ServiceImplementationMap",
        "ServiceCache",
        "_waiters",
        "_deadlockDetector",
        "_dependencyCheckTimer"
    );

    /// <summary>
    /// List of property names that are forbidden to access manually (currently empty, reserved for extension)
    /// </summary>
    private static readonly ImmutableHashSet<string> ForbiddenPropertyNames =
        ImmutableHashSet<string>.Empty;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ManualCallGeneratedMethod,
            DiagnosticDescriptors.ManualAccessGeneratedField,
            DiagnosticDescriptors.ManualAccessGeneratedProperty,
            DiagnosticDescriptors.ManualSetInjectionReadyField
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

                // If IScope doesn't exist, the project doesn't use GodotSharpDI
                if (cachedSymbols.IScope == null)
                    return;

                // Register syntax node analysis
                compilationContext.RegisterSyntaxNodeAction(
                    ctx => SafeAnalyze(ctx, cachedSymbols, AnalyzeInvocation),
                    SyntaxKind.InvocationExpression
                );
                compilationContext.RegisterSyntaxNodeAction(
                    ctx => SafeAnalyze(ctx, cachedSymbols, AnalyzeMemberAccess),
                    SyntaxKind.SimpleMemberAccessExpression
                );
                compilationContext.RegisterSyntaxNodeAction(
                    ctx => SafeAnalyze(ctx, cachedSymbols, AnalyzeIdentifierName),
                    SyntaxKind.IdentifierName
                );
                compilationContext.RegisterSyntaxNodeAction(
                    ctx => SafeAnalyze(ctx, cachedSymbols, AnalyzeAssignment),
                    SyntaxKind.SimpleAssignmentExpression
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
        SyntaxNodeAnalysisContext context,
        CachedSymbols cachedSymbols,
        Action<SyntaxNodeAnalysisContext, CachedSymbols> analyze
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

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Get the method symbol being called
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // Check if method name is in forbidden list
        if (!ForbiddenMethodNames.Contains(methodSymbol.Name))
            return;

        // Check if call location is in generated code region
        if (IsInGeneratedCodeRegion(invocation))
            return;

        // Check if it's a call to a generated method
        if (!IsGeneratedMethodCall(methodSymbol, cachedSymbols, context.SemanticModel))
            return;

        // Get the call expression (this.Method() or obj.Method())
        string calledOn = GetCalledOnExpression(invocation);

        // Report diagnostic
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.ManualCallGeneratedMethod,
            invocation.GetLocation(),
            methodSymbol.Name,
            calledOn
        );

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Get the member symbol being accessed
        var symbolInfo = context.SemanticModel.GetSymbolInfo(
            memberAccess,
            context.CancellationToken
        );
        if (symbolInfo.Symbol is null)
            return;

        // Check if it's a method call (handled by AnalyzeInvocation)
        if (symbolInfo.Symbol is IMethodSymbol)
            return;

        AnalyzeMemberSymbol(
            context,
            symbolInfo.Symbol,
            memberAccess.GetLocation(),
            memberAccess.Expression.ToString()
        );
    }

    private static void AnalyzeIdentifierName(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
    {
        var identifier = (IdentifierNameSyntax)context.Node;

        // If it's the right side of a member access expression (Name part), skip (handled by AnalyzeMemberAccess)
        if (
            identifier.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == identifier
        )
            return;

        // If it's an invocation expression, skip (handled by AnalyzeInvocation)
        if (identifier.Parent is InvocationExpressionSyntax)
            return;

        // Get symbol
        var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken);
        if (symbolInfo.Symbol is null)
            return;

        // Check if it's a method (handled by AnalyzeInvocation)
        if (symbolInfo.Symbol is IMethodSymbol)
            return;

        // Determine access expression
        string accessedOn = "this";

        // If identifier is the left side of a member access expression (Expression part)
        if (identifier.Parent is MemberAccessExpressionSyntax ma && ma.Expression == identifier)
        {
            accessedOn = "this";
        }

        AnalyzeMemberSymbol(context, symbolInfo.Symbol, identifier.GetLocation(), accessedOn);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Check if in generated code region
        if (IsInGeneratedCodeRegion(assignment))
            return;

        // Get symbol of the left side of assignment
        var symbolInfo = context.SemanticModel.GetSymbolInfo(
            assignment.Left,
            context.CancellationToken
        );
        if (symbolInfo.Symbol is not IFieldSymbol fieldSymbol)
            return;

        // Check if field name matches IsXxxInjectionReady pattern
        if (!IsInjectionReadyFieldName(fieldSymbol.Name))
            return;

        // Check if the field is really a generated field
        if (!IsGeneratedField(fieldSymbol))
            return;

        // Get access expression
        string accessedOn = "this";
        if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
        {
            accessedOn = memberAccess.Expression.ToString();
        }

        // Report diagnostic
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.ManualSetInjectionReadyField,
            assignment.GetLocation(),
            fieldSymbol.Name,
            accessedOn
        );

        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsInjectionReadyFieldName(string fieldName)
    {
        return fieldName.StartsWith("Is") && fieldName.EndsWith("InjectionReady");
    }

    private static void AnalyzeMemberSymbol(
        SyntaxNodeAnalysisContext context,
        ISymbol symbol,
        Location location,
        string accessedOn
    )
    {
        // Check if in generated code region
        if (IsInGeneratedCodeRegion(context.Node))
            return;

        DiagnosticDescriptor? descriptor = null;
        string memberName = symbol.Name;

        // Check field access
        if (symbol is IFieldSymbol fieldSymbol)
        {
            if (!ForbiddenFieldNames.Contains(fieldSymbol.Name))
                return;

            // Check if the field is really a generated field
            if (!IsGeneratedField(fieldSymbol))
                return;

            descriptor = DiagnosticDescriptors.ManualAccessGeneratedField;
        }
        // Check property access
        else if (symbol is IPropertySymbol propertySymbol)
        {
            if (!ForbiddenPropertyNames.Contains(propertySymbol.Name))
                return;

            // Check if property definition is in generated file
            var propertyLocation = propertySymbol.Locations.FirstOrDefault();
            if (propertyLocation == null || !IsGeneratedFile(propertyLocation))
                return;

            descriptor = DiagnosticDescriptors.ManualAccessGeneratedProperty;
        }
        else
        {
            return;
        }

        // Report diagnostic
        var diagnostic = Diagnostic.Create(descriptor, location, memberName, accessedOn);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsGeneratedField(IFieldSymbol fieldSymbol)
    {
        try
        {
            // Method 1: Check field definition location
            var fieldLocation = fieldSymbol.Locations.FirstOrDefault();
            if (fieldLocation != null && IsGeneratedFile(fieldLocation))
            {
                return true;
            }

            // Method 2: Check field's declaring syntax
            foreach (var declaringSyntax in fieldSymbol.DeclaringSyntaxReferences)
            {
                var syntax = declaringSyntax.GetSyntax();

                if (syntax is VariableDeclaratorSyntax declarator)
                {
                    var fieldDecl = declarator.Parent?.Parent as FieldDeclarationSyntax;
                    if (fieldDecl != null)
                    {
                        var classDecl = fieldDecl.Parent as ClassDeclarationSyntax;
                        if (classDecl != null && IsGeneratedPartialClass(classDecl))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch
        {
            // If check fails, conservatively: don't report diagnostic
            return false;
        }
    }

    private static bool IsGeneratedPartialClass(ClassDeclarationSyntax classDecl)
    {
        try
        {
            // Check if in generated file
            if (classDecl.SyntaxTree?.FilePath != null)
            {
                var filePath = classDecl.SyntaxTree.FilePath;
                if (
                    filePath.Contains(".DI.g.cs")
                    || (filePath.Contains(".DI.") && filePath.EndsWith(".g.cs"))
                )
                {
                    return true;
                }
            }

            // Check for GeneratedCode attribute
            if (
                classDecl.AttributeLists.Any(attrList =>
                    attrList.Attributes.Any(attr => attr.Name.ToString().Contains("GeneratedCode"))
                )
            )
            {
                return true;
            }

            // Check if only field declarations
            var members = classDecl.Members;
            if (members.Count > 0 && members.All(m => m is FieldDeclarationSyntax))
            {
                var allFieldsPrivate = members
                    .OfType<FieldDeclarationSyntax>()
                    .All(f => f.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword)));

                if (allFieldsPrivate)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsInGeneratedCodeRegion(SyntaxNode node)
    {
        try
        {
            if (IsGeneratedFile(node.GetLocation()))
            {
                return true;
            }

            var containingClass = node.Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();
            if (containingClass != null && IsGeneratedPartialClass(containingClass))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsGeneratedMethodCall(
        IMethodSymbol methodSymbol,
        CachedSymbols cachedSymbols,
        SemanticModel semanticModel
    )
    {
        try
        {
            // Case 1: Direct call to generated private method
            var methodLocation = methodSymbol.Locations.FirstOrDefault();
            if (methodLocation != null && IsGeneratedFile(methodLocation))
            {
                return true;
            }

            // Case 2: Check if method declaration is in generated partial class
            foreach (var declaringSyntax in methodSymbol.DeclaringSyntaxReferences)
            {
                var syntax = declaringSyntax.GetSyntax();
                if (syntax is MethodDeclarationSyntax methodDecl)
                {
                    var classDecl = methodDecl.Parent as ClassDeclarationSyntax;
                    if (classDecl != null && IsGeneratedPartialClass(classDecl))
                    {
                        return true;
                    }
                }
            }

            // Case 3: Call to generated implementation method via interface
            if (methodSymbol.ContainingType != null)
            {
                var containingType = methodSymbol.ContainingType;

                if (containingType.TypeKind == TypeKind.Interface)
                {
                    if (IsIScopeMethod(cachedSymbols, containingType, methodSymbol.Name))
                    {
                        return true;
                    }
                }
                else
                {
                    if (cachedSymbols.ImplementsIScope(containingType))
                    {
                        if (IsExplicitInterfaceImplementation(methodSymbol))
                        {
                            return true;
                        }

                        var implementations = containingType.FindImplementationForInterfaceMember(
                            methodSymbol
                        );
                        if (implementations != null)
                        {
                            var implLocation = implementations.Locations.FirstOrDefault();
                            if (implLocation != null && IsGeneratedFile(implLocation))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsIScopeMethod(CachedSymbols cachedSymbols, ITypeSymbol interfaceType, string methodName)
    {
        try
        {
            if (cachedSymbols.IScope == null)
                return false;

            if (!SymbolEqualityComparer.Default.Equals(interfaceType, cachedSymbols.IScope))
                return false;

            return methodName == "ProvideService"
                || methodName == "ResolveDependency";
        }
        catch
        {
            return false;
        }
    }

    private static bool IsExplicitInterfaceImplementation(IMethodSymbol method)
    {
        try
        {
            return method.ExplicitInterfaceImplementations.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsGeneratedFile(Location location)
    {
        try
        {
            var filePath = location.SourceTree?.FilePath;
            if (filePath is null || filePath.Length == 0)
                return false;

            return filePath.Contains(".DI.g.cs")
                || (filePath.Contains(".DI.") && filePath.EndsWith(".g.cs"));
        }
        catch
        {
            return false;
        }
    }

    private static string GetCalledOnExpression(InvocationExpressionSyntax invocation)
    {
        try
        {
            return invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Expression.ToString(),
                _ => "this",
            };
        }
        catch
        {
            return "this";
        }
    }
}
