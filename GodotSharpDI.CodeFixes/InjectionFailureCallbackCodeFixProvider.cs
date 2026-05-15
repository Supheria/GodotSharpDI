using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GodotSharpDI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharpDI.CodeFixes;

/// <summary>
/// Provides code fixes for missing injection callback methods (FailureCallback and ReadyCallback)
/// Enhanced version: Adds exception handling to prevent CodeFix crashes
/// </summary>
[ExportCodeFixProvider(
    LanguageNames.CSharp,
    Name = nameof(InjectionFailureCallbackCodeFixProvider)
)]
[Shared]
public sealed class InjectionFailureCallbackCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            "GDI_U004", // MissingInjectionFailureCallbackImplementation
            "GDI_U006" // MissingInjectionReadyCallbackImplementation
        );

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        try
        {
            var root = await context
                .Document.GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (root == null)
                return;

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Extract method name from diagnostic message
            var message = diagnostic.GetMessage();
            var methodName = ExtractMethodNameFromMessage(message);

            if (string.IsNullOrEmpty(methodName))
                return;

            // Find class declaration at diagnostic location
            var classDeclaration = root.FindToken(diagnosticSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (classDeclaration == null)
                return;

            // Determine callback type based on diagnostic ID
            var isFailureCallback = diagnostic.Id == "GDI_U004";
            var isReadyCallback = diagnostic.Id == "GDI_U006";

            if (isFailureCallback)
            {
                // Register code fix for failure callback
                var title = string.Format(Resources.CodeFix_InjectionFailureCallback, methodName);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedDocument: c =>
                            AddFailureCallbackImplementationAsync(
                                context.Document,
                                classDeclaration,
                                methodName,
                                c
                            ),
                        equivalenceKey: title
                    ),
                    diagnostic
                );
            }
            else if (isReadyCallback)
            {
                // Try to get member type from semantic model for generating correct parameter signature
                string? memberTypeName = null;
                try
                {
                    var semanticModel = await context.Document
                        .GetSemanticModelAsync(context.CancellationToken)
                        .ConfigureAwait(false);

                    if (semanticModel != null)
                    {
                        var memberNode = root.FindToken(diagnosticSpan.Start).Parent;
                        var memberSymbol = memberNode != null
                            ? semanticModel.GetDeclaredSymbol(memberNode, context.CancellationToken)
                            : null;

                        if (memberSymbol is IFieldSymbol fs)
                            memberTypeName = fs.Type.ToDisplayString(
                                Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat
                            );
                        else if (memberSymbol is IPropertySymbol ps)
                            memberTypeName = ps.Type.ToDisplayString(
                                Microsoft.CodeAnalysis.SymbolDisplayFormat.FullyQualifiedFormat
                            );

                        // Remove nullable ? suffix
                        if (memberTypeName != null && memberTypeName.EndsWith("?"))
                            memberTypeName = memberTypeName.Substring(0, memberTypeName.Length - 1);
                    }
                }
                catch
                {
                    // Semantic model acquisition failed, fallback to object
                }

                // Register code fix for ready callback
                var title = string.Format(Resources.CodeFix_InjectionReadyCallback, methodName);
                var capturedMemberTypeName = memberTypeName;
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedDocument: c =>
                            AddReadyCallbackImplementationAsync(
                                context.Document,
                                classDeclaration,
                                methodName,
                                capturedMemberTypeName,
                                c
                            ),
                        equivalenceKey: title
                    ),
                    diagnostic
                );
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is normal, rethrow
            throw;
        }
        catch (Exception)
        {
            // CodeFix failure should not crash IDE
            // Silently ignore - user just won't see this fix option
        }
    }

    private static string ExtractMethodNameFromMessage(string message)
    {
        try
        {
            // Extract method name from message
            // Message format: "Member 'xxx' is marked with [Inject(FailureCallback = true)] but the required callback method 'OnXxxInjectionFailed' is not implemented."

            var startIndex = message.LastIndexOf('\'');
            if (startIndex == -1)
                return string.Empty;

            var endIndex = message.IndexOf('\'', startIndex + 1);
            if (endIndex == -1)
            {
                // Try to find the second pair of quotes
                startIndex = message.IndexOf('\'');
                if (startIndex == -1)
                    return string.Empty;

                var secondQuoteIndex = message.IndexOf('\'', startIndex + 1);
                if (secondQuoteIndex == -1)
                    return string.Empty;

                startIndex = message.IndexOf('\'', secondQuoteIndex + 1);
                if (startIndex == -1)
                    return string.Empty;

                endIndex = message.IndexOf('\'', startIndex + 1);
                if (endIndex == -1)
                    return string.Empty;
            }

            return message.Substring(startIndex + 1, endIndex - startIndex - 1);
        }
        catch (Exception)
        {
            // Parse failed, return empty string
            return string.Empty;
        }
    }

    private async Task<Document> AddFailureCallbackImplementationAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string methodName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (root == null)
                return document;

            // Create partial method implementation
            var method = CreateFailureCallbackMethod(methodName);

            // Find suitable insertion position
            var newClassDeclaration = classDeclaration.AddMembers(method);

            // Replace old class declaration
            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            return document.WithSyntaxRoot(newRoot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Fix failed, return original document
            return document;
        }
    }

    private async Task<Document> AddReadyCallbackImplementationAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string methodName,
        string? memberTypeName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (root == null)
                return document;

            // Create partial method implementation
            var method = CreateReadyCallbackMethod(methodName, memberTypeName);

            // Find suitable insertion position
            var newClassDeclaration = classDeclaration.AddMembers(method);

            // Replace old class declaration
            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            return document.WithSyntaxRoot(newRoot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Fix failed, return original document
            return document;
        }
    }

    private static MethodDeclarationSyntax CreateFailureCallbackMethod(string methodName)
    {
        try
        {
            // Create method body
            var statements = SyntaxFactory.List(
                new StatementSyntax[]
                {
                    // GD.Print("Injection ready");
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory
                            .InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("GD"),
                                    SyntaxFactory.IdentifierName("Print")
                                )
                            )
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal("Dependency injection failed")
                                            )
                                        )
                                    )
                                )
                            )
                    ),
                }
            );

            // Create method: partial void OnXxxInjectionFailed() { ... }
            var method = SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier(methodName)
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                )
                .WithParameterList(SyntaxFactory.ParameterList())
                .WithBody(SyntaxFactory.Block(statements))
                .WithLeadingTrivia(
                    SyntaxFactory.ElasticCarriageReturnLineFeed,
                    SyntaxFactory.ElasticWhitespace("    ")
                )
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            return method;
        }
        catch (Exception)
        {
            // If method creation fails, return the simplest empty method
            return SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    methodName
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                )
                .WithParameterList(SyntaxFactory.ParameterList())
                .WithBody(SyntaxFactory.Block());
        }
    }

    private static MethodDeclarationSyntax CreateReadyCallbackMethod(string methodName, string? memberTypeName)
    {
        try
        {
            // Determine parameter type: prefer known member type, otherwise fallback to object
            var typeSyntax = memberTypeName != null
                ? (TypeSyntax)SyntaxFactory.ParseTypeName(memberTypeName)
                : (TypeSyntax)SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));

            // Parameter name: Remove "On" prefix and "InjectionReady" suffix from method name, lowercase first letter
            var paramName = DeriveParmName(methodName);

            var parameter = SyntaxFactory
                .Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(typeSyntax.WithTrailingTrivia(SyntaxFactory.Space));

            // Create method body
            var statements = SyntaxFactory.List(
                new StatementSyntax[]
                {
                    // GD.Print("Dependency injection ready");
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory
                            .InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("GD"),
                                    SyntaxFactory.IdentifierName("Print")
                                )
                            )
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                SyntaxFactory.Literal("Dependency injection ready")
                                            )
                                        )
                                    )
                                )
                            )
                    ),
                }
            );

            // Create method: partial void OnXxxInjectionReady(TypeA xxx) { ... }
            var method = SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier(methodName)
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                )
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList(parameter)
                    )
                )
                .WithBody(SyntaxFactory.Block(statements))
                .WithLeadingTrivia(
                    SyntaxFactory.ElasticCarriageReturnLineFeed,
                    SyntaxFactory.ElasticWhitespace("    ")
                )
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            return method;
        }
        catch (Exception)
        {
            // If method creation fails, return the simplest empty method
            return SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    methodName
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                )
                .WithParameterList(SyntaxFactory.ParameterList())
                .WithBody(SyntaxFactory.Block());
        }
    }

    /// <summary>
    /// Extract parameter name from method name OnXxxInjectionReady (lowercase first letter of Xxx part)
    /// </summary>
    private static string DeriveParmName(string methodName)
    {
        try
        {
            const string prefix = "On";
            const string suffix = "InjectionReady";
            if (methodName.StartsWith(prefix) && methodName.EndsWith(suffix))
            {
                var middle = methodName.Substring(
                    prefix.Length,
                    methodName.Length - prefix.Length - suffix.Length
                );
                if (middle.Length > 0)
                    return char.ToLower(middle[0]) + middle.Substring(1);
            }
            return "value";
        }
        catch
        {
            return "value";
        }
    }
}
