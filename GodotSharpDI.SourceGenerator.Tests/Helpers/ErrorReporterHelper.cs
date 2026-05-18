using System;
using System.Collections.Generic;
using GodotSharpDI.Runtime;

namespace GodotSharpDI.SourceGenerator.Tests.Helpers;

public static class ErrorReporterHelper
{
    public static (List<string> errors, List<string> warnings, Action restore) Capture()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var prevError = ErrorReporter.ErrorOutput;
        var prevOutput = ErrorReporter.Output;
        ErrorReporter.ErrorOutput = msg => errors.Add(msg);
        ErrorReporter.Output = msg => warnings.Add(msg);
        return (errors, warnings, () =>
        {
            ErrorReporter.ErrorOutput = prevError;
            ErrorReporter.Output = prevOutput;
        });
    }
}
