using System;
using System.Collections.Generic;
using GodotSharpDI.Runtime.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class SyncProviderRunnerTests
{
    [Fact]
    public void Run_Success_InstanceProvided()
    {
        var _ = ErrorReporterHelper.CaptureErrors(out var restore);
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
        var errors = ErrorReporterHelper.CaptureErrors(out var restore);
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
            Assert.NotEmpty(errors);
            Assert.Contains("factory broke", errors[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void Run_ProviderTypePassedToProvide()
    {
        var _ = ErrorReporterHelper.CaptureErrors(out var restore);
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
