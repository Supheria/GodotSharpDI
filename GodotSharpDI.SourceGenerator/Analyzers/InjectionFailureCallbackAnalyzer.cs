using System;
using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSharpDI.SourceGenerator.Analyzers;

/// <summary>
/// 分析器：检测缺失的注入回调方法实现（FailureCallback 和 ReadyCallback）
/// 增强版：添加异常处理，防止分析器崩溃
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectionFailureCallbackAnalyzer : DiagnosticAnalyzer
{
    private const string InjectAttributeName = "GodotSharpDI.Abstractions.InjectAttribute";
    private const string UserAttributeName = "GodotSharpDI.Abstractions.UserAttribute";
    private const string HostAttributeName = "GodotSharpDI.Abstractions.HostAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingInjectionFailureCallbackImplementation,
            DiagnosticDescriptors.MissingInjectionReadyCallbackImplementation
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // 使用安全包装器注册分析
        context.RegisterSymbolAction(
            ctx => SafeAnalyze(ctx, AnalyzeNamedType),
            SymbolKind.NamedType
        );
    }

    /// <summary>
    /// 安全包装器：捕获分析过程中的异常，避免分析器崩溃
    /// </summary>
    private static void SafeAnalyze(
        SymbolAnalysisContext context,
        Action<SymbolAnalysisContext> analyze
    )
    {
        try
        {
            analyze(context);
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
            // 如果需要调试，可以在这里添加日志
        }
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        // 只检查标记了 [User] 的类
        if (!HasUserAttribute(typeSymbol))
            return;

        // 收集所有标记了 [Inject] 的成员
        var allInjectMembers = typeSymbol
            .GetMembers()
            .Where(m => m is IFieldSymbol or IPropertySymbol)
            .Where(m => HasInjectAttribute(m))
            .ToArray();

        if (allInjectMembers.Length == 0)
            return;

        // 收集类中所有的 partial 方法
        var partialMethods = typeSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsPartialDefinition || m.PartialImplementationPart != null)
            .ToArray();

        // 检查每个 Inject 成员
        foreach (var member in allInjectMembers)
        {
            try
            {
                // 检查 FailureCallback
                if (HasInjectWithFailureCallback(member))
                {
                    AnalyzeMemberFailureCallback(context, member, partialMethods, typeSymbol);
                }

                // 检查 ReadyCallback
                if (HasInjectWithReadyCallback(member))
                {
                    AnalyzeMemberReadyCallback(context, member, partialMethods, typeSymbol);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // 单个成员分析失败不应该影响其他成员
                // 静默忽略，继续处理下一个成员
            }
        }
    }

    /// <summary>
    /// 分析单个成员的失败回调实现
    /// </summary>
    private static void AnalyzeMemberFailureCallback(
        SymbolAnalysisContext context,
        ISymbol member,
        IMethodSymbol[] partialMethods,
        INamedTypeSymbol typeSymbol
    )
    {
        var expectedMethodName = NamingHelper.GetFailureCallbackMethodName(member.Name);

        // 检查是否存在对应的 partial 方法实现
        var hasImplementation = partialMethods.Any(m =>
        {
            try
            {
                // 必须有相同的方法名
                if (m.Name != expectedMethodName)
                    return false;

                // 必须有实现部分
                if (m.PartialImplementationPart == null)
                    return false;

                // 检查签名是否匹配: partial void OnXxxInjectionFailed(string error)
                if (m.ReturnsVoid && m.Parameters.Length == 1)
                {
                    var param = m.Parameters[0];
                    return param.Type.SpecialType == SpecialType.System_String;
                }

                return false;
            }
            catch
            {
                // 检查失败，保守处理：认为不匹配
                return false;
            }
        });

        if (!hasImplementation)
        {
            // 报告诊断 - 使用成员的位置
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MissingInjectionFailureCallbackImplementation,
                member.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                member.Name,
                expectedMethodName
            );

            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// 分析单个成员的就绪回调实现
    /// </summary>
    private static void AnalyzeMemberReadyCallback(
        SymbolAnalysisContext context,
        ISymbol member,
        IMethodSymbol[] partialMethods,
        INamedTypeSymbol typeSymbol
    )
    {
        var expectedMethodName = NamingHelper.GetReadyCallbackMethodName(member.Name);

        // 检查是否存在对应的 partial 方法实现
        var hasImplementation = partialMethods.Any(m =>
        {
            try
            {
                // 必须有相同的方法名
                if (m.Name != expectedMethodName)
                    return false;

                // 必须有实现部分
                if (m.PartialImplementationPart == null)
                    return false;

                // 检查签名是否匹配: partial void OnXxxInjectionReady()
                if (m.ReturnsVoid && m.Parameters.Length == 0)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                // 检查失败，保守处理：认为不匹配
                return false;
            }
        });

        if (!hasImplementation)
        {
            // 报告诊断 - 使用成员的位置
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MissingInjectionReadyCallbackImplementation,
                member.Locations.FirstOrDefault() ?? typeSymbol.Locations.FirstOrDefault(),
                member.Name,
                expectedMethodName
            );

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool HasUserAttribute(INamedTypeSymbol typeSymbol)
    {
        try
        {
            return typeSymbol
                .GetAttributes()
                .Any(attr =>
                {
                    try
                    {
                        var attrClass = attr.AttributeClass;
                        return attrClass != null
                            && (
                                attrClass.ToDisplayString() == UserAttributeName
                                || attrClass.ToDisplayString() == HostAttributeName
                            );
                    }
                    catch
                    {
                        return false;
                    }
                });
        }
        catch
        {
            return false;
        }
    }

    private static bool HasInjectAttribute(ISymbol member)
    {
        try
        {
            return member
                .GetAttributes()
                .Any(attr =>
                {
                    try
                    {
                        var attrClass = attr.AttributeClass;
                        return attrClass != null
                            && attrClass.ToDisplayString() == InjectAttributeName;
                    }
                    catch
                    {
                        return false;
                    }
                });
        }
        catch
        {
            return false;
        }
    }

    private static bool HasInjectWithFailureCallback(ISymbol member)
    {
        try
        {
            var injectAttr = member
                .GetAttributes()
                .FirstOrDefault(attr =>
                {
                    try
                    {
                        var attrClass = attr.AttributeClass;
                        return attrClass != null
                            && attrClass.ToDisplayString() == InjectAttributeName;
                    }
                    catch
                    {
                        return false;
                    }
                });

            if (injectAttr == null)
                return false;

            // 检查 FailureCallback 属性
            foreach (var namedArg in injectAttr.NamedArguments)
            {
                try
                {
                    if (namedArg.Key == "FailureCallback" && namedArg.Value.Value is bool value)
                    {
                        return value;
                    }
                }
                catch
                {
                    // 继续检查下一个参数
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasInjectWithReadyCallback(ISymbol member)
    {
        try
        {
            var injectAttr = member
                .GetAttributes()
                .FirstOrDefault(attr =>
                {
                    try
                    {
                        var attrClass = attr.AttributeClass;
                        return attrClass != null
                            && attrClass.ToDisplayString() == InjectAttributeName;
                    }
                    catch
                    {
                        return false;
                    }
                });

            if (injectAttr == null)
                return false;

            // 检查 ReadyCallback 属性
            foreach (var namedArg in injectAttr.NamedArguments)
            {
                try
                {
                    if (namedArg.Key == "ReadyCallback" && namedArg.Value.Value is bool value)
                    {
                        return value;
                    }
                }
                catch
                {
                    // 继续检查下一个参数
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
