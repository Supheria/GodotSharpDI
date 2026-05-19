using System;
using System.Collections.Generic;
using GodotSharpDI.Runtime.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class DeadlockDetectorTests
{
    private static (DeadlockDetector detector, List<string> errors, Action<string> errorOutput) CreateDetector()
    {
        var errors = new List<string>();
        return (new DeadlockDetector(), errors, ErrorReporterHelper.CreateErrorCollector(errors));
    }

    [Fact]
    public void ReportError_CapturesOutput()
    {
        var errors = new List<string>();
        var errorOutput = ErrorReporterHelper.CreateErrorCollector(errors);

        ErrorReporter.ReportError("test", errorOutput);

        Assert.Single(errors);
        Assert.Equal("test", errors[0]);
    }

    [Fact]
    public void NoCycle_SingleEdge_NoError()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        // ProviderA provides ServiceA, ProviderB provides ServiceB
        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");

        // ProviderA waits for ServiceB (provided by ProviderB)
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB", errorOutput);

        Assert.Empty(errors);
    }

    [Fact]
    public void DirectCycle_Detected()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        // ProviderA provides ServiceA, ProviderB provides ServiceB
        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");

        // ProviderA waits for ServiceB - no cycle yet
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB", errorOutput);
        Assert.Empty(errors);

        // ProviderB waits for ServiceA - creates cycle A→B→A
        detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceA", errorOutput);

        Assert.True(errors.Count > 0,
            "Expected cycle error. ProviderA waits for ServiceB, ProviderB waits for ServiceA.");
        Assert.Contains("Deadlock", errors[0]);
    }

    [Fact]
    public void LongerCycle_ABCA_Detected()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");
        detector.RegisterServiceProvider("ProviderC", "ServiceC");

        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB", errorOutput);
        detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceC", errorOutput);
        detector.TrackAndDetect("GDI_WF:ProviderC:Ctx", "ServiceA", errorOutput);

        Assert.Single(errors);
        Assert.Contains("Deadlock", errors[0]);
    }

    [Fact]
    public void NoPrefix_Ignored()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        detector.TrackAndDetect("SomeRandomType", "ServiceB", errorOutput);

        Assert.Empty(errors);
    }

    [Fact]
    public void InvalidPrefixFormat_NoColon_Ignored()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        detector.TrackAndDetect("GDI_WF:NoColonHere", "ServiceB", errorOutput);

        Assert.Empty(errors);
    }

    [Fact]
    public void MultipleEdges_NoCycle_NoError()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");
        detector.RegisterServiceProvider("ProviderC", "ServiceC");

        // ProviderA waits for both ServiceB and ServiceC — no cycle
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB", errorOutput);
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceC", errorOutput);

        Assert.Empty(errors);
    }

    [Fact]
    public void SelfReference_Detected()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        // ProviderA provides ServiceA and waits for ServiceA
        detector.RegisterServiceProvider("ProviderA", "ServiceA");

        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceA", errorOutput);

        Assert.Single(errors);
        Assert.Contains("Deadlock", errors[0]);
    }

    [Fact]
    public void DuplicateEdges_NoExtraError()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");

        // Same edge registered twice
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB", errorOutput);
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB", errorOutput);
        detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceA", errorOutput);

        // Should detect cycle once, not multiple times
        Assert.Single(errors);
    }

    [Fact]
    public void UnknownService_NoCycle_NoError()
    {
        var (detector, errors, errorOutput) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");

        // ProviderA waits for an unknown service (no provider registered)
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "UnknownService", errorOutput);

        Assert.Empty(errors);
    }
}
