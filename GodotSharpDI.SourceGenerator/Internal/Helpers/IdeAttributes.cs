namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// IDE attribute helper class
/// </summary>
internal static class IdeAttributes
{
    /// <summary>
    /// EditorBrowsable(EditorBrowsableState.Never) - Hide member in IDE
    /// </summary>
    public const string EditorBrowsableNever =
        "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]";
}
