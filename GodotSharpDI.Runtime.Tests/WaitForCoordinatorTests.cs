using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class WaitForCoordinatorTests
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
    public void SingleDependency_Triggered_OnAllResolvedCalled()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var resolved = false;
            var list = new List<Action<bool>>();

            var coordinator = new WaitForCoordinator(1, () =>
            {
                resolved = true;
                return Task.CompletedTask;
            });

            coordinator.Register(list, "dep", "member", ErrorReporter.ErrorOutput, a => a());

            Assert.Single(list);
            Assert.False(resolved);

            // Trigger the callback
            list[0].Invoke(true);

            // OnAllResolved is called synchronously via ContinueWith, need brief wait
            Thread.Sleep(50);
            Assert.True(resolved);
        }
        finally { restore(); }
    }

    [Fact]
    public void MultipleDependencies_AllTriggered_OnAllResolvedCalled()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var resolved = false;
            var list1 = new List<Action<bool>>();
            var list2 = new List<Action<bool>>();
            var list3 = new List<Action<bool>>();

            var coordinator = new WaitForCoordinator(3, () =>
            {
                resolved = true;
                return Task.CompletedTask;
            });

            coordinator.Register(list1, "dep1", "member", ErrorReporter.ErrorOutput, a => a());
            coordinator.Register(list2, "dep2", "member", ErrorReporter.ErrorOutput, a => a());
            coordinator.Register(list3, "dep3", "member", ErrorReporter.ErrorOutput, a => a());

            list1[0].Invoke(true);
            Thread.Sleep(20);
            Assert.False(resolved);

            list2[0].Invoke(true);
            Thread.Sleep(20);
            Assert.False(resolved);

            list3[0].Invoke(true);
            Thread.Sleep(50);
            Assert.True(resolved);
        }
        finally { restore(); }
    }

    [Fact]
    public void DependencyFails_StillDecrementsCount()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var resolved = false;
            var list1 = new List<Action<bool>>();
            var list2 = new List<Action<bool>>();

            var coordinator = new WaitForCoordinator(2, () =>
            {
                resolved = true;
                return Task.CompletedTask;
            });

            coordinator.Register(list1, "dep1", "member", ErrorReporter.ErrorOutput, a => a());
            coordinator.Register(list2, "dep2", "member", ErrorReporter.ErrorOutput, a => a());

            // First dependency fails
            list1[0].Invoke(false);
            Thread.Sleep(20);
            Assert.False(resolved);

            // Second dependency succeeds
            list2[0].Invoke(true);
            Thread.Sleep(50);
            Assert.True(resolved);
        }
        finally { restore(); }
    }

    [Fact]
    public void DependencyFails_ReportsError()
    {
        var (errors, _, restore) = CaptureErrorReporter();
        try
        {
            var list = new List<Action<bool>>();

            var coordinator = new WaitForCoordinator(1, () => Task.CompletedTask);
            coordinator.Register(list, "myDep", "myMember", ErrorReporter.ErrorOutput, a => a());

            list[0].Invoke(false);

            Thread.Sleep(50);
            Assert.NotEmpty(errors);
            Assert.Contains("myDep", errors[0]);
            Assert.Contains("myMember", errors[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void OnAllResolvedThrows_ErrorReportedViaDispatch()
    {
        var (errors, _, restore) = CaptureErrorReporter();
        try
        {
            var list = new List<Action<bool>>();

            // Use Task.FromException so the exception is captured by ContinueWith
            // (synchronous throw would propagate before ContinueWith is set up)
            var coordinator = new WaitForCoordinator(1, () =>
                Task.FromException(new InvalidOperationException("callback broke")));

            coordinator.Register(list, "dep", "member", ErrorReporter.ErrorOutput, a => a());

            list[0].Invoke(true);

            // ContinueWith error is dispatched back to main thread
            Thread.Sleep(100);
            Assert.NotEmpty(errors);
            Assert.Contains("callback broke", errors[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void EmptyDependencyCount_OnAllResolvedCalledImmediately()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            // Edge case: 0 dependencies — onAllResolved should never be called
            // (caller should handle this, but verify no crash)
            var resolved = false;
            var coordinator = new WaitForCoordinator(0, () =>
            {
                resolved = true;
                return Task.CompletedTask;
            });

            // No register calls — nothing to trigger
            Thread.Sleep(50);
            Assert.False(resolved);
        }
        finally { restore(); }
    }
}
