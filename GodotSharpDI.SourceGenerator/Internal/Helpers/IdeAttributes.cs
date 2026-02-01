namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

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
}
