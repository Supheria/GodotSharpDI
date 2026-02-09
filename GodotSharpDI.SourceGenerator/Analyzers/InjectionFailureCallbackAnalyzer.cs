using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GodotSharpDI.SourceGenerator.Analyzers;

/// <summary>
/// 分析器：检测缺失的注入失败回调方法实现
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InjectionFailureCallbackAnalyzer : DiagnosticAnalyzer
{
    private const string InjectAttributeName = "GodotSharpDI.Abstractions.InjectAttribute";
    private const string UserAttributeName = "GodotSharpDI.Abstractions.UserAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.MissingInjectionFailureCallbackImplementation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        // 只检查标记了 [User] 的类
        if (!HasUserAttribute(typeSymbol))
            return;

        // 收集所有标记了 [Inject(FailureCallback = true)] 的成员
        var membersWithCallback = typeSymbol
            .GetMembers()
            .Where(m => m is IFieldSymbol or IPropertySymbol)
            .Where(m => HasInjectWithFailureCallback(m))
            .ToArray();

        if (membersWithCallback.Length == 0)
            return;

        // 收集类中所有的 partial 方法
        var partialMethods = typeSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.IsPartialDefinition || m.PartialImplementationPart != null)
            .ToArray();

        // 检查每个需要回调的成员
        foreach (var member in membersWithCallback)
        {
            var expectedMethodName = NamingHelper.GetFailureCallbackMethodName(member.Name);

            // 检查是否存在对应的 partial 方法实现
            var hasImplementation = partialMethods.Any(m =>
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
    }

    private static bool HasUserAttribute(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes().Any(attr =>
        {
            var attrClass = attr.AttributeClass;
            return attrClass != null
                && attrClass.ToDisplayString() == UserAttributeName;
        });
    }

    private static bool HasInjectWithFailureCallback(ISymbol member)
    {
        var injectAttr = member
            .GetAttributes()
            .FirstOrDefault(attr =>
            {
                var attrClass = attr.AttributeClass;
                return attrClass != null
                    && attrClass.ToDisplayString() == InjectAttributeName;
            });

        if (injectAttr == null)
            return false;

        // 检查 FailureCallback 属性
        foreach (var namedArg in injectAttr.NamedArguments)
        {
            if (namedArg.Key == "FailureCallback" && namedArg.Value.Value is bool value)
            {
                return value;
            }
        }

        return false;
    }
}
