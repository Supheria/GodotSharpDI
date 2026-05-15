using System;
using System.Collections.Generic;
using Xunit;

namespace GodotSharpDI.Runtime.Tests;

public class InjectionExecutorTests
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
    public void Execute_SuccessfulInjection_AssignAndReadyCallbackCalled()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            string? assigned = null;
            var readyCalled = false;
            var failedCalled = false;
            var resolvedCalled = false;
            var callbackList = new List<Action<bool>>();

            InjectionExecutor.Execute(
                assign: v => assigned = v,
                value: "hello",
                readyCallback: v => { readyCalled = true; Assert.Equal("hello", v); },
                failedCallback: () => failedCalled = true,
                callbackList: callbackList,
                onDependencyResolved: () => resolvedCalled = true,
                typeName: "TestType",
                memberName: "_field");

            Assert.Equal("hello", assigned);
            Assert.True(readyCalled);
            Assert.False(failedCalled);
            Assert.True(resolvedCalled);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_NullValue_FailedCallbackCalled()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            string? assigned = null;
            var readyCalled = false;
            var failedCalled = false;
            var resolvedCalled = false;
            var callbackList = new List<Action<bool>>();

            InjectionExecutor.Execute<string>(
                assign: v => assigned = v,
                value: null,
                readyCallback: v => readyCalled = true,
                failedCallback: () => failedCalled = true,
                callbackList: callbackList,
                onDependencyResolved: () => resolvedCalled = true,
                typeName: "TestType",
                memberName: "_field");

            Assert.Null(assigned);
            Assert.False(readyCalled);
            Assert.True(failedCalled);
            Assert.True(resolvedCalled);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_AssignThrows_NotifiesCallbackAsFailureAndReportsError()
    {
        var (_, warnings, restore) = CaptureErrorReporter();
        try
        {
            var resolvedCalled = false;
            var readyCalled = false;
            var callbackResults = new List<bool>();
            var callbackList = new List<Action<bool>> { ok => callbackResults.Add(ok) };

            InjectionExecutor.Execute<string>(
                assign: v => throw new InvalidOperationException("assign broke"),
                value: "val",
                readyCallback: v => readyCalled = true,
                failedCallback: null,
                callbackList: callbackList,
                onDependencyResolved: () => resolvedCalled = true,
                typeName: "MyType",
                memberName: "_field");

            // Assign failed → callback notified with false, readyCallback not called
            Assert.Single(callbackResults);
            Assert.False(callbackResults[0]);
            Assert.False(readyCalled);
            Assert.True(resolvedCalled);
            Assert.NotEmpty(warnings);
            Assert.Contains("assign broke", warnings[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_ReadyCallbackThrows_StillNotifiesCallbacksAndReportsError()
    {
        var (errors, _, restore) = CaptureErrorReporter();
        try
        {
            var resolvedCalled = false;
            var callbackResults = new List<bool>();
            var callbackList = new List<Action<bool>> { ok => callbackResults.Add(ok) };

            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: "val",
                readyCallback: v => throw new InvalidOperationException("ready broke"),
                failedCallback: null,
                callbackList: callbackList,
                onDependencyResolved: () => resolvedCalled = true,
                typeName: "MyType",
                memberName: "_field");

            Assert.Single(callbackResults);
            Assert.True(callbackResults[0]);
            Assert.True(resolvedCalled);
            Assert.NotEmpty(errors);
            Assert.Contains("ready broke", errors[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_NullValue_FailedCallbackThrows_StillReportsError()
    {
        var (_, warnings, restore) = CaptureErrorReporter();
        try
        {
            var resolvedCalled = false;
            var callbackList = new List<Action<bool>>();

            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: null,
                readyCallback: null,
                failedCallback: () => throw new InvalidOperationException("fail broke"),
                callbackList: callbackList,
                onDependencyResolved: () => resolvedCalled = true,
                typeName: "MyType",
                memberName: "_field");

            Assert.True(resolvedCalled);
            Assert.NotEmpty(warnings);
            Assert.Contains("fail broke", warnings[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_CallbackListReceivesTrueOnSuccess()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var results = new List<bool>();
            var callbackList = new List<Action<bool>> { ok => results.Add(ok) };

            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: "ok",
                readyCallback: null,
                failedCallback: null,
                callbackList: callbackList,
                onDependencyResolved: () => { },
                typeName: "T",
                memberName: "_f");

            Assert.Single(results);
            Assert.True(results[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_CallbackListReceivesFalseOnFailure()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var results = new List<bool>();
            var callbackList = new List<Action<bool>> { ok => results.Add(ok) };

            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: null,
                readyCallback: null,
                failedCallback: null,
                callbackList: callbackList,
                onDependencyResolved: () => { },
                typeName: "T",
                memberName: "_f");

            Assert.Single(results);
            Assert.False(results[0]);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_CallbackListClearedAfterNotification()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var callbackList = new List<Action<bool>> { _ => { }, _ => { } };

            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: "ok",
                readyCallback: null,
                failedCallback: null,
                callbackList: callbackList,
                onDependencyResolved: () => { },
                typeName: "T",
                memberName: "_f");

            Assert.Empty(callbackList);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_MultipleCallbacks_AllNotified()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var count = 0;
            var callbackList = new List<Action<bool>>
            {
                _ => count++,
                _ => count++,
                _ => count++,
            };

            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: "ok",
                readyCallback: null,
                failedCallback: null,
                callbackList: callbackList,
                onDependencyResolved: () => { },
                typeName: "T",
                memberName: "_f");

            Assert.Equal(3, count);
        }
        finally { restore(); }
    }

    [Fact]
    public void Execute_OnDependencyResolved_AlwaysCalled()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var callCount = 0;

            // Success case
            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: "ok",
                readyCallback: null,
                failedCallback: null,
                callbackList: new List<Action<bool>>(),
                onDependencyResolved: () => callCount++,
                typeName: "T",
                memberName: "_f");

            // Failure case
            InjectionExecutor.Execute<string>(
                assign: v => { },
                value: null,
                readyCallback: null,
                failedCallback: null,
                callbackList: new List<Action<bool>>(),
                onDependencyResolved: () => callCount++,
                typeName: "T",
                memberName: "_f");

            Assert.Equal(2, callCount);
        }
        finally { restore(); }
    }
}
