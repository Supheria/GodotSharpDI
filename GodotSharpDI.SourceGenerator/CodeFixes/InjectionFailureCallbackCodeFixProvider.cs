using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharpDI.SourceGenerator.CodeFixes;

/// <summary>
/// 为缺失的注入回调方法提供代码修复（FailureCallback 和 ReadyCallback）
/// 增强版：添加异常处理，防止 CodeFix 崩溃
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
            "GDI_U006"  // MissingInjectionReadyCallbackImplementation
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

            // 从诊断消息中提取方法名
            var message = diagnostic.GetMessage();
            var methodName = ExtractMethodNameFromMessage(message);

            if (string.IsNullOrEmpty(methodName))
                return;

            // 找到诊断位置的类声明
            var classDeclaration = root.FindToken(diagnosticSpan.Start)
                .Parent?.AncestorsAndSelf()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (classDeclaration == null)
                return;

            // 根据诊断ID确定回调类型
            var isFailureCallback = diagnostic.Id == "GDI_U004";
            var isReadyCallback = diagnostic.Id == "GDI_U006";

            if (isFailureCallback)
            {
                // 注册失败回调的代码修复
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
                // 注册就绪回调的代码修复
                var title = string.Format(Resources.CodeFix_InjectionReadyCallback, methodName);
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: title,
                        createChangedDocument: c =>
                            AddReadyCallbackImplementationAsync(
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

    private static string ExtractMethodNameFromMessage(string message)
    {
        try
        {
            // 从消息中提取方法名
            // 消息格式: "成员 'xxx' 标记了 [Inject(FailureCallback = true)]，但未实现所需的回调方法 'OnXxxInjectionFailed'。"
            // 或英文: "Member 'xxx' is marked with [Inject(FailureCallback = true)] but the required callback method 'OnXxxInjectionFailed' is not implemented."

            var startIndex = message.LastIndexOf('\'');
            if (startIndex == -1)
                return string.Empty;

            var endIndex = message.IndexOf('\'', startIndex + 1);
            if (endIndex == -1)
            {
                // 尝试查找第二组引号对
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
            // 解析失败，返回空字符串
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

            // 创建 partial 方法实现
            var method = CreateFailureCallbackMethod(methodName);

            // 找到合适的插入位置
            var newClassDeclaration = classDeclaration.AddMembers(method);

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

    private async Task<Document> AddReadyCallbackImplementationAsync(
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

            // 创建 partial 方法实现
            var method = CreateReadyCallbackMethod(methodName);

            // 找到合适的插入位置
            var newClassDeclaration = classDeclaration.AddMembers(method);

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

    private static MethodDeclarationSyntax CreateFailureCallbackMethod(string methodName)
    {
        try
        {
            // 创建方法体
            var statements = SyntaxFactory.List(
                new StatementSyntax[]
                {
                    // GD.PushError(error);
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory
                            .InvocationExpression(
                                SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName("GD"),
                                    SyntaxFactory.IdentifierName("PushError")
                                )
                            )
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.IdentifierName("error")
                                        )
                                    )
                                )
                            )
                    ),
                }
            );

            // 创建方法：partial void OnXxxInjectionFailed(string error) { ... }
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
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory
                                .Parameter(SyntaxFactory.Identifier("error"))
                                .WithType(
                                    SyntaxFactory.PredefinedType(
                                        SyntaxFactory.Token(SyntaxKind.StringKeyword)
                                    )
                                )
                        )
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
            // 如果创建方法失败，返回一个最简单的空方法
            return SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    methodName
                )
                .WithModifiers(
                    SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                )
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory
                                .Parameter(SyntaxFactory.Identifier("error"))
                                .WithType(
                                    SyntaxFactory.PredefinedType(
                                        SyntaxFactory.Token(SyntaxKind.StringKeyword)
                                    )
                                )
                        )
                    )
                )
                .WithBody(SyntaxFactory.Block());
        }
    }

    private static MethodDeclarationSyntax CreateReadyCallbackMethod(string methodName)
    {
        try
        {
            // 创建方法体
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
                                                SyntaxFactory.Literal("Dependency injection ready")
                                            )
                                        )
                                    )
                                )
                            )
                    ),
                }
            );

            // 创建方法：partial void OnXxxInjectionReady() { ... }
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
            // 如果创建方法失败，返回一个最简单的空方法
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
}
