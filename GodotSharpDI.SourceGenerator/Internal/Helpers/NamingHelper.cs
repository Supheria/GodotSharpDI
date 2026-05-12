using System.Text;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// 命名转换辅助类
/// </summary>
internal static class NamingHelper
{
    /// <summary>
    /// 将成员名转换为大写驼峰格式
    /// 规则：
    /// 1. 忽略前导下划线
    /// 2. 将剩余部分转换为大写驼峰格式（去除中间的下划线）
    /// </summary>
    /// <param name="memberName">成员名，如 "_myField", "my_service", "MyProperty"</param>
    /// <returns>大写驼峰格式的名称，如 "MyField", "MyService", "MyProperty"</returns>
    public static string ToPascalCase(string memberName)
    {
        if (string.IsNullOrEmpty(memberName))
            return string.Empty;

        // 去除前导下划线
        int startIndex = 0;
        while (startIndex < memberName.Length && memberName[startIndex] == '_')
        {
            startIndex++;
        }

        if (startIndex >= memberName.Length)
            return string.Empty;

        var sb = new StringBuilder();

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

        return sb.ToString();
    }

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
        var pascalCase = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascalCase))
            return "OnInjectionFailed";

        return $"On{pascalCase}InjectionFailed";
    }

    /// <summary>
    /// 将成员名转换为就绪回调方法名
    /// 规则：
    /// 1. 忽略前导下划线
    /// 2. 将剩余部分转换为大写驼峰格式（去除中间的下划线）
    /// 3. 添加 "On" 前缀和 "InjectionReady" 后缀
    /// </summary>
    /// <param name="memberName">成员名，如 "_myField", "my_service", "MyProperty"</param>
    /// <returns>就绪回调方法名，如 "OnMyFieldInjectionReady"</returns>
    public static string GetReadyCallbackMethodName(string memberName)
    {
        var pascalCase = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascalCase))
            return "OnInjectionReady";

        return $"On{pascalCase}InjectionReady";
    }

    /// <summary>
    /// 将成员名转换为注入回调列表字段名。
    /// 用于 WaitFor 机制：每个 [Inject] 成员对应一个 List&lt;Action&lt;bool&gt;&gt;，
    /// 在主线程上直接调用，无需跨线程跳转。
    /// 例: "_config" → "__config_callbacks"
    /// </summary>
    public static string GetInjectionCallbackListName(string memberName)
    {
        var pascal = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascal)) return "__callbacks";
        return $"__{char.ToLower(pascal[0])}{pascal.Substring(1)}_callbacks";
    }

    /// <summary>
    /// 将成员名转换为方法参数名（camelCase，去除前导下划线）
    /// 规则：
    /// 1. 忽略前导下划线
    /// 2. 将剩余部分转换为小写驼峰格式（去除中间的下划线）
    /// </summary>
    /// <param name="memberName">成员名，如 "_myField", "my_service", "MyProperty"</param>
    /// <returns>camelCase 参数名，如 "myField", "myService", "myProperty"</returns>
    public static string ToParameterName(string memberName)
    {
        var pascal = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascal))
            return "value";
        return char.ToLower(pascal[0]) + pascal.Substring(1);
    }

    /// <summary>
    /// 规则：
    /// 1. 忽略前导下划线
    /// 2. 将剩余部分转换为大写驼峰格式（去除中间的下划线）
    /// 3. 添加 "Is" 前缀和 "InjectionReady" 后缀
    /// </summary>
    /// <param name="memberName">成员名，如 "_myField", "my_service", "MyProperty"</param>
    /// <returns>注入准备标识字段名，如 "IsMyFieldInjectionReady"</returns>
    public static string GetInjectionReadyFieldName(string memberName)
    {
        var pascalCase = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascalCase))
            return "IsInjectionReady";

        return $"Is{pascalCase}InjectionReady";
    }
}
