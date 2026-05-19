using System;
using System.Collections.Generic;
using System.Reflection;
using GodotSharpDI.Runtime;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.E2E;

/// <summary>
/// End-to-end tests for injection callbacks: ReadyCallback, FailureCallback, IDependenciesResolved.
/// </summary>
public class CallbackTests
{
    [Fact]
    public void ReadyCallback_FiresOnSuccessfulInjection()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { string Value { get; } }
    public class MyService : IMyService { public string Value => ""hello""; }

    [Host]
    public partial class MyHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IMyService) })]
        public MyService CreateService() => new MyService();

        public override partial void _Notification(int what);
    }

    [User]
    public partial class MyUser : Godot.Node
    {
        [Inject(ReadyCallback = true)] private IMyService _service;
        public IMyService? LastReadyValue { get; private set; }

        partial void OnServiceInjectionReady(IMyService service)
        {
            LastReadyValue = service;
        }

        public override partial void _Notification(int what);
    }

    [Modules(typeof(MyHost))]
    public partial class MyScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

        var asm = E2ETestHelper.GenerateAndCompile(source);

        var scope = E2ETestHelper.Instantiate(asm, "Test.MyScope");
        var host = E2ETestHelper.Instantiate(asm, "Test.MyHost");
        var user = E2ETestHelper.Instantiate(asm, "Test.MyUser");

        E2ETestHelper.WireParent(host, scope);
        E2ETestHelper.WireParent(user, scope);

        E2ETestHelper.Notify(host, 10);
        E2ETestHelper.Notify(host, 13);
        E2ETestHelper.Notify(user, 10);
        E2ETestHelper.Notify(user, 13);

        // Verify injection succeeded
        var service = E2ETestHelper.GetFieldValue(user, "_service");
        Assert.NotNull(service);

        // Verify ReadyCallback fired with the correct value
        var lastReady = E2ETestHelper.GetPropertyValue(user, "LastReadyValue");
        Assert.NotNull(lastReady);
        Assert.Same(service, lastReady);
    }

    [Fact]
    public void FailureCallback_FiresWhenProviderFails()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IMyService { }
    public class MyService : IMyService { }

    [Host]
    public partial class FailingHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IMyService) })]
        public MyService CreateService() => throw new System.InvalidOperationException(""provider failed"");

        public override partial void _Notification(int what);
    }

    [User]
    public partial class MyUser : Godot.Node
    {
        [Inject(FailureCallback = true)] private IMyService _service;
        public bool FailureCallbackFired { get; private set; }

        partial void OnServiceInjectionFailed()
        {
            FailureCallbackFired = true;
        }

        public override partial void _Notification(int what);
    }

    [Modules(typeof(FailingHost))]
    public partial class MyScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

        var asm = E2ETestHelper.GenerateAndCompile(source);

        var scope = E2ETestHelper.Instantiate(asm, "Test.MyScope");
        var host = E2ETestHelper.Instantiate(asm, "Test.FailingHost");
        var user = E2ETestHelper.Instantiate(asm, "Test.MyUser");

        E2ETestHelper.WireParent(host, scope);
        E2ETestHelper.WireParent(user, scope);

        E2ETestHelper.Notify(host, 10);
        E2ETestHelper.Notify(host, 13);
        E2ETestHelper.Notify(user, 10);
        E2ETestHelper.Notify(user, 13);

        // Verify injection failed (provider threw)
        var service = E2ETestHelper.GetFieldValue(user, "_service");
        Assert.Null(service);

        // Verify FailureCallback fired
        var fired = E2ETestHelper.GetPropertyValue(user, "FailureCallbackFired");
        Assert.Equal(true, fired);
    }

    [Fact]
    public void IDependenciesResolved_CalledAfterAllInjections()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IServiceA { }
    public interface IServiceB { }
    public class ServiceA : IServiceA { }
    public class ServiceB : IServiceB { }

    [Host]
    public partial class MyHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IServiceA) })]
        public ServiceA CreateA() => new ServiceA();

        [Provide(ExposedTypes = new[] { typeof(IServiceB) })]
        public ServiceB CreateB() => new ServiceB();

        public override partial void _Notification(int what);
    }

    [User]
    public partial class MyUser : Godot.Node, IDependenciesResolved
    {
        [Inject] private IServiceA _a;
        [Inject] private IServiceB _b;
        public int ResolvedCallCount { get; private set; }

        void IDependenciesResolved.OnDependenciesResolved()
        {
            ResolvedCallCount++;
        }

        public override partial void _Notification(int what);
    }

    [Modules(typeof(MyHost))]
    public partial class MyScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

        var asm = E2ETestHelper.GenerateAndCompile(source);

        var scope = E2ETestHelper.Instantiate(asm, "Test.MyScope");
        var host = E2ETestHelper.Instantiate(asm, "Test.MyHost");
        var user = E2ETestHelper.Instantiate(asm, "Test.MyUser");

        E2ETestHelper.WireParent(host, scope);
        E2ETestHelper.WireParent(user, scope);

        E2ETestHelper.Notify(host, 10);
        E2ETestHelper.Notify(host, 13);
        E2ETestHelper.Notify(user, 10);
        E2ETestHelper.Notify(user, 13);

        // Verify both injections succeeded
        Assert.NotNull(E2ETestHelper.GetFieldValue(user, "_a"));
        Assert.NotNull(E2ETestHelper.GetFieldValue(user, "_b"));

        // Verify OnDependenciesResolved was called exactly once
        var count = E2ETestHelper.GetPropertyValue(user, "ResolvedCallCount");
        Assert.Equal(1, count);
    }
}
