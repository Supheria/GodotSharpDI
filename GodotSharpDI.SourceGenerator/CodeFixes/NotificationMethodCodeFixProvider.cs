using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharpDI.SourceGenerator.CodeFixes;

/// <summary>
/// 为缺失的 _Notification 方法提供代码修复
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NotificationMethodCodeFixProvider))]
[Shared]
public sealed class NotificationMethodCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("GDI_C080"); // MissingNotificationMethod

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context
            .Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // 找到诊断位置的类声明
        var classDeclaration = root.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration == null)
            return;

        // 注册代码修复
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

    private async Task<Document> AddNotificationMethodAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // 创建 _Notification 方法
        var notificationMethod = CreateNotificationMethod();

        // 找到合适的插入位置（在类的最后一个成员之后，或类开始位置）
        var newClassDeclaration = classDeclaration.AddMembers(notificationMethod);

        // 替换旧的类声明
        var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

        return document.WithSyntaxRoot(newRoot);
    }

    private static MethodDeclarationSyntax CreateNotificationMethod()
    {
        // 创建方法：public override partial void _Notification(int what);
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
}
