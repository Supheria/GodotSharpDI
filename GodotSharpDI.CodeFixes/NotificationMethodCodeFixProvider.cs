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
/// Provides code fix for missing _Notification method
/// Enhanced version: Adds exception handling to prevent CodeFix crashes
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NotificationMethodCodeFixProvider))]
[Shared]
public sealed class NotificationMethodCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("GDI_C060"); // MissingNotificationMethod

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

            // Find class declaration at diagnostic location
            var classDeclaration = root.FindToken(diagnosticSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (classDeclaration == null)
                return;

            // Register code fix
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Resources.CodeFix_Notification,
                    createChangedDocument: c =>
                        AddNotificationMethodAsync(context.Document, classDeclaration, c),
                    equivalenceKey: Resources.CodeFix_Notification
                ),
                diagnostic
            );
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

    private async Task<Document> AddNotificationMethodAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (root == null)
                return document;

            // Create _Notification method
            var notificationMethod = CreateNotificationMethod();

            // Find suitable insertion position
            var newClassDeclaration = classDeclaration.AddMembers(notificationMethod);

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

    private static MethodDeclarationSyntax CreateNotificationMethod()
    {
        try
        {
            // Create method: public override partial void _Notification(int what);
            var method = SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("_Notification")
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(
                        SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                        SyntaxFactory.Token(SyntaxKind.OverrideKeyword),
                        SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                    )
                )
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory
                                .Parameter(SyntaxFactory.Identifier("what"))
                                .WithType(
                                    SyntaxFactory.PredefinedType(
                                        SyntaxFactory.Token(SyntaxKind.IntKeyword)
                                    )
                                )
                        )
                    )
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(
                    SyntaxFactory.ElasticCarriageReturnLineFeed,
                    SyntaxFactory.ElasticWhitespace("    ")
                )
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            return method;
        }
        catch (Exception)
        {
            // If method creation fails, return the simplest version
            return SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "_Notification"
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }
    }
}
