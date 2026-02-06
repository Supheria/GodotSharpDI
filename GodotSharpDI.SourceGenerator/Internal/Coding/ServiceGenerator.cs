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
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            if (
                node.ValidatedTypeInfo.Constructor == null
                || node.ValidatedTypeInfo.Constructor.Parameters.IsEmpty
            )
            {
                GenerateParameterlessFactory(f, node.ValidatedTypeInfo);
            }
            else
            {
                GenerateParameterizedFactory(f, node, node.ValidatedTypeInfo.Constructor);
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.Service.g.cs", f.ToString());
    }

    private static void GenerateParameterlessFactory(
        CodeFormatter f,
        ValidatedTypeInfo validatedType
    )
    {
        // CreateService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"public static void CreateService("
                + $"{GlobalNames.IScope} scope, "
                + $"{GlobalNames.Action}<{GlobalNames.Object}> onCreated, "
                + $"{GlobalNames.Action}<{GlobalNames.String}> onFailed, "
                + $"{GlobalNames.String}? dependencyChain = null)"
        );
        f.BeginBlock();
        {
            f.BeginTryCatch();
            {
                f.AppendLine(
                    $"var instance = new {validatedType.Symbol.ToFullyQualifiedName()}();"
                );
                f.AppendLine();

                f.AppendLine("// 提供服务实例");
                f.AppendLine(
                    $"scope.ProvideService<{validatedType.Symbol.ToFullyQualifiedName()}>(instance);"
                );
                f.AppendLine();

                f.AppendLine("onCreated.Invoke(instance);");
            }
            f.CatchBlock("ex");
            {
                f.AppendLine("onFailed.Invoke(ex.Message);");
            }
            f.EndTryCatch();
        }
        f.EndBlock();
    }

    private static void GenerateParameterizedFactory(
        CodeFormatter f,
        TypeNode typeNode,
        ConstructorInfo ctor
    )
    {
        // CreateService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"public static void CreateService("
                + $"{GlobalNames.IScope} scope, "
                + $"{GlobalNames.Action}<{GlobalNames.Object}> onCreated, "
                + $"{GlobalNames.Action}<{GlobalNames.String}> onFailed, "
                + $"{GlobalNames.String}? dependencyChain = null)"
        );
        f.BeginBlock();
        {
            f.AppendLine($"var remaining = {ctor.Parameters.Length};");
            f.AppendLine($"var hasFailed = false;");
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
                        f.AppendLine("if (hasFailed) return;");
                        f.AppendLine();
                        f.BeginTryCatch();
                        {
                            f.AppendLine($"p{i} = dependency;");
                        }
                        f.CatchBlock("ex");
                        {
                            f.AppendLine("hasFailed = true;");
                            f.AppendLine(
                                $"PushError(ex.Message, \"{param.Symbol.Name}\", \"{param.Type}\");"
                            );
                        }
                        f.EndTryCatch();
                        f.AppendLine("TryCreate();");
                    }
                    f.EndBlock(",");
                    f.AppendLine($"requestorType: \"{typeNode.ValidatedTypeInfo.Symbol.Name}\",");
                    f.AppendLine("scopeChain: null,");
                    f.AppendLine("dependencyChain: dependencyChain");
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
                    f.StringBuilderAppendLine(
                        $"  服务类型: {typeNode.ValidatedTypeInfo.Symbol.Name}"
                    );
                    f.StringBuilderAppendLine("  参数名: {paramName}");
                    f.StringBuilderAppendLine("  参数类型: {paramType}");
                    f.StringBuilderAppendLine("  异常: {exMsg}");
                }
                f.EndStringBuilderAppend();
                f.AppendLine();
                f.AppendLine("onFailed(errorMessage.ToString());");
            }
            f.EndBlock();
            f.AppendLine();

            // TryCreate
            f.AppendLine("void TryCreate()");
            f.BeginBlock();
            {
                f.AppendLine("if (hasFailed) return;");
                f.AppendLine("if (--remaining == 0)");
                f.BeginBlock();
                {
                    f.BeginTryCatch();
                    {
                        var paramNames = new List<string>();
                        for (int i = 0; i < ctor.Parameters.Length; i++)
                        {
                            paramNames.Add($"p{i}!");
                        }
                        var paramList = string.Join(", ", paramNames);
                        f.AppendLine(
                            $"var instance = new {typeNode.ValidatedTypeInfo.Symbol.ToFullyQualifiedName()}({paramList});"
                        );
                        f.AppendLine();

                        // 提供服务实例（使用实现类型作为键）
                        f.AppendLine("// 提供服务实例");
                        f.AppendLine(
                            $"scope.ProvideService<{typeNode.ValidatedTypeInfo.Symbol.ToFullyQualifiedName()}>(instance);"
                        );
                        f.AppendLine();

                        f.AppendLine("onCreated.Invoke(instance);");
                    }
                    f.CatchBlock("ex");
                    {
                        f.AppendLine("hasFailed = true;");
                        f.BeginStringBuilderAppend("errorMessage", true);
                        {
                            f.StringBuilderAppendLine("[GodotSharpDI] 服务实例化失败");
                            f.StringBuilderAppendLine(
                                $"  服务类型: {typeNode.ValidatedTypeInfo.Symbol.Name}"
                            );
                            f.StringBuilderAppendLine("  异常: {ex.Message}");
                        }
                        f.EndStringBuilderAppend();
                        f.AppendLine();

                        f.AppendLine("onFailed(errorMessage.ToString());");
                    }
                    f.EndTryCatch();
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }
}
