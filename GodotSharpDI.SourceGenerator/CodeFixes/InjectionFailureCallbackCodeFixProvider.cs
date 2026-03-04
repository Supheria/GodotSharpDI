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
                // 尝试从语义模型获取成员类型，用于生成正确的参数签名
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

                        // 去掉可空 ? 后缀
                        if (memberTypeName != null && memberTypeName.EndsWith("?"))
                            memberTypeName = memberTypeName.Substring(0, memberTypeName.Length - 1);
                    }
                }
                catch
                {
                    // 语义模型获取失败，回退到 object
                }

                // 注册就绪回调的代码修复
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
        string? memberTypeName,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (root == null)
                return document;

            // 创建 partial 方法实现
            var method = CreateReadyCallbackMethod(methodName, memberTypeName);

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

            // 创建方法：partial void OnXxxInjectionFailed() { ... }
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

    private static MethodDeclarationSyntax CreateReadyCallbackMethod(string methodName, string? memberTypeName)
    {
        try
        {
            // 确定参数类型：优先使用已知成员类型，否则回退到 object
            var typeSyntax = memberTypeName != null
                ? (TypeSyntax)SyntaxFactory.ParseTypeName(memberTypeName)
                : (TypeSyntax)SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword));

            // 参数名：方法名去掉 "On" 前缀和 "InjectionReady" 后缀，首字母小写
            var paramName = DeriveParmName(methodName);

            var parameter = SyntaxFactory
                .Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(typeSyntax.WithTrailingTrivia(SyntaxFactory.Space));

            // 创建方法体
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

            // 创建方法：partial void OnXxxInjectionReady(TypeA xxx) { ... }
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

    /// <summary>
    /// 从方法名 OnXxxInjectionReady 提取参数名（首字母小写的 Xxx 部分）
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
