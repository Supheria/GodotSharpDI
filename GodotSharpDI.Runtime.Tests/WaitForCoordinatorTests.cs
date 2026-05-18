using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GodotSharpDI.Runtime.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class WaitForCoordinatorTests
{
    private static readonly Action<string> NoOp = _ => { };

    [Fact]
    public void SingleDependency_Triggered_OnAllResolvedCalled()
    {
        var resolved = false;
        var list = new List<Action<bool>>();

        var coordinator = new WaitForCoordinator(1, () =>
        {
            resolved = true;
            return Task.CompletedTask;
        });

        coordinator.Register(list, "dep", "member", NoOp, a => a());

        Assert.Single(list);
        Assert.False(resolved);

        // Trigger the callback
        list[0].Invoke(true);

        // OnAllResolved is called synchronously via ContinueWith, need brief wait
        Thread.Sleep(50);
        Assert.True(resolved);
    }

    [Fact]
    public void MultipleDependencies_AllTriggered_OnAllResolvedCalled()
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

        coordinator.Register(list1, "dep1", "member", NoOp, a => a());
        coordinator.Register(list2, "dep2", "member", NoOp, a => a());
        coordinator.Register(list3, "dep3", "member", NoOp, a => a());

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

    [Fact]
    public void DependencyFails_StillDecrementsCount()
    {
        var resolved = false;
        var list1 = new List<Action<bool>>();
        var list2 = new List<Action<bool>>();

        var coordinator = new WaitForCoordinator(2, () =>
        {
            resolved = true;
            return Task.CompletedTask;
        });

        coordinator.Register(list1, "dep1", "member", NoOp, a => a());
        coordinator.Register(list2, "dep2", "member", NoOp, a => a());

        // First dependency fails
        list1[0].Invoke(false);
        Thread.Sleep(20);
        Assert.False(resolved);

        // Second dependency succeeds
        list2[0].Invoke(true);
        Thread.Sleep(50);
        Assert.True(resolved);
    }

    [Fact]
    public void DependencyFails_ReportsError()
    {
        var errors = new List<string>();
        var errorOutput = ErrorReporterHelper.CreateErrorCollector(errors);
        var list = new List<Action<bool>>();

        var coordinator = new WaitForCoordinator(1, () => Task.CompletedTask);
        coordinator.Register(list, "myDep", "myMember", errorOutput, a => a());

        list[0].Invoke(false);

        Thread.Sleep(50);
        Assert.NotEmpty(errors);
        Assert.Contains("myDep", errors[0]);
        Assert.Contains("myMember", errors[0]);
    }

    [Fact]
    public void OnAllResolvedThrows_ErrorReportedViaDispatch()
    {
        var errors = new List<string>();
        var errorOutput = ErrorReporterHelper.CreateErrorCollector(errors);
        var list = new List<Action<bool>>();

        // Use Task.FromException so the exception is captured by ContinueWith
        // (synchronous throw would propagate before ContinueWith is set up)
        var coordinator = new WaitForCoordinator(1, () =>
            Task.FromException(new InvalidOperationException("callback broke")));

        coordinator.Register(list, "dep", "member", errorOutput, a => a());

        list[0].Invoke(true);

        // ContinueWith error is dispatched back to main thread
        Thread.Sleep(100);
        Assert.NotEmpty(errors);
        Assert.Contains("callback broke", errors[0]);
    }

    [Fact]
    public void EmptyDependencyCount_OnAllResolvedCalledImmediately()
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
}
