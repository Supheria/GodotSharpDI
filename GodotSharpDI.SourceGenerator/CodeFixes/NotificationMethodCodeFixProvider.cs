using System;
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

namespace GodotSharpDI.SourceGenerator.CodeFixes;

/// <summary>
/// 为缺失的 _Notification 方法提供代码修复
/// 增强版：添加异常处理，防止 CodeFix 崩溃
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
        try
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
        catch (OperationCanceledException)
        {
            // 取消操作是正常的，重新抛出
            throw;
        }
        catch (Exception)
        {
            // CodeFix 失败不应该崩溃 IDE
            // 静默忽略 - 用户只是看不到这个修复选项
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

            // 创建 _Notification 方法
            var notificationMethod = CreateNotificationMethod();

            // 找到合适的插入位置
            var newClassDeclaration = classDeclaration.AddMembers(notificationMethod);

            // 替换旧的类声明
            var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

            return document.WithSyntaxRoot(newRoot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // 修复失败，返回原文档
            return document;
        }
    }

    private static MethodDeclarationSyntax CreateNotificationMethod()
    {
        try
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
        catch (Exception)
        {
            // 如果创建方法失败，返回一个最简单的版本
            return SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "_Notification"
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }
    }
}
