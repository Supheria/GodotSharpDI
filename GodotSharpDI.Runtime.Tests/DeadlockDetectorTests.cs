using System;
using System.Collections.Generic;
using GodotSharpDI.Runtime.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class DeadlockDetectorTests
{
    private static (DeadlockDetector detector, List<string> errors, Action restore) CreateDetector()
    {
        var errors = ErrorReporterHelper.CaptureErrors(out var restore);
        return (new DeadlockDetector(), errors, restore);
    }

    [Fact]
    public void ReportError_CapturesOutput()
    {
        var errors = ErrorReporterHelper.CaptureErrors(out var restore);
        try
        {
            ErrorReporter.ReportError("test");

            Assert.Single(errors);
            Assert.Equal("test", errors[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void NoCycle_SingleEdge_NoError()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            // ProviderA provides ServiceA, ProviderB provides ServiceB
            detector.RegisterServiceProvider("ProviderA", "ServiceA");
            detector.RegisterServiceProvider("ProviderB", "ServiceB");

            // ProviderA waits for ServiceB (provided by ProviderB)
            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");

            Assert.Empty(errors);
        }
        finally { restore(); }
    }

    [Fact]
    public void DirectCycle_Detected()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
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
        finally { restore(); }
    }

    [Fact]
    public void LongerCycle_ABCA_Detected()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            detector.RegisterServiceProvider("ProviderA", "ServiceA");
            detector.RegisterServiceProvider("ProviderB", "ServiceB");
            detector.RegisterServiceProvider("ProviderC", "ServiceC");

            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
            detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceC");
            detector.TrackAndDetect("GDI_WF:ProviderC:Ctx", "ServiceA");

            Assert.Single(errors);
            Assert.Contains("Deadlock", errors[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void NoPrefix_Ignored()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            detector.TrackAndDetect("SomeRandomType", "ServiceB");

            Assert.Empty(errors);
        }
        finally { restore(); }
    }

    [Fact]
    public void InvalidPrefixFormat_NoColon_Ignored()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            detector.TrackAndDetect("GDI_WF:NoColonHere", "ServiceB");

            Assert.Empty(errors);
        }
        finally { restore(); }
    }

    [Fact]
    public void MultipleEdges_NoCycle_NoError()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            detector.RegisterServiceProvider("ProviderA", "ServiceA");
            detector.RegisterServiceProvider("ProviderB", "ServiceB");
            detector.RegisterServiceProvider("ProviderC", "ServiceC");

            // ProviderA waits for both ServiceB and ServiceC — no cycle
            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceC");

            Assert.Empty(errors);
        }
        finally { restore(); }
    }

    [Fact]
    public void SelfReference_Detected()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            // ProviderA provides ServiceA and waits for ServiceA
            detector.RegisterServiceProvider("ProviderA", "ServiceA");

            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceA");

            Assert.Single(errors);
            Assert.Contains("Deadlock", errors[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void DuplicateEdges_NoExtraError()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            detector.RegisterServiceProvider("ProviderA", "ServiceA");
            detector.RegisterServiceProvider("ProviderB", "ServiceB");

            // Same edge registered twice
            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "ServiceB");
            detector.TrackAndDetect("GDI_WF:ProviderB:Ctx", "ServiceA");

            // Should detect cycle once, not multiple times
            Assert.Single(errors);
        }
        finally { restore(); }
    }

    [Fact]
    public void UnknownService_NoCycle_NoError()
    {
        var (detector, errors, restore) = CreateDetector();
        try
        {
            detector.RegisterServiceProvider("ProviderA", "ServiceA");

            // ProviderA waits for an unknown service (no provider registered)
            detector.TrackAndDetect("GDI_WF:ProviderA:Ctx", "UnknownService");

            Assert.Empty(errors);
        }
        finally { restore(); }
    }
}
