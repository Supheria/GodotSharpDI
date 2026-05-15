using System.Collections.Generic;
using System.Text;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

internal sealed class CodeFormatter
{
    private readonly StringBuilder _sb = new();
    private int _level;

    public CodeFormatter CreateFromCurrentLevel()
    {
        return new CodeFormatter { _level = _level };
    }

    private void Indent()
    {
        _sb.Append(' ', _level * 4);
    }

    public void AppendRaw(string text, bool indent = false)
    {
        if (indent)
        {
            Indent();
        }
        _sb.Append(text);
    }

    public void AppendLine()
    {
        _sb.Append('\n');
    }

    public void AppendLine(string text)
    {
        Indent();
        _sb.Append(text);
        _sb.Append('\n');
    }

    public void AppendLine(string text, string comment)
    {
        Indent();
        _sb.Append(text);
        _sb.Append(" // ");
        _sb.Append(comment);
        _sb.Append('\n');
    }

    public void AppendLineIf(bool condition, string text)
    {
        if (condition)
            AppendLine(text);
    }

    /// <summary>
    /// Emit an opening brace '{' and increase indentation level.
    /// Must be paired with <see cref="EndBlock()"/> or <see cref="EndBlock(string)"/>.
    /// </summary>
    public void BeginBlock()
    {
        AppendLine("{");
        _level++;
    }

    /// <summary>
    /// Decrease indentation level and emit a closing brace '}'.
    /// </summary>
    public void EndBlock()
    {
        _level--;
        AppendLine("}");
    }

    /// <summary>
    /// Decrease indentation level and emit a closing brace '}' followed by <paramref name="append"/>
    /// (e.g., a comma for collection initializer entries or a semicolon).
    /// </summary>
    public void EndBlock(string append)
    {
        _level--;
        Indent();
        _sb.Append('}');
        _sb.Append(append);
        _sb.Append('\n');
    }

    /// <summary>
    /// Increase indentation level without emitting a brace.
    /// Use for aligning multi-line expressions (e.g., method call arguments).
    /// Must be paired with <see cref="EndLevel"/>.
    /// Unlike <see cref="BeginBlock"/>/<see cref="EndBlock"/>, this does NOT emit { }.
    /// </summary>
    public void BeginLevel()
    {
        _level++;
    }

    /// <summary>
    /// Decrease indentation level (paired with <see cref="BeginLevel"/>).
    /// </summary>
    public void EndLevel()
    {
        _level--;
    }

    public void AppendXmlComment(string text)
    {
        AppendLine("/// " + text);
    }

    public void AppendXmlCodeBlock(IEnumerable<RemarkItem> items)
    {
        AppendXmlComment("<code>");
        foreach (var (type, name) in items)
        {
            AppendXmlComment($"<b>{type}</b> {name}<br/>");
        }
        AppendXmlComment("</code>");
    }

    public override string ToString() => _sb.ToString();
}

internal readonly record struct RemarkItem(string Type, string Name);
