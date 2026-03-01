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
/// 分析器：检测对框架生成的成员（方法、字段、属性）的手动访问
/// 使用 CachedSymbols 优化符号查找
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
        "__parentScope",
        // Scope 生成的字段
        "ServiceTypes",
        "_services",
        "_waiters",
        "_disposableSingletons",
        // User IDependenciesResolved 生成的字段
        "_unresolvedDependencies"
    );

    /// <summary>
    /// 禁止手动访问的属性名称列表（当前为空，预留扩展）
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

        // 使用 CompilationStartAction 初始化 CachedSymbols
        context.RegisterCompilationStartAction(compilationContext =>
        {
            try
            {
                var cachedSymbols = new CachedSymbols(compilationContext.Compilation);

                // 如果 IScope 不存在，说明项目没有使用 GodotSharpDI
                if (cachedSymbols.IScope == null)
                    return;

                // 注册语法节点分析
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
                // 初始化失败，静默忽略
            }
        });
    }

    /// <summary>
    /// 安全包装器：捕获分析过程中的异常，避免分析器崩溃
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
            // 取消操作是正常的，不需要报告
            throw;
        }
        catch (Exception)
        {
            // 分析器不应该崩溃
            // 静默忽略错误，因为分析器失败不应该阻止编译
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
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
        if (!IsGeneratedMethodCall(methodSymbol, cachedSymbols, context.SemanticModel))
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

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
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

    private static void AnalyzeIdentifierName(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
    {
        var identifier = (IdentifierNameSyntax)context.Node;

        // 如果是成员访问表达式的右侧（Name 部分），跳过（由 AnalyzeMemberAccess 处理）
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
        if (identifier.Parent is MemberAccessExpressionSyntax ma && ma.Expression == identifier)
        {
            accessedOn = "this";
        }

        AnalyzeMemberSymbol(context, symbolInfo.Symbol, identifier.GetLocation(), accessedOn);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, CachedSymbols cachedSymbols)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // 检查是否在生成的代码区域中
        if (IsInGeneratedCodeRegion(assignment))
            return;

        // 获取赋值左侧的符号
        var symbolInfo = context.SemanticModel.GetSymbolInfo(
            assignment.Left,
            context.CancellationToken
        );
        if (symbolInfo.Symbol is not IFieldSymbol fieldSymbol)
            return;

        // 检查字段名是否匹配 IsXxxInjectionReady 模式
        if (!IsInjectionReadyFieldName(fieldSymbol.Name))
            return;

        // 检查字段是否真的是生成的字段
        if (!IsGeneratedField(fieldSymbol))
            return;

        // 获取访问表达式
        string accessedOn = "this";
        if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
        {
            accessedOn = memberAccess.Expression.ToString();
        }

        // 报告诊断
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

            // 检查字段是否真的是生成的字段
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

    private static bool IsGeneratedField(IFieldSymbol fieldSymbol)
    {
        try
        {
            // 方法1: 检查字段定义位置
            var fieldLocation = fieldSymbol.Locations.FirstOrDefault();
            if (fieldLocation != null && IsGeneratedFile(fieldLocation))
            {
                return true;
            }

            // 方法2: 检查字段的声明语法
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
            // 如果检查失败，保守处理：不报告诊断
            return false;
        }
    }

    private static bool IsGeneratedPartialClass(ClassDeclarationSyntax classDecl)
    {
        try
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

            // 检查是否只有字段声明
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

            return methodName == "ResolveDependency"
                || methodName == "RegisterService"
                || methodName == "UnregisterService";
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
            if (string.IsNullOrEmpty(filePath))
                return false;

            return filePath.Contains(".DI.g.cs")
                || filePath.Contains(".DI.") && filePath.EndsWith(".g.cs");
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
