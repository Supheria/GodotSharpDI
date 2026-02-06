using System.Collections.Generic;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// Scope 接口实现代码生成器
/// </summary>
internal static class ScopeInterfaceGenerator
{
    public static void GenerateInterface(SourceProductionContext context, ScopeNode node)
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            Generate(f, node);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.IScope.g.cs", f.ToString());
    }

    private static void Generate(CodeFormatter f, ScopeNode node)
    {
        GenerateProvideService(f, node.ValidatedTypeInfo);
        f.AppendLine();

        GenerateResolveDependency(f, node.ValidatedTypeInfo);
        f.AppendLine();
    }

    private static void GenerateProvideService(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        // ProvideService
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"void {GlobalNames.IScope}.ProvideService<T>(T instance)");
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("if (!ServiceTypes.Contains(type))", "检查是否是单例服务类型");
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试向父 Scope 注册");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.ProvideService(instance);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.PushError("$\"直到根 Service Scope 都无法注册服务类型：{type.Name}\"");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (!_services.TryAdd(type, instance))", "注册服务");
            f.BeginBlock();
            {
                f.PushError("$\"重复注册类型: {type.Name}\"");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (_waiters.Remove(type, out var waiterList))", "通知等待者");
            f.BeginBlock();
            {
                f.AppendLine("foreach (var waiter in waiterList)");
                f.BeginBlock();
                {
                    f.BeginTryCatch();
                    {
                        f.AppendLine("waiter.Callback.Invoke(instance);");
                    }
                    f.CatchBlock("ex");
                    {
                        f.BeginStringBuilderAppend("errorMessage", true);
                        {
                            f.StringBuilderAppendLine("[GodotSharpDI] 依赖注入回调执行失败");
                            f.StringBuilderAppendLine("  服务类型: {type.Name}");
                            f.StringBuilderAppendLine("  请求者类型: {waiter.RequestorType}");
                            f.StringBuilderAppendLine($"  当前 Scope: {validatedType.Symbol.Name}");
                            f.StringBuilderAppendLine("  Scope 传递链: {waiter.ScopeChain}");
                            f.StringBuilderAppendLine("  依赖链条: {waiter.DependencyChain}");
                            f.StringBuilderAppendLine("  异常: {ex.Message}");
                        }
                        f.EndStringBuilderAppend();
                        f.AppendLine();

                        f.PushError("errorMessage.ToString()");
                    }
                    f.EndTryCatch();
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateResolveDependency(CodeFormatter f, ValidatedTypeInfo validatedType)
    {
        // ResolveDependency
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"void {GlobalNames.IScope}.ResolveDependency<T>("
                + $"{GlobalNames.Action}<T> onResolved, "
                + $"{GlobalNames.String} requestorType, "
                + $"{GlobalNames.String}? scopeChain, "
                + $"{GlobalNames.String}? dependencyChain)"
        );
        f.BeginBlock();
        {
            f.AppendLine("// 构建 Scope 传递链");
            f.AppendLine(
                $"var currentScopeChain = scopeChain is null ? \"{validatedType.Symbol.Name}\" : scopeChain + \" -> {validatedType.Symbol.Name}\";"
            );

            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("// 构建依赖链条");
            f.AppendLine(
                "var currentDependencyChain = dependencyChain ?? $\"{requestorType} -> {type.Name}\";"
            );
            f.AppendLine();

            f.AppendLine("if (!ServiceTypes.Contains(type))", "检查是否是单例服务类型");
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试从父 Scope 解析");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine(
                        "parent.ResolveDependency(onResolved, requestorType, currentScopeChain, currentDependencyChain);"
                    );
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.BeginStringBuilderAppend("errorMessage", true);
                {
                    f.StringBuilderAppendLine(
                        "[GodotSharpDI] 直到根 Scope 都无法找到服务类型: {type.Name}"
                    );
                    f.StringBuilderAppendLine("  请求者类型: {requestorType}");
                    f.StringBuilderAppendLine($"  当前 Scope: {validatedType.Symbol.Name}");
                    f.StringBuilderAppendLine("  Scope 传递链: {currentScopeChain}");
                    f.StringBuilderAppendLine("  依赖链条: {currentDependencyChain}");
                }
                f.EndStringBuilderAppend();
                f.AppendLine();

                f.PushError("errorMessage.ToString()");
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine(
                "if (_services.TryGetValue(type, out var singleton))",
                "尝试从已注册的单例获取"
            );
            f.BeginBlock();
            {
                f.BeginTryCatch();
                {
                    f.AppendLine("onResolved.Invoke((T)singleton);");
                }
                f.CatchBlock("ex");
                {
                    f.BeginStringBuilderAppend("errorMessage", true);
                    {
                        f.StringBuilderAppendLine("[GodotSharpDI] 依赖注入回调执行失败");
                        f.StringBuilderAppendLine("  服务类型: {type.Name}");
                        f.StringBuilderAppendLine("  请求者类型: {requestorType}");
                        f.StringBuilderAppendLine($"  当前 Scope: {validatedType.Symbol.Name}");
                        f.StringBuilderAppendLine("  Scope 传递链: {currentScopeChain}");
                        f.StringBuilderAppendLine("  依赖链条: {currentDependencyChain}");
                        f.StringBuilderAppendLine("  异常: {ex.Message}");
                    }
                    f.EndStringBuilderAppend();
                    f.AppendLine();

                    f.PushError("errorMessage.ToString()");
                }
                f.EndTryCatch();
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (!_waiters.TryGetValue(type, out var waiterList))", "加入等待列表");
            f.BeginBlock();
            {
                f.AppendLine($"waiterList = new {GlobalNames.List}<DependencyWaitInfo>();");
                f.AppendLine("_waiters[type] = waiterList;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("waiterList.Add(new DependencyWaitInfo");
            f.BeginBlock();
            {
                f.AppendLine("Callback = obj => onResolved.Invoke((T)obj),");
                f.AppendLine($"RequestTicks = {GlobalNames.DateTime}.Now.Ticks,");
                f.AppendLine("RequestorType = requestorType,");
                f.AppendLine("ScopeChain = currentScopeChain,");
                f.AppendLine("DependencyChain = currentDependencyChain,");
            }
            f.EndBlock(");");
        }
        f.EndBlock();
    }
}
