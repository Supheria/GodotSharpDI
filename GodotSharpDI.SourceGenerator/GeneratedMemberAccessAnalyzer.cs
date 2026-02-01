using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSharpDI.SourceGenerator;

/// <summary>
/// 分析器：检测对框架生成的成员（方法、字段、属性）的手动访问
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GeneratedMemberAccessAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// 禁止手动调用的方法名称列表
    /// </summary>
    private static readonly ImmutableHashSet<string> ForbiddenMethodNames = ImmutableHashSet.Create(
        // Service 工厂方法
        "CreateService",
        // Node DI 生成的私有方法
        "GetServiceScope",
        "AttachToScope",
        "UnattachToScope",
        // User 生成的私有方法
        "ResolveUserDependencies",
        "OnDependencyResolved",
        // Host 生成的私有方法
        "AttachHostServices",
        "UnattachHostServices",
        // Scope 生成的私有方法
        "GetParentScope",
        "InstantiateScopeSingletons",
        "DisposeScopeSingletons",
        "CheckWaitList",
        // Scope 实现的IScope方法
        "ResolveDependency",
        "RegisterService",
        "UnregisterService"
    );

    /// <summary>
    /// 禁止手动访问的字段名称列表
    /// </summary>
    private static readonly ImmutableHashSet<string> ForbiddenFieldNames = ImmutableHashSet.Create(
        // Node DI 生成的字段
        "_serviceScope",
        // Scope 生成的字段
        "ServiceTypes",
        "_parentScope",
        "_services",
        "_waiters",
        "_disposableSingletons",
        // User IServicesReady 生成的字段
        "_unresolvedDependencies"
    );

    /// <summary>
    /// 禁止手动访问的属性名称列表（当前为空，预留扩展）
    /// </summary>
    private static readonly ImmutableHashSet<string> ForbiddenPropertyNames =
        ImmutableHashSet<string>.Empty;

    /// <summary>
    /// IScope 的完全限定名称
    /// </summary>
    private const string IScopeFullName = "GodotSharpDI.Abstractions.IScope";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ManualCallGeneratedMethod,
            DiagnosticDescriptors.ManualAccessGeneratedField,
            DiagnosticDescriptors.ManualAccessGeneratedProperty
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 注册语法节点分析
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeMemberAccess,
            SyntaxKind.SimpleMemberAccessExpression
        );
        context.RegisterSyntaxNodeAction(AnalyzeIdentifierName, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // 获取被调用的方法符号
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        // 检查方法名是否在禁止列表中
        if (!ForbiddenMethodNames.Contains(methodSymbol.Name))
            return;

        // 检查调用位置是否在生成的代码区域中
        if (IsInGeneratedCodeRegion(invocation))
            return;

        // 检查是否是对生成方法的调用
        if (!IsGeneratedMethodCall(methodSymbol, context.SemanticModel))
            return;

        // 获取调用表达式（this.Method() 或 obj.Method()）
        string calledOn = GetCalledOnExpression(invocation);

        // 报告诊断
        var diagnostic = Diagnostic.Create(
            DiagnosticDescriptors.ManualCallGeneratedMethod,
            invocation.GetLocation(),
            methodSymbol.Name,
            calledOn
        );

        context.ReportDiagnostic(diagnostic);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // 获取被访问的成员符号
        var symbolInfo = context.SemanticModel.GetSymbolInfo(
            memberAccess,
            context.CancellationToken
        );
        if (symbolInfo.Symbol is null)
            return;

        // 检查是否是方法调用（由 AnalyzeInvocation 处理）
        if (symbolInfo.Symbol is IMethodSymbol)
            return;

        AnalyzeMemberSymbol(
            context,
            symbolInfo.Symbol,
            memberAccess.GetLocation(),
            memberAccess.Expression.ToString()
        );
    }

    private static void AnalyzeIdentifierName(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;

        // 如果是成员访问表达式的右侧（Name 部分），跳过（由 AnalyzeMemberAccess 处理）
        // 例如：obj.Property 中的 Property
        if (
            identifier.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == identifier
        )
            return;

        // 如果是调用表达式，跳过（由 AnalyzeInvocation 处理）
        if (identifier.Parent is InvocationExpressionSyntax)
            return;

        // 获取符号
        var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken);
        if (symbolInfo.Symbol is null)
            return;

        // 检查是否是方法（由 AnalyzeInvocation 处理）
        if (symbolInfo.Symbol is IMethodSymbol)
            return;

        // 确定访问表达式
        string accessedOn = "this";

        // 如果标识符是成员访问表达式的左侧（Expression 部分）
        // 例如：ServiceTypes.Count 中的 ServiceTypes
        if (identifier.Parent is MemberAccessExpressionSyntax ma && ma.Expression == identifier)
        {
            // 这种情况下，accessedOn 保持为 "this"，因为 ServiceTypes 是当前类的成员
            accessedOn = "this";
        }

        AnalyzeMemberSymbol(context, symbolInfo.Symbol, identifier.GetLocation(), accessedOn);
    }

    private static void AnalyzeMemberSymbol(
        SyntaxNodeAnalysisContext context,
        ISymbol symbol,
        Location location,
        string accessedOn
    )
    {
        // 检查是否在生成的代码区域中
        if (IsInGeneratedCodeRegion(context.Node))
            return;

        DiagnosticDescriptor? descriptor = null;
        string memberName = symbol.Name;

        // 检查字段访问
        if (symbol is IFieldSymbol fieldSymbol)
        {
            if (!ForbiddenFieldNames.Contains(fieldSymbol.Name))
                return;

            // 检查字段是否真的是生成的字段（通过检查其语法）
            if (!IsGeneratedField(fieldSymbol))
                return;

            descriptor = DiagnosticDescriptors.ManualAccessGeneratedField;
        }
        // 检查属性访问
        else if (symbol is IPropertySymbol propertySymbol)
        {
            if (!ForbiddenPropertyNames.Contains(propertySymbol.Name))
                return;

            // 检查属性定义是否在生成的文件中
            var propertyLocation = propertySymbol.Locations.FirstOrDefault();
            if (propertyLocation == null || !IsGeneratedFile(propertyLocation))
                return;

            descriptor = DiagnosticDescriptors.ManualAccessGeneratedProperty;
        }
        else
        {
            return;
        }

        // 报告诊断
        var diagnostic = Diagnostic.Create(descriptor, location, memberName, accessedOn);

        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// 判断字段是否是生成的字段
    /// </summary>
    private static bool IsGeneratedField(IFieldSymbol fieldSymbol)
    {
        // 方法1: 检查字段定义位置
        var fieldLocation = fieldSymbol.Locations.FirstOrDefault();
        if (fieldLocation != null && IsGeneratedFile(fieldLocation))
        {
            return true;
        }

        // 方法2: 检查字段的声明语法
        // 生成的字段通常有特定的模式，例如私有、特定类型等
        foreach (var declaringSyntax in fieldSymbol.DeclaringSyntaxReferences)
        {
            var syntax = declaringSyntax.GetSyntax();

            // 检查是否在 partial class 的另一部分中
            if (syntax is VariableDeclaratorSyntax declarator)
            {
                var fieldDecl = declarator.Parent?.Parent as FieldDeclarationSyntax;
                if (fieldDecl != null)
                {
                    // 检查是否在生成的 partial class 中
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

    /// <summary>
    /// 判断 partial class 声明是否是生成的
    /// </summary>
    private static bool IsGeneratedPartialClass(ClassDeclarationSyntax classDecl)
    {
        // 检查是否在生成的文件中
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

        // 检查是否有 GeneratedCode 属性
        if (
            classDecl.AttributeLists.Any(attrList =>
                attrList.Attributes.Any(attr => attr.Name.ToString().Contains("GeneratedCode"))
            )
        )
        {
            return true;
        }

        // 检查是否只有字段声明，没有其他成员（这是生成的 partial class 的典型特征）
        var members = classDecl.Members;
        if (members.Count > 0 && members.All(m => m is FieldDeclarationSyntax))
        {
            // 进一步检查：生成的字段通常都是私有的
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

    /// <summary>
    /// 判断语法节点是否在生成的代码区域中
    /// </summary>
    private static bool IsInGeneratedCodeRegion(SyntaxNode node)
    {
        // 检查节点所在的文件
        if (IsGeneratedFile(node.GetLocation()))
        {
            return true;
        }

        // 检查节点是否在生成的 partial class 内部
        var containingClass = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass != null && IsGeneratedPartialClass(containingClass))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 判断方法调用是否指向生成的方法
    /// </summary>
    private static bool IsGeneratedMethodCall(
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel
    )
    {
        // 情况1: 直接调用生成的私有方法
        var methodLocation = methodSymbol.Locations.FirstOrDefault();
        if (methodLocation != null && IsGeneratedFile(methodLocation))
        {
            return true;
        }

        // 情况2: 检查方法声明是否在生成的 partial class 中
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

        // 情况3: 通过接口调用生成的实现方法
        if (methodSymbol.ContainingType != null)
        {
            var containingType = methodSymbol.ContainingType;

            // 如果是接口方法，查找实现该接口的类型
            if (containingType.TypeKind == TypeKind.Interface)
            {
                // 对于接口方法，我们需要检查是否是 IScope 的方法
                if (IsIScopeMethod(containingType, methodSymbol.Name))
                {
                    // 禁止所有对 IScope 方法的显式调用
                    return true;
                }
            }
            else
            {
                // 如果是类方法，检查该类是否实现了 IScope
                if (ImplementsIScope(containingType))
                {
                    // 检查该方法是否是显式接口实现
                    if (IsExplicitInterfaceImplementation(methodSymbol))
                    {
                        return true;
                    }

                    // 检查是否在生成的文件中定义
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

    /// <summary>
    /// 判断类型是否实现了 IScope 接口
    /// </summary>
    private static bool ImplementsIScope(ITypeSymbol type)
    {
        return type.AllInterfaces.Any(i => i.ToDisplayString() == IScopeFullName);
    }

    /// <summary>
    /// 判断方法是否是 IScope 接口的方法
    /// </summary>
    private static bool IsIScopeMethod(ITypeSymbol interfaceType, string methodName)
    {
        if (interfaceType.ToDisplayString() != IScopeFullName)
            return false;

        return methodName == "ResolveDependency"
            || methodName == "RegisterService"
            || methodName == "UnregisterService";
    }

    /// <summary>
    /// 判断方法是否是显式接口实现
    /// </summary>
    private static bool IsExplicitInterfaceImplementation(IMethodSymbol method)
    {
        return method.ExplicitInterfaceImplementations.Length > 0;
    }

    /// <summary>
    /// 判断位置是否在生成的文件中
    /// </summary>
    private static bool IsGeneratedFile(Location location)
    {
        var filePath = location.SourceTree?.FilePath;
        if (string.IsNullOrEmpty(filePath))
            return false;

        return filePath.Contains(".DI.g.cs")
            || filePath.Contains(".DI.") && filePath.EndsWith(".g.cs");
    }

    /// <summary>
    /// 获取方法调用的对象表达式
    /// </summary>
    private static string GetCalledOnExpression(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression.ToString(),
            _ => "this",
        };
    }
}
