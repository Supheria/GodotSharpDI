using System;
using System.Collections.Generic;

namespace GodotSharpDI.SourceGenerator.Tests.Helpers;

public static class ErrorReporterHelper
{
    public static Action<string> CreateErrorCollector(List<string> errors)
    {
        return msg => errors.Add(msg);
    }
}
