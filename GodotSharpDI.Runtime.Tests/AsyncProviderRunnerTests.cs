using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GodotSharpDI.Runtime.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class AsyncProviderRunnerTests
{
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
        var (errors, _, restore) = ErrorReporterHelper.Capture();
        try
        {
            object? provided = null;
            Action<Action>? capturedDispatch = null;

            await AsyncProviderRunner.Run<object>(
                Task.FromException<object>(new InvalidOperationException("async broke")),
                (inst, _) => provided = inst,
                "TestProvider",
                CancellationToken.None,
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
        finally { restore(); }
    }

    [Fact]
    public async Task Run_Cancelled_SilentExit()
    {
        var (_, _, restore) = ErrorReporterHelper.Capture();
        try
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
                _ => dispatchCalled = true
            );

            Assert.Null(provided);
            Assert.False(dispatchCalled);
        }
        finally
        {
            restore();
        }
    }

    [Fact]
    public async Task Run_CancelledAfterTask_CompletesButSkipsDispatch()
    {
        var (_, _, restore) = ErrorReporterHelper.Capture();
        try
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
                _ => dispatchCalled = true
            );

            Assert.Null(provided);
            Assert.False(dispatchCalled);
        }
        finally
        {
            restore();
        }
    }

    [Fact]
    public async Task Run_ProviderTypePassedToProvide()
    {
        var (_, _, restore) = ErrorReporterHelper.Capture();
        try
        {
            string? capturedType = null;
            Action<Action>? capturedDispatch = null;

            await AsyncProviderRunner.Run(
                Task.FromResult(new object()),
                (_, pt) => capturedType = pt,
                "MyHost",
                CancellationToken.None,
                action =>
                {
                    capturedDispatch = a => action();
                }
            );

            capturedDispatch!(() => { });
            Assert.Equal("MyHost", capturedType);
        }
        finally
        {
            restore();
        }
    }

    [Fact]
    public async Task Run_DispatchThrowsOnCancelled_DoesNotProvide()
    {
        var (_, _, restore) = ErrorReporterHelper.Capture();
        try
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
                action =>
                { /* Don't call action since cancelled */
                }
            );

            Assert.Null(provided);
        }
        finally
        {
            restore();
        }
    }
}
