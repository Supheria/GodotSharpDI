using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSharpDI.SourceGenerator;

/// <summary>
/// 分析器：检测对框架生成的私有方法的手动调用
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MethodCallAnalyzer : DiagnosticAnalyzer
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
    /// IScope 的完全限定名称
    /// </summary>
    private const string IScopeFullName = "GodotSharpDI.Abstractions.IScope";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ManualCallGeneratedMethod);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 注册语法节点分析
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
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

        // 检查调用位置是否在生成的文件中（如果在生成文件中调用，则允许）
        if (IsGeneratedFile(invocation.GetLocation()))
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

        // 情况2: 通过接口调用生成的实现方法
        // 检查方法所属的类型是否实现了 IScope
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
