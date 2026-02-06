using System.Collections.Generic;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Service 工厂代码生成器
/// </summary>
internal static class ServiceGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        var type = node.ValidatedTypeInfo;

        var f = new CodeFormatter();

        f.BeginClassDeclaration(type, out var className);
        {
            if (type.Constructor == null || type.Constructor.Parameters.IsEmpty)
            {
                GenerateParameterlessFactory(f, className);
            }
            else
            {
                GenerateParameterizedFactory(f, className, type.Constructor);
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Service.g.cs", f.ToString());
    }

    private static void GenerateParameterlessFactory(CodeFormatter f, string className)
    {
        // CreateService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"public static void CreateService({GlobalNames.IScope} scope, {GlobalNames.Action}<{GlobalNames.Object}, {GlobalNames.IScope}> onCreated)"
        );
        f.BeginBlock();
        {
            f.AppendLine($"var instance = new {className}();");
            f.AppendLine("onCreated.Invoke(instance, scope);");
        }
        f.EndBlock();
    }

    private static void GenerateParameterizedFactory(
        CodeFormatter f,
        string className,
        ConstructorInfo ctor
    )
    {
        // CreateService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"public static void CreateService({GlobalNames.IScope} scope, {GlobalNames.Action}<{GlobalNames.Object}, {GlobalNames.IScope}> onCreated)"
        );
        f.BeginBlock();
        {
            f.AppendLine($"var remaining = {ctor.Parameters.Length};");
            f.AppendLine();

            // 声明参数变量
            for (int i = 0; i < ctor.Parameters.Length; i++)
            {
                var param = ctor.Parameters[i];
                f.AppendLine($"{param.Type.ToFullyQualifiedName()}? p{i} = null;");
            }
            f.AppendLine();

            // 解析依赖
            for (int i = 0; i < ctor.Parameters.Length; i++)
            {
                var param = ctor.Parameters[i];

                f.AppendLine($"scope.ResolveDependency<{param.Type.ToFullyQualifiedName()}>(");
                f.BeginLevel();
                {
                    f.AppendLine("dependency =>");
                    f.BeginBlock();
                    {
                        f.BeginTryCatch();
                        {
                            f.AppendLine($"p{i} = dependency;");
                        }
                        f.CatchBlock("ex");
                        {
                            f.AppendLine(
                                $"PushError(ex.Message, \"{param.Symbol.Name}\", \"{param.Type}\");"
                            );
                        }
                        f.EndTryCatch();
                        f.AppendLine("TryCreate();");
                    }
                    f.EndBlock(",");
                    f.AppendLine($"requestorType: \"{className}\"");
                }
                f.EndLevel();
                f.AppendLine(");");
            }

            f.AppendLine();
            f.AppendLine("return;");
            f.AppendLine();

            // PushError
            f.AppendLine("void PushError(string exMsg, string paramName, string paramType)");
            f.BeginBlock();
            {
                f.BeginStringBuilderAppend("errorMessage", true);
                {
                    f.StringBuilderAppendLine("[GodotSharpDI] 依赖赋值失败");
                    f.StringBuilderAppendLine($"  服务类型: {className}");
                    f.StringBuilderAppendLine("  参数名: {paramName}");
                    f.StringBuilderAppendLine("  参数类型: {paramType}");
                    f.StringBuilderAppendLine("  异常: {exMsg}");
                }
                f.EndStringBuilderAppend();
                f.AppendLine();
                f.PushError("errorMessage.ToString()");
            }
            f.EndBlock();
            f.AppendLine();

            // TryCreate
            f.AppendLine("void TryCreate()");
            f.BeginBlock();
            {
                f.AppendLine("if (--remaining == 0)");
                f.BeginBlock();
                {
                    var paramNames = new List<string>();
                    for (int i = 0; i < ctor.Parameters.Length; i++)
                    {
                        paramNames.Add($"p{i}!");
                    }
                    var paramList = string.Join(", ", paramNames);
                    f.AppendLine($"var instance = new {className}({paramList});");
                    f.AppendLine("onCreated.Invoke(instance, scope);");
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }
}
