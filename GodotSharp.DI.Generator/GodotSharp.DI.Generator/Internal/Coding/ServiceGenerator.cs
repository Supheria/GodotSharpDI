using System.Text;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator.Internal.Coding;

internal static class ServiceGenerator
{
    private static string FormatType(ITypeSymbol type)
    {
        return type.ToDisplayString(DisplayFormats.TypeFullQualified);
    }

    private static string FormatClassName(ITypeSymbol type)
    {
        return type.ToDisplayString(DisplayFormats.ClassName);
    }

    public static void Generate(SourceProductionContext context, DiGraph graph)
    {
        foreach (var service in graph.Services)
        {
            var source = GenerateFactorySource(service);
            var hintName = $"{service.Symbol.Name}.DI.Factory.g.cs";
            context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static string GenerateFactorySource(ClassTypeInfo info)
    {
        var f = new CodeFormatter();

        // ---------------------------
        // 文件头
        // ---------------------------
        f.AppendLine($"namespace {info.Namespace};");
        f.AppendLine();

        // ---------------------------
        // partial class
        // ---------------------------
        f.AppendLine($"partial class {FormatClassName(info.Symbol)}");
        f.BeginBlock();
        {
            f.AppendLine("#nullable enable");
            f.AppendLine();

            // ---------------------------
            // CreateService 方法签名
            // ---------------------------
            f.AppendLine(
                $"public static void CreateService({TypeNamesGlobal.ScopeInterface} scope, global::System.Action<global::System.Object> onCreated)"
            );
            f.BeginBlock();
            {
                var ctor = info.ServiceConstructor!;
                if (ctor.Parameters.Length < 1)
                {
                    // ---------------------------
                    // 无参构造
                    // ---------------------------
                    f.AppendLine($"var instance = new {FormatType(info.Symbol)}();");
                    f.AppendLine("onCreated.Invoke(instance);");
                }
                else
                {
                    // ---------------------------
                    // 多参数构造函数
                    // ---------------------------
                    var paramCount = ctor.Parameters.Length;

                    f.AppendLine($"var remaining = {paramCount};");
                    f.AppendLine();

                    // 临时变量
                    for (int i = 0; i < paramCount; i++)
                    {
                        var pType = FormatType(ctor.Parameters[i].Symbol);
                        f.AppendLine($"{pType}? p{i} = default;");
                    }
                    f.AppendLine();

                    // ResolveDependency 调用
                    for (int i = 0; i < paramCount; i++)
                    {
                        var pType = FormatType(ctor.Parameters[i].Symbol);

                        f.AppendLine($"scope.ResolveDependency<{pType}>(dependency =>");
                        f.BeginBlock();
                        {
                            f.AppendLine($"p{i} = dependency;");
                            f.AppendLine(
                                "if (global::System.Threading.Interlocked.Decrement(ref remaining) == 0)"
                            );
                            f.BeginBlock();
                            {
                                f.AppendLine("Create();");
                            }
                            f.EndBlock();
                        }
                        f.EndBlock(");");
                    }
                    f.AppendLine();

                    f.AppendLine("return;");
                    f.AppendLine();

                    // Create() 方法
                    f.AppendLine("void Create()");
                    f.BeginBlock();
                    {
                        f.AppendRaw($"var instance = new {FormatType(info.Symbol)}(", true);
                        for (int i = 0; i < paramCount; i++)
                        {
                            if (i > 0)
                                f.AppendRaw(", ");
                            f.AppendRaw($"p{i}!");
                        }
                        f.AppendRaw(");");
                        f.AppendLine();
                        f.AppendLine("onCreated.Invoke(instance);");
                    }
                    f.EndBlock();
                }
            }
            f.EndBlock();
        }
        f.EndBlock();

        return f.ToString();
    }
}
