using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using TypeInfo = GodotSharp.DI.Generator.Internal.Data.TypeInfo;

namespace GodotSharp.DI.Generator.Internal.Coding;

/// <summary>
/// HostAndUser 代码生成器
/// 生成同时具有 Host 和 User 特性的类型代码
/// </summary>
internal static class HostAndUserGenerator
{
    public static void Generate(SourceProductionContext context, TypeNode node)
    {
        // 生成基础 DI 文件（包含 Node DI 代码）
        NodeDIGenerator.GenerateBaseDI(context, node);

        // 生成 Host 特定代码
        HostGenerator.GenerateHostSpecific(context, node);

        // 生成 User 特定代码
        UserGenerator.GenerateUserSpecific(context, node);
    }
}
