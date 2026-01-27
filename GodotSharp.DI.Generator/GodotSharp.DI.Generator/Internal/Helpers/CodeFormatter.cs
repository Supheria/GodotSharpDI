using System.Collections.Generic;
using System.Text;

namespace GodotSharp.DI.Generator.Internal.Helpers;

internal sealed class CodeFormatter
{
    private readonly StringBuilder _sb = new();
    private int _level;

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

    public void BeginBlock()
    {
        AppendLine("{");
        _level++;
    }

    public void EndBlock()
    {
        _level--;
        AppendLine("}");
    }

    public void EndBlock(string append)
    {
        _level--;
        Indent();
        _sb.Append('}');
        _sb.Append(append);
        _sb.Append('\n');
    }

    public void BeginLevel()
    {
        _level++;
    }

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
