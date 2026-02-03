using GodotSharpDI.SourceGenerator.Internal.Data;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Coding;

/// <summary>
/// HostAndUser 代码生成器
/// 生成同时具有 Host 和 User 特性的类型代码
/// </summary>
internal static class HostAndUserGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 生成基础 DI 文件（包含 Node DI 代码）
        NodeLifeCycleGenerator.Generate(context, node.ValidatedTypeInfo);

        // 生成 Host 特定代码
        HostGenerator.GenerateHostSpecific(context, node);

        // 生成 User 特定代码
        UserGenerator.GenerateUserSpecific(context, node);
    }
}
