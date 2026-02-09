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
/// 为缺失的注入失败回调方法提供代码修复
/// </summary>
[ExportCodeFixProvider(
    LanguageNames.CSharp,
    Name = nameof(InjectionFailureCallbackCodeFixProvider)
)]
[Shared]
public sealed class InjectionFailureCallbackCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("GDI_U004"); // MissingInjectionFailureCallbackImplementation

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

        // 注册代码修复
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

    private static string ExtractMethodNameFromMessage(string message)
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

    private async Task<Document> AddFailureCallbackImplementationAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        string methodName,
        CancellationToken cancellationToken
    )
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // 创建 partial 方法实现
        var method = CreateFailureCallbackMethod(methodName);

        // 找到合适的插入位置（在类的最后一个成员之后）
        var newClassDeclaration = classDeclaration.AddMembers(method);

        // 替换旧的类声明
        var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

        return document.WithSyntaxRoot(newRoot);
    }

    private static MethodDeclarationSyntax CreateFailureCallbackMethod(string methodName)
    {
        // 创建方法体
        var statements = SyntaxFactory.List(
            new StatementSyntax[]
            {
                // GD.PushError($"Injection failed for {memberName}: {error}");
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("GD"),
                            SyntaxFactory.IdentifierName("PushError")
                        )
                    )
                        .WithArgumentList(
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("error"))
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
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
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
}
