using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Coding;

/// <summary>
/// Scope 生命周期代码生成器
/// </summary>
internal static class ScopeLifecycleGenerator
{
    public static void Generate(CodeFormatter f, ScopeNode node, DiGraph graph)
    {
        GenerateParentScopeField(f);
        f.AppendLine();

        GenerateGetParentScope(f);
        f.AppendLine();

        GenerateInstantiateScopeSingletons(f, node, graph);
        f.AppendLine();

        GenerateDisposeScopeSingletons(f);
        f.AppendLine();

        GenerateCheckWaitList(f);
        f.AppendLine();

        GenerateNotification(f);
    }

    private static void GenerateParentScopeField(CodeFormatter f)
    {
        f.AppendLine($"private {GlobalNames.IScope}? _parentScope;");
    }

    private static void GenerateGetParentScope(CodeFormatter f)
    {
        // GetParentScope
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine($"private {GlobalNames.IScope}? GetParentScope()");
        f.BeginBlock();
        {
            f.AppendLine("if (_parentScope is not null)");
            f.BeginBlock();
            {
                f.AppendLine("return _parentScope;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine("var parent = GetParent();");
            f.AppendLine("while (parent is not null)");
            f.BeginBlock();
            {
                f.AppendLine($"if (parent is {GlobalNames.IScope} scope)");
                f.BeginBlock();
                {
                    f.AppendLine("_parentScope = scope;");
                    f.AppendLine("return _parentScope;");
                }
                f.EndBlock();
                f.AppendLine("parent = parent.GetParent();");
            }
            f.EndBlock();

            f.AppendLine("return null;");
        }
        f.EndBlock();
    }

    private static void GenerateInstantiateScopeSingletons(
        CodeFormatter f,
        ScopeNode node,
        DiGraph graph
    )
    {
        // InstantiateScopeSingletons
        f.AppendHiddenMethodCommentAndAttribute("实例化所有 Scope 约束的单例服务");
        f.AppendLine("private void InstantiateScopeSingletons()");
        f.BeginBlock();
        {
            foreach (var serviceType in node.InstantiateServices)
            {
                if (!graph.ServiceNodeMap.TryGetValue(serviceType, out var serviceNode))
                {
                    continue;
                }

                var simpleServiceName = serviceType.Name;

                f.AppendLine($"{simpleServiceName}.CreateService(");
                f.BeginLevel();
                {
                    f.AppendLine("this,");
                    f.AppendLine("(instance, scope) =>");
                    f.BeginBlock();
                    {
                        f.AppendLine($"if (instance is {GlobalNames.IDisposable} disposable)");
                        f.BeginBlock();
                        {
                            f.AppendLine("_disposableSingletons.Add(disposable);");
                        }
                        f.EndBlock();

                        foreach (var exposedType in serviceNode.ProvidedServices)
                        {
                            f.AppendLine(
                                $"scope.RegisterService(({exposedType.ToDisplayString()})instance);"
                            );
                        }
                    }
                    f.EndBlock();
                }
                f.EndLevel();
                f.AppendLine(");");
            }
        }
        f.EndBlock();
    }

    private static void GenerateDisposeScopeSingletons(CodeFormatter f)
    {
        // DisposeScopeSingletons
        f.AppendHiddenMethodCommentAndAttribute("释放所有 Scope 约束的单例服务实例");
        f.AppendLine("private void DisposeScopeSingletons()");
        f.BeginBlock();
        {
            f.AppendLine("foreach (var disposable in _disposableSingletons)");
            f.BeginBlock();
            {
                f.BeginTryCatch();
                {
                    f.AppendLine("disposable.Dispose();");
                }
                f.EndTryCatch();
            }
            f.EndBlock();

            f.AppendLine("_disposableSingletons.Clear();");
            f.AppendLine("_services.Clear();");
        }
        f.EndBlock();
    }

    private static void GenerateCheckWaitList(CodeFormatter f)
    {
        // CheckWaitList
        f.AppendHiddenMethodCommentAndAttribute();
        f.AppendLine("private void CheckWaitList()");
        f.BeginBlock();
        {
            f.AppendLine("if (_waiters.Count == 0)");
            f.BeginBlock();
            {
                f.AppendLine("return;");
            }
            f.EndBlock();
            f.AppendLine();

            f.AppendLine($"var sb = new {GlobalNames.StringBuilder}();");
            f.AppendLine("var first = true;");
            f.AppendLine("foreach (var type in _waiters.Keys)");
            f.BeginBlock();
            {
                f.AppendLine("if (!first)");
                f.BeginBlock();
                {
                    f.AppendLine("sb.Append(',');");
                }
                f.EndBlock();
                f.AppendLine("sb.Append(type.Name);");
                f.AppendLine("first = false;");
            }
            f.EndBlock();

            f.AppendLine(
                $"{GlobalNames.GodotGD}.PushError($\"存在未完成注入的服务类型：{{sb}}\");"
            );
            f.AppendLine("_waiters.Clear();");
        }
        f.EndBlock();
    }

    private static void GenerateNotification(CodeFormatter f)
    {
        f.AppendLine("public override void _Notification(int what)");
        f.BeginBlock();
        {
            f.AppendLine("base._Notification(what);");
            f.AppendLine();
            f.AppendLine("switch ((long)what)");
            f.BeginBlock();
            {
                f.AppendLine("case NotificationEnterTree:");
                f.AppendLine("case NotificationExitTree:");
                f.BeginBlock();
                {
                    f.AppendLine("_parentScope = null;");
                    f.AppendLine("break;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine("case NotificationReady:");
                f.BeginBlock();
                {
                    f.AppendLine("InstantiateScopeSingletons();");
                    f.AppendLine("CheckWaitList();");
                    f.AppendLine("break;");
                }
                f.EndBlock();
                f.AppendLine();

                f.AppendLine("case NotificationPredelete:");
                f.BeginBlock();
                {
                    f.AppendLine("DisposeScopeSingletons();");
                    f.AppendLine("break;");
                }
                f.EndBlock();
            }
            f.EndBlock();
        }
        f.EndBlock();
    }
}
