using System;
using System.Collections.Generic;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class SyncProviderRunnerTests
{
    private static (List<string> errors, List<string> warnings, Action restore) CaptureErrorReporter()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var prevError = ErrorReporter.ErrorOutput;
        var prevOutput = ErrorReporter.Output;
        ErrorReporter.ErrorOutput = msg => errors.Add(msg);
        ErrorReporter.Output = msg => warnings.Add(msg);
        return (errors, warnings, () => { ErrorReporter.ErrorOutput = prevError; ErrorReporter.Output = prevOutput; });
    }

    [Fact]
    public void Run_Success_InstanceProvided()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var instance = new object();
            object? provided = null;
            string? providerType = null;

            SyncProviderRunner.Run(
                () => instance,
                (inst, pt) => { provided = inst; providerType = pt; },
                "TestProvider");

            Assert.Same(instance, provided);
            Assert.Equal("TestProvider", providerType);
        }
        finally { restore(); }
    }

    [Fact]
    public void Run_FactoryThrows_NullProvidedAndErrorReported()
    {
        var (_, warnings, restore) = CaptureErrorReporter();
        try
        {
            object? provided = null;
            string? providerType = null;

            SyncProviderRunner.Run<object>(
                () => throw new InvalidOperationException("factory broke"),
                (inst, pt) => { provided = inst; providerType = pt; },
                "TestProvider");

            Assert.Null(provided);
            Assert.Equal("TestProvider", providerType);
            Assert.NotEmpty(warnings);
            Assert.Contains("factory broke", warnings[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void Run_ProviderTypePassedToProvide()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            string? capturedType = null;

            SyncProviderRunner.Run(
                () => new object(),
                (_, pt) => capturedType = pt,
                "MyHost");

            Assert.Equal("MyHost", capturedType);
        }
        finally { restore(); }
    }
}
