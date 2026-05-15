using System;
using System.Collections.Generic;
using System.Reflection;
using GodotSharpDI.Runtime;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.E2E;

/// <summary>
/// End-to-end tests for basic Host→Scope→User injection flow.
/// Verifies that generated code + runtime library work together correctly.
/// </summary>
public class BasicInjectionTests
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
    public void BasicSyncProvide_InjectsIntoUser()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
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
        [Inject] private IMyService _service;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(MyHost))]
    public partial class MyScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

            var asm = E2ETestHelper.GenerateAndCompile(source);

            // Instantiate scope and host
            var scope = E2ETestHelper.Instantiate(asm, "Test.MyScope");
            var host = E2ETestHelper.Instantiate(asm, "Test.MyHost");
            var user = E2ETestHelper.Instantiate(asm, "Test.MyUser");

            // Wire parent: host→scope, user→scope
            E2ETestHelper.WireParent(host, scope);
            E2ETestHelper.WireParent(user, scope);

            // Trigger lifecycle: EnterTree → Ready
            E2ETestHelper.Notify(host, 10); // EnterTree
            E2ETestHelper.Notify(host, 13); // Ready → ProvideServices + ResolveDependencies
            E2ETestHelper.Notify(user, 10); // EnterTree
            E2ETestHelper.Notify(user, 13); // Ready → ResolveDependencies

            // Verify: _service should be injected
            var serviceValue = E2ETestHelper.GetFieldValue(user, "_service");
            Assert.NotNull(serviceValue);

            // Verify it's the right type
            var valueProp = serviceValue!.GetType().GetProperty("Value");
            Assert.Equal("hello", valueProp!.GetValue(serviceValue));
        }
        finally { restore(); }
    }

    [Fact]
    public void MethodProvider_InjectsIntoUser()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IConfig { string Name { get; } }
    public class Config : IConfig { public string Name => ""prod""; }

    [Host]
    public partial class ConfigHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IConfig) })]
        public Config BuildConfig() => new Config();

        public override partial void _Notification(int what);
    }

    [User]
    public partial class App : Godot.Node
    {
        [Inject] private IConfig _config;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(ConfigHost))]
    public partial class AppScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

            var asm = E2ETestHelper.GenerateAndCompile(source);

            var scope = E2ETestHelper.Instantiate(asm, "Test.AppScope");
            var host = E2ETestHelper.Instantiate(asm, "Test.ConfigHost");
            var user = E2ETestHelper.Instantiate(asm, "Test.App");

            E2ETestHelper.WireParent(host, scope);
            E2ETestHelper.WireParent(user, scope);

            E2ETestHelper.Notify(host, 10);
            E2ETestHelper.Notify(host, 13);
            E2ETestHelper.Notify(user, 10);
            E2ETestHelper.Notify(user, 13);

            var config = E2ETestHelper.GetFieldValue(user, "_config");
            Assert.NotNull(config);
        }
        finally { restore(); }
    }

    [Fact]
    public void MultipleServices_AllInjected()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
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
    public partial class MultiHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IServiceA) })]
        public ServiceA CreateA() => new ServiceA();

        [Provide(ExposedTypes = new[] { typeof(IServiceB) })]
        public ServiceB CreateB() => new ServiceB();

        public override partial void _Notification(int what);
    }

    [User]
    public partial class MultiUser : Godot.Node
    {
        [Inject] private IServiceA _a;
        [Inject] private IServiceB _b;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(MultiHost))]
    public partial class MultiScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

            var asm = E2ETestHelper.GenerateAndCompile(source);

            var scope = E2ETestHelper.Instantiate(asm, "Test.MultiScope");
            var host = E2ETestHelper.Instantiate(asm, "Test.MultiHost");
            var user = E2ETestHelper.Instantiate(asm, "Test.MultiUser");

            E2ETestHelper.WireParent(host, scope);
            E2ETestHelper.WireParent(user, scope);

            E2ETestHelper.Notify(host, 10);
            E2ETestHelper.Notify(host, 13);
            E2ETestHelper.Notify(user, 10);
            E2ETestHelper.Notify(user, 13);

            Assert.NotNull(E2ETestHelper.GetFieldValue(user, "_a"));
            Assert.NotNull(E2ETestHelper.GetFieldValue(user, "_b"));
        }
        finally { restore(); }
    }

    [Fact]
    public void HostSelfExposure_Works()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IGameHost { }

    [Host]
    public partial class GameHost : Godot.Node, IGameHost
    {
        [Provide(ExposedTypes = new[] { typeof(IGameHost) })]
        public GameHost Self => this;

        public override partial void _Notification(int what);
    }

    [User]
    public partial class GameUser : Godot.Node
    {
        [Inject] private IGameHost _host;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(GameHost))]
    public partial class GameScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

            var asm = E2ETestHelper.GenerateAndCompile(source);

            var scope = E2ETestHelper.Instantiate(asm, "Test.GameScope");
            var host = E2ETestHelper.Instantiate(asm, "Test.GameHost");
            var user = E2ETestHelper.Instantiate(asm, "Test.GameUser");

            E2ETestHelper.WireParent(host, scope);
            E2ETestHelper.WireParent(user, scope);

            E2ETestHelper.Notify(host, 10);
            E2ETestHelper.Notify(host, 13);
            E2ETestHelper.Notify(user, 10);
            E2ETestHelper.Notify(user, 13);

            var injectedHost = E2ETestHelper.GetFieldValue(user, "_host");
            Assert.NotNull(injectedHost);
            Assert.Same(host, injectedHost);
        }
        finally { restore(); }
    }

    [Fact]
    public void TwoHosts_CrossInjection_Works()
    {
        var (_, _, restore) = CaptureErrorReporter();
        try
        {
            var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IServiceX { }
    public interface IServiceY { }
    public class ServiceX : IServiceX { }
    public class ServiceY : IServiceY { }

    [Host]
    public partial class HostA : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IServiceX) })]
        public ServiceX CreateX() => new ServiceX();

        public override partial void _Notification(int what);
    }

    [Host]
    public partial class HostB : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IServiceY) })]
        public ServiceY CreateY() => new ServiceY();

        public override partial void _Notification(int what);
    }

    [User]
    public partial class CrossUser : Godot.Node
    {
        [Inject] private IServiceX _x;
        [Inject] private IServiceY _y;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(HostA), typeof(HostB))]
    public partial class CrossScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

            var asm = E2ETestHelper.GenerateAndCompile(source);

            var scope = E2ETestHelper.Instantiate(asm, "Test.CrossScope");
            var hostA = E2ETestHelper.Instantiate(asm, "Test.HostA");
            var hostB = E2ETestHelper.Instantiate(asm, "Test.HostB");
            var user = E2ETestHelper.Instantiate(asm, "Test.CrossUser");

            E2ETestHelper.WireParent(hostA, scope);
            E2ETestHelper.WireParent(hostB, scope);
            E2ETestHelper.WireParent(user, scope);

            E2ETestHelper.Notify(hostA, 10);
            E2ETestHelper.Notify(hostA, 13);
            E2ETestHelper.Notify(hostB, 10);
            E2ETestHelper.Notify(hostB, 13);
            E2ETestHelper.Notify(user, 10);
            E2ETestHelper.Notify(user, 13);

            Assert.NotNull(E2ETestHelper.GetFieldValue(user, "_x"));
            Assert.NotNull(E2ETestHelper.GetFieldValue(user, "_y"));
        }
        finally { restore(); }
    }
}
