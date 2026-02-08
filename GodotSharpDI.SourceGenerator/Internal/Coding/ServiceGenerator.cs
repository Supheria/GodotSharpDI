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

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var fileName);
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
                GenerateParameterizedFactory(
                    f,
                    node.ValidatedTypeInfo,
                    node.ValidatedTypeInfo.Constructor
                );
            }
        }
        f.EndClassDeclaration();

        context.AddSource($"{fileName}.DI.Service.g.cs", f.ToString());
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
                f.AppendLine(
                    $"var errorMessage = $\"单例服务 '{validatedType.Symbol.Name}' 实例化失败。异常: {{ex.Message}}\";"
                );
                f.AppendLine(
                    $"scope.ProvideService<{validatedType.Symbol.ToFullyQualifiedName()}>(null, errorMessage);"
                );
            }
            f.EndTryCatch();
        }
        f.EndBlock();
    }

    private static void GenerateParameterizedFactory(
        CodeFormatter f,
        ValidatedTypeInfo validatedType,
        ConstructorInfo ctor
    )
    {
        // CreateService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"public static void CreateService("
                + $"{GlobalNames.IScope} scope, "
                + $"{GlobalNames.Action}<{GlobalNames.Object}> onCreated, "
                + $"{GlobalNames.String}? dependencyChain = null)"
        );
        f.BeginBlock();
        {
            f.AppendLine($"var remaining = {ctor.Parameters.Length};");
            f.AppendLine("var hasFailed = false;");
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
                var paramName = param.Symbol.Name;

                f.AppendLine($"scope.ResolveDependency<{param.Type.ToFullyQualifiedName()}>(");
                f.BeginLevel();
                {
                    f.AppendLine("dependency =>");
                    f.BeginBlock();
                    {
                        f.BeginTryCatch();
                        {
                            f.AppendLine($"p{i} = dependency;");
                            f.AppendLine("TryCreate();");
                        }
                        f.CatchBlock("ex");
                        {
                            f.AppendLine(
                                $"var errorMessage = $\"单例服务 '{validatedType.Symbol.Name}' 无法提供服务。 参数 ‘{paramName}’ 异常: {{ex.Message}}\";"
                            );
                            f.AppendLine("TryCreate(errorMessage);");
                        }
                        f.EndTryCatch();
                    }
                    f.EndBlock(",");
                    f.AppendLine($"requestorType: \"{validatedType.Symbol.Name}\",");
                    f.AppendLine("scopeChain: null,");
                    f.AppendLine("dependencyChain: dependencyChain");
                }
                f.EndLevel();
                f.AppendLine(");");
            }

            f.AppendLine();
            f.AppendLine("return;");
            f.AppendLine();

            // TryCreate
            f.AppendLine($"void TryCreate({GlobalNames.String}? errorMessage = null)");
            f.BeginBlock();
            {
                f.AppendLine("if (hasFailed) return;");
                f.AppendLine("if (errorMessage is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("hasFailed = true;");
                    f.AppendLine(
                        $"scope.ProvideService<{validatedType.Symbol.ToFullyQualifiedName()}>(null, errorMessage);"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
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
                            $"var instance = new {validatedType.Symbol.ToFullyQualifiedName()}({paramList});"
                        );
                        f.AppendLine();

                        // 提供服务实例（使用实现类型作为键）
                        f.AppendLine("// 提供服务实例");
                        f.AppendLine(
                            $"scope.ProvideService<{validatedType.Symbol.ToFullyQualifiedName()}>(instance);"
                        );
                        f.AppendLine();

                        f.AppendLine("onCreated.Invoke(instance);");
                    }
                    f.CatchBlock("ex");
                    {
                        f.AppendLine(
                            $"errorMessage = $\"单例服务 '{validatedType.Symbol.Name}' 实例化失败。异常: {{ex.Message}}\";"
                        );
                        f.AppendLine(
                            $"scope.ProvideService<{validatedType.Symbol.ToFullyQualifiedName()}>(null, errorMessage);"
                        );
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
