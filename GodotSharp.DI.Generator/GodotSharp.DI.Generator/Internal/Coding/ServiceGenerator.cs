// using System.Text;
// using GodotSharp.DI.Generator.Internal.Data;
// using GodotSharp.DI.Generator.Internal.Helpers;
// using GodotSharp.DI.Shared;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.Text;
//
// namespace GodotSharp.DI.Generator.Internal.Coding;
//
// internal static class ServiceGenerator
// {
//     private static string FormatType(ITypeSymbol type)
//     {
//         return type.ToDisplayString(DisplayFormats.TypeFullQualified);
//     }
//
//     private static string FormatClassName(ITypeSymbol type)
//     {
//         return type.ToDisplayString(DisplayFormats.ClassName);
//     }
//
//     public static void Generate(SourceProductionContext context, DiGraph graph)
//     {
//         foreach (var service in graph.ServiceNodes)
//         {
//             var source = GenerateFactorySource(service);
//             var hintName = $"{service.TypeInfo.Symbol.Name}.DI.Factory.g.cs";
//             context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
//         }
//     }
//
//     private static string GenerateFactorySource(TypeNode info)
//     {
//         var f = new CodeFormatter();
//
//         SourceGenHelper.AppendFileHeader(f);
//
//         f.AppendLine($"namespace {info.Namespace};");
//         f.AppendLine();
//         f.AppendLine($"partial class {FormatClassName(info.TypeInfo.Symbol)}");
//         f.BeginBlock();
//         {
//             // ---------------------------
//             // CreateService 方法签名
//             // ---------------------------
//             f.AppendLine(
//                 info.TypeInfo.Lifetime == ServiceLifetime.Singleton
//                     ? $"public static void CreateService({TypeNamesGlobal.ScopeInterface} scope, global::System.Action<global::System.Object, {TypeNamesGlobal.ScopeInterface}> onCreated)"
//                     : $"public static void CreateService({TypeNamesGlobal.ScopeInterface} scope, global::System.Action<global::System.Object> onCreated)"
//             );
//             f.BeginBlock();
//             {
//                 var ctor = info.TypeInfo.Constructor;
//                 var paramCount = ctor.Parameters.Length;
//
//                 // ---------------------------
//                 // 无参构造
//                 // ---------------------------
//                 if (paramCount == 0)
//                 {
//                     f.AppendLine($"var instance = new {FormatType(info.TypeInfo.Symbol)}();");
//                     f.AppendLine(
//                         info.TypeInfo.Lifetime == ServiceLifetime.Singleton
//                             ? "onCreated.Invoke(instance, scope);"
//                             : "onCreated.Invoke(instance);"
//                     );
//                 }
//                 // ---------------------------
//                 // 多参数构造函数
//                 // ---------------------------
//                 else
//                 {
//                     // 剩余依赖计数
//                     f.AppendLine($"var remaining = {paramCount};");
//                     f.AppendLine();
//
//                     // 临时变量
//                     for (int i = 0; i < paramCount; i++)
//                     {
//                         var pType = FormatType(ctor.Parameters[i].Symbol);
//                         f.AppendLine($"{pType}? p{i} = default;");
//                     }
//                     f.AppendLine();
//
//                     // ResolveDependency 调用
//                     for (int i = 0; i < paramCount; i++)
//                     {
//                         var pType = FormatType(ctor.Parameters[i].Symbol);
//
//                         f.AppendLine($"scope.ResolveDependency<{pType}>(dep =>");
//                         f.BeginBlock();
//                         {
//                             f.AppendLine($"p{i} = dep;");
//                             f.AppendLine("TryCreate();");
//                         }
//                         f.EndBlock(");");
//                     }
//
//                     f.AppendLine();
//                     f.AppendLine("return;");
//                     f.AppendLine();
//
//                     // ---------------------------
//                     // Create() 方法
//                     // ---------------------------
//                     f.AppendLine("void TryCreate()");
//                     f.BeginBlock();
//                     {
//                         f.AppendLine("if (--remaining == 0)");
//                         f.BeginBlock();
//                         {
//                             f.AppendRaw(
//                                 $"var instance = new {FormatType(info.TypeInfo.Symbol)}(",
//                                 indent: true
//                             );
//                             for (int i = 0; i < paramCount; i++)
//                             {
//                                 if (i > 0)
//                                     f.AppendRaw(", ");
//                                 f.AppendRaw($"p{i}!");
//                             }
//                             f.AppendRaw(");");
//                             f.AppendLine();
//                             f.AppendLine(
//                                 info.TypeInfo.Lifetime == ServiceLifetime.Singleton
//                                     ? "onCreated.Invoke(instance, scope);"
//                                     : "onCreated.Invoke(instance);"
//                             );
//                         }
//                         f.EndBlock();
//                     }
//                     f.EndBlock();
//                 }
//             }
//             f.EndBlock();
//         }
//         f.EndBlock();
//
//         return f.ToString();
//     }
// }
