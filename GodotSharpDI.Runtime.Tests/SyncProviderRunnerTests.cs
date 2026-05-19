using System;
using System.Collections.Generic;
using GodotSharpDI.Runtime.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class SyncProviderRunnerTests
{
    private static readonly Action<string> NoOp = _ => { };

    [Fact]
    public void Run_Success_InstanceProvided()
    {
        var instance = new object();
        object? provided = null;
        string? providerType = null;

        SyncProviderRunner.Run(
            () => instance,
            (inst, pt) => { provided = inst; providerType = pt; },
            "TestProvider",
            NoOp);

        Assert.Same(instance, provided);
        Assert.Equal("TestProvider", providerType);
    }

    [Fact]
    public void Run_FactoryThrows_NullProvidedAndErrorReported()
    {
        var errors = new List<string>();
        var errorOutput = ErrorReporterHelper.CreateErrorCollector(errors);
        object? provided = null;
        string? providerType = null;

        SyncProviderRunner.Run<object>(
            () => throw new InvalidOperationException("factory broke"),
            (inst, pt) => { provided = inst; providerType = pt; },
            "TestProvider",
            errorOutput);

        Assert.Null(provided);
        Assert.Equal("TestProvider", providerType);
        Assert.NotEmpty(errors);
        Assert.Contains("factory broke", errors[0]);
    }

    [Fact]
    public void Run_ProviderTypePassedToProvide()
    {
        string? capturedType = null;

        SyncProviderRunner.Run(
            () => new object(),
            (_, pt) => capturedType = pt,
            "MyHost",
            NoOp);

        Assert.Equal("MyHost", capturedType);
    }
}
