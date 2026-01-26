namespace GodotSharp.DI.Generator.Internal.Helpers;

/// <summary>
/// IDE 特性辅助类
/// </summary>
internal static class IdeAttributes
{
    /// <summary>
    /// EditorBrowsable(EditorBrowsableState.Never) - 在 IDE 中隐藏成员
    /// </summary>
    public const string EditorBrowsableNever =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";

    /// <summary>
    /// CompilerGenerated - 标记为编译器生成
    /// </summary>#pragma error disable CS0619
    public const string CompilerGenerated =
        "[global::System.Runtime.CompilerServices.CompilerGenerated]";

    /// <summary>
    /// 组合特性：隐藏且标记为过时（推荐用于禁止调用的方法）
    /// </summary>
    public const string HiddenAndObsolete =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]"
        + "\n    [global::System.Obsolete(\"This method is managed by the DI framework and should not be called manually. Use the provided public APIs instead.\", true)]";
}
