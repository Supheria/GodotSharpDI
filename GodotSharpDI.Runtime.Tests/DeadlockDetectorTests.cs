using System.Collections.Generic;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class DeadlockDetectorTests
{
    private static (DeadlockDetector detector, List<string> errors) CreateDetector()
    {
        var errors = new List<string>();
        var detector = new DeadlockDetector();
        ErrorReporter.ErrorOutput = msg => errors.Add(msg);
        return (detector, errors);
    }

    [Fact]
    public void ErrorOutput_CallbackWorks()
    {
        var errors = new List<string>();
        ErrorReporter.ErrorOutput = msg => errors.Add(msg);
        ErrorReporter.ErrorOutput("test");

        Assert.Single(errors);
        Assert.Equal("test", errors[0]);
    }

    [Fact]
    public void NoCycle_SingleEdge_NoError()
    {
        var (detector, errors) = CreateDetector();

        // ProviderA provides ServiceA, ProviderB provides ServiceB
        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");

        // ProviderA waits for ServiceB (provided by ProviderB)
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");

        Assert.Empty(errors);
    }

    [Fact]
    public void DirectCycle_Detected()
    {
        var errors = new List<string>();
        ErrorReporter.ErrorOutput = msg => errors.Add(msg);
        var detector = new DeadlockDetector();

        // ProviderA provides ServiceA, ProviderB provides ServiceB
        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");

        // ProviderA waits for ServiceB - no cycle yet
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
        Assert.Empty(errors);

        // ProviderB waits for ServiceA - creates cycle A→B→A
        detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceA");

        Assert.True(errors.Count > 0,
            "Expected cycle error. ProviderA waits for ServiceB, ProviderB waits for ServiceA.");
        Assert.Contains("Deadlock", errors[0]);
    }

    [Fact]
    public void LongerCycle_ABCA_Detected()
    {
        var (detector, errors) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");
        detector.RegisterServiceProvider("ProviderC", "ServiceC");

        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
        detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceC");
        detector.TrackAndDetect("GDI_WF:ProviderC:Ctx", "ServiceA");

        Assert.Single(errors);
        Assert.Contains("Deadlock", errors[0]);
    }

    [Fact]
    public void NoPrefix_Ignored()
    {
        var (detector, errors) = CreateDetector();

        detector.TrackAndDetect("SomeRandomType", "ServiceB");

        Assert.Empty(errors);
    }

    [Fact]
    public void InvalidPrefixFormat_NoColon_Ignored()
    {
        var (detector, errors) = CreateDetector();

        detector.TrackAndDetect("GDI_WF:NoColonHere", "ServiceB");

        Assert.Empty(errors);
    }

    [Fact]
    public void MultipleEdges_NoCycle_NoError()
    {
        var (detector, errors) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");
        detector.RegisterServiceProvider("ProviderC", "ServiceC");

        // ProviderA waits for both ServiceB and ServiceC — no cycle
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceC");

        Assert.Empty(errors);
    }

    [Fact]
    public void SelfReference_Detected()
    {
        var (detector, errors) = CreateDetector();

        // ProviderA provides ServiceA and waits for ServiceA
        detector.RegisterServiceProvider("ProviderA", "ServiceA");

        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceA");

        Assert.Single(errors);
        Assert.Contains("Deadlock", errors[0]);
    }

    [Fact]
    public void DuplicateEdges_NoExtraError()
    {
        var (detector, errors) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");
        detector.RegisterServiceProvider("ProviderB", "ServiceB");

        // Same edge registered twice
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
        detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceA");

        // Should detect cycle once, not multiple times
        Assert.Single(errors);
    }

    [Fact]
    public void UnknownService_NoCycle_NoError()
    {
        var (detector, errors) = CreateDetector();

        detector.RegisterServiceProvider("ProviderA", "ServiceA");

        // ProviderA waits for an unknown service (no provider registered)
        detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "UnknownService");

        Assert.Empty(errors);
    }
}
