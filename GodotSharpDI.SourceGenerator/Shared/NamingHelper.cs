using System.Text;

namespace GodotSharpDI.SourceGenerator.Shared;

/// <summary>
/// 命名转换辅助类
/// </summary>
internal static class NamingHelper
{
    /// <summary>
    /// 将成员名转换为失败回调方法名
    /// 规则：
    /// 1. 忽略前导下划线
    /// 2. 将剩余部分转换为大写驼峰格式（去除中间的下划线）
    /// 3. 添加 "On" 前缀和 "InjectionFailed" 后缀
    /// </summary>
    /// <param name="memberName">成员名，如 "_myField", "my_service", "MyProperty"</param>
    /// <returns>失败回调方法名，如 "OnMyFieldInjectionFailed"</returns>
    public static string GetFailureCallbackMethodName(string memberName)
    {
        if (string.IsNullOrEmpty(memberName))
            return "OnInjectionFailed";

        // 去除前导下划线
        int startIndex = 0;
        while (startIndex < memberName.Length && memberName[startIndex] == '_')
        {
            startIndex++;
        }

        if (startIndex >= memberName.Length)
            return "OnInjectionFailed";

        var sb = new StringBuilder("On");

        // 转换为大写驼峰格式，去除下划线
        bool capitalizeNext = true; // 第一个字符大写
        for (int i = startIndex; i < memberName.Length; i++)
        {
            char c = memberName[i];

            if (c == '_')
            {
                // 遇到下划线，下一个字符需要大写
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpper(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }

        sb.Append("InjectionFailed");
        return sb.ToString();
    }
}
