using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GodotSharpDI.Runtime.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class AsyncProviderRunnerTests
{
    private static readonly Action<string> NoOp = _ => { };

    [Fact]
    public async Task Run_Success_InstanceProvidedViaDispatch()
    {
        var instance = new object();
        object? provided = null;
        Action<Action>? capturedDispatch = null;

        await AsyncProviderRunner.Run(
            Task.FromResult(instance),
            (inst, _) => provided = inst,
            "TestProvider",
            CancellationToken.None,
            NoOp,
            action =>
            {
                capturedDispatch = a => action();
            }
        );

        // Simulate main-thread dispatch
        Assert.NotNull(capturedDispatch);
        capturedDispatch!(() => { });
        Assert.Same(instance, provided);
    }

    [Fact]
    public async Task Run_TaskThrows_ErrorReportedAndNullProvided()
    {
        var errors = new List<string>();
        var errorOutput = ErrorReporterHelper.CreateErrorCollector(errors);
        object? provided = null;
        Action<Action>? capturedDispatch = null;

        await AsyncProviderRunner.Run<object>(
            Task.FromException<object>(new InvalidOperationException("async broke")),
            (inst, _) => provided = inst,
            "TestProvider",
            CancellationToken.None,
            errorOutput,
            action =>
            {
                capturedDispatch = a => action();
            }
        );

        Assert.NotEmpty(errors);
        Assert.Contains("async broke", errors[0]);

        // Simulate main-thread dispatch for the failure path
        Assert.NotNull(capturedDispatch);
        capturedDispatch!(() => { });
        Assert.Null(provided);
    }

    [Fact]
    public async Task Run_Cancelled_SilentExit()
    {
        object? provided = null;
        var dispatchCalled = false;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await AsyncProviderRunner.Run(
            Task.FromResult(new object()),
            (inst, _) => provided = inst,
            "TestProvider",
            cts.Token,
            NoOp,
            _ => dispatchCalled = true
        );

        Assert.Null(provided);
        Assert.False(dispatchCalled);
    }

    [Fact]
    public async Task Run_CancelledAfterTask_CompletesButSkipsDispatch()
    {
        object? provided = null;
        var dispatchCalled = false;

        using var cts = new CancellationTokenSource();

        // Create a task that completes, then cancel before dispatch
        var task = Task.FromResult(new object());
        await task;

        // Cancel before the runner processes the result
        cts.Cancel();

        await AsyncProviderRunner.Run(
            task,
            (inst, _) => provided = inst,
            "TestProvider",
            cts.Token,
            NoOp,
            _ => dispatchCalled = true
        );

        Assert.Null(provided);
        Assert.False(dispatchCalled);
    }

    [Fact]
    public async Task Run_ProviderTypePassedToProvide()
    {
        string? capturedType = null;
        Action<Action>? capturedDispatch = null;

        await AsyncProviderRunner.Run(
            Task.FromResult(new object()),
            (_, pt) => capturedType = pt,
            "MyHost",
            CancellationToken.None,
            NoOp,
            action =>
            {
                capturedDispatch = a => action();
            }
        );

        capturedDispatch!(() => { });
        Assert.Equal("MyHost", capturedType);
    }

    [Fact]
    public async Task Run_DispatchThrowsOnCancelled_DoesNotProvide()
    {
        object? provided = null;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Dispatch that throws when cancelled — should not propagate
        await AsyncProviderRunner.Run(
            Task.FromResult(new object()),
            (inst, _) => provided = inst,
            "TestProvider",
            cts.Token,
            NoOp,
            action =>
            { /* Don't call action since cancelled */
            }
        );

        Assert.Null(provided);
    }
}
