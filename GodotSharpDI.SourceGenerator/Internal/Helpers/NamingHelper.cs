using System.Text;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// Naming conversion helper class
/// </summary>
internal static class NamingHelper
{
    /// <summary>
    /// Convert member name to PascalCase format
    /// Rules:
    /// 1. Ignore leading underscores
    /// 2. Convert remaining part to PascalCase format (remove underscores in between)
    /// </summary>
    /// <param name="memberName">Member name, e.g., "_myField", "my_service", "MyProperty"</param>
    /// <returns>PascalCase formatted name, e.g., "MyField", "MyService", "MyProperty"</returns>
    public static string ToPascalCase(string memberName)
    {
        if (string.IsNullOrEmpty(memberName))
            return string.Empty;

        // Remove leading underscores
        int startIndex = 0;
        while (startIndex < memberName.Length && memberName[startIndex] == '_')
        {
            startIndex++;
        }

        if (startIndex >= memberName.Length)
            return string.Empty;

        var sb = new StringBuilder();

        // Convert to PascalCase format, remove underscores
        bool capitalizeNext = true; // Capitalize first character
        for (int i = startIndex; i < memberName.Length; i++)
        {
            char c = memberName[i];

            if (c == '_')
            {
                // Underscore encountered, next character needs to be capitalized
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
    /// Convert member name to failure callback method name
    /// Rules:
    /// 1. Ignore leading underscores
    /// 2. Convert remaining part to PascalCase format (remove underscores in between)
    /// 3. Add "On" prefix and "InjectionFailed" suffix
    /// </summary>
    /// <param name="memberName">Member name, e.g., "_myField", "my_service", "MyProperty"</param>
    /// <returns>Failure callback method name, e.g., "OnMyFieldInjectionFailed"</returns>
    public static string GetFailureCallbackMethodName(string memberName)
    {
        var pascalCase = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascalCase))
            return "OnInjectionFailed";

        return $"On{pascalCase}InjectionFailed";
    }

    /// <summary>
    /// Convert member name to ready callback method name
    /// Rules:
    /// 1. Ignore leading underscores
    /// 2. Convert remaining part to PascalCase format (remove underscores in between)
    /// 3. Add "On" prefix and "InjectionReady" suffix
    /// </summary>
    /// <param name="memberName">Member name, e.g., "_myField", "my_service", "MyProperty"</param>
    /// <returns>Ready callback method name, e.g., "OnMyFieldInjectionReady"</returns>
    public static string GetReadyCallbackMethodName(string memberName)
    {
        var pascalCase = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascalCase))
            return "OnInjectionReady";

        return $"On{pascalCase}InjectionReady";
    }

    /// <summary>
    /// Convert member name to injection callback list field name.
    /// Used for WaitFor mechanism: Each [Inject] member corresponds to a List&lt;Action&lt;bool&gt;&gt;,
    /// called directly on the main thread, no cross-thread jumping needed.
    /// Example: "_config" → "__config_callbacks"
    /// </summary>
    public static string GetInjectionCallbackListName(string memberName)
    {
        var pascal = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascal)) return "__callbacks";
        return $"__{char.ToLower(pascal[0])}{pascal.Substring(1)}_callbacks";
    }

    /// <summary>
    /// Convert member name to method parameter name (camelCase, remove leading underscores)
    /// Rules:
    /// 1. Ignore leading underscores
    /// 2. Convert remaining part to camelCase format (remove underscores in between)
    /// </summary>
    /// <param name="memberName">Member name, e.g., "_myField", "my_service", "MyProperty"</param>
    /// <returns>camelCase parameter name, e.g., "myField", "myService", "myProperty"</returns>
    public static string ToParameterName(string memberName)
    {
        var pascal = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascal))
            return "value";
        return char.ToLower(pascal[0]) + pascal.Substring(1);
    }

    /// <summary>
    /// Rules:
    /// 1. Ignore leading underscores
    /// 2. Convert remaining part to PascalCase format (remove underscores in between)
    /// 3. Add "Is" prefix and "InjectionReady" suffix
    /// </summary>
    /// <param name="memberName">Member name, e.g., "_myField", "my_service", "MyProperty"</param>
    /// <returns>Injection ready flag field name, e.g., "IsMyFieldInjectionReady"</returns>
    public static string GetInjectionReadyFieldName(string memberName)
    {
        var pascalCase = ToPascalCase(memberName);
        if (string.IsNullOrEmpty(pascalCase))
            return "IsInjectionReady";

        return $"Is{pascalCase}InjectionReady";
    }
}
