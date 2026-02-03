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
    public static void GenerateInterface(
        SourceProductionContext context,
        ScopeNode node,
        DiGraph graph
    )
    {
        var f = new CodeFormatter();

        f.BeginClassDeclaration(node.ValidatedTypeInfo, out var className);
        {
            Generate(f, node, graph);
        }
        f.EndClassDeclaration();

        context.AddSource($"{className}.DI.IScope.g.cs", f.ToString());
    }

    private static void Generate(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        GenerateProvideService(f);
        f.AppendLine();

        GenerateResolveDependency(f);
        f.AppendLine();
    }

    private static void GenerateProvideService(CodeFormatter f)
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

                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PushError($\"直到根 Service Scope 都无法注册服务类型：{{type.Name}}\");"
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (!_services.TryAdd(type, instance))", "注册服务");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PushError($\"重复注册类型: {{type.Name}}。\");"
                );
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (_waiters.Remove(type, out var waiterList))", "通知等待者");
            f.BeginBlock();
            {
                f.AppendLine("foreach (var callback in waiterList)");
                f.BeginBlock();
                {
                    f.BeginTryCatch();
                    {
                        f.AppendLine("callback.Invoke(instance);");
                    }
                    f.EndTryCatch();
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }

    private static void GenerateResolveDependency(CodeFormatter f)
    {
        // ResolveDependency
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine(
            $"void {GlobalNames.IScope}.ResolveDependency<T>({GlobalNames.Action}<T> onResolved)"
        );
        f.BeginBlock();
        {
            f.AppendLine("var type = typeof(T);");
            f.AppendLine();

            f.AppendLine("if (!ServiceTypes.Contains(type))", "检查是否是单例服务类型");
            f.BeginBlock();
            {
                f.AppendLine("var parent = GetParentScope();", "尝试从父 Scope 解析");
                f.AppendLine("if (parent is not null)");
                f.BeginBlock();
                {
                    f.AppendLine("parent.ResolveDependency(onResolved);");
                    f.AppendLine("return;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine(
                    $"{GlobalNames.GodotGD}.PushError($\"直到根 Service Scope 都无法找到服务类型：{{type.Name}}\");"
                );
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
                f.EndTryCatch();
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("if (!_waiters.TryGetValue(type, out var waiterList))", "加入等待列表");
            f.BeginBlock();
            {
                f.AppendLine(
                    $"waiterList = new {GlobalNames.List}<{GlobalNames.Action}<{GlobalNames.Object}>>();"
                );
                f.AppendLine("_waiters[type] = waiterList;");
            }
            f.EndBlock();
            f.AppendLine("waiterList.Add(obj => onResolved.Invoke((T)obj));");
        }
        f.EndBlock();
    }
}
