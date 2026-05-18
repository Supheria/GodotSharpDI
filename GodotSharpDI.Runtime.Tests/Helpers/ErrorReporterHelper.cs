using System;
using System.Collections.Generic;
using GodotSharpDI.Runtime;

namespace GodotSharpDI.Runtime.Tests.Helpers;

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

    public static List<string> CaptureErrors(out Action restore)
    {
        var errors = new List<string>();
        var prev = ErrorReporter.ErrorOutput;
        ErrorReporter.ErrorOutput = msg => errors.Add(msg);
        restore = () => ErrorReporter.ErrorOutput = prev;
        return errors;
    }
}
