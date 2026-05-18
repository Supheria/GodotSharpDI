using System;
using System.Collections.Generic;
using System.Reflection;
using GodotSharpDI.Runtime;
using GodotSharpDI.SourceGenerator.Tests.Helpers;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.E2E;

/// <summary>
/// End-to-end tests for the WaitFor mechanism.
/// Verifies that [Provide(WaitFor = ...)] correctly delays service provision
/// until the specified [Inject] dependencies are resolved.
/// </summary>
public class WaitForTests
{
    [Fact]
    public void WaitFor_SingleDep_ServiceProvidedAfterInjection()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IConfig { string Name { get; } }
    public class Config : IConfig { public string Name => ""prod""; }

    public interface IMyService { string ConfigName { get; } }
    public class MyService : IMyService
    {
        private readonly IConfig _config;
        public MyService(IConfig config) { _config = config; }
        public string ConfigName => _config.Name;
    }

    [Host]
    public partial class ConfigHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IConfig) })]
        public Config BuildConfig() => new Config();

        public override partial void _Notification(int what);
    }

    [Host]
    public partial class ServiceHost : Godot.Node
    {
        [Inject] private IConfig _config;

        [Provide(ExposedTypes = new[] { typeof(IMyService) }, WaitFor = new[] { ""_config"" })]
        public MyService BuildService() => new MyService(_config);

        public override partial void _Notification(int what);
    }

    [User]
    public partial class MyUser : Godot.Node
    {
        [Inject] private IMyService _service;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(ConfigHost), typeof(ServiceHost))]
    public partial class MyScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

        var asm = E2ETestHelper.GenerateAndCompile(source);

        var scope = E2ETestHelper.Instantiate(asm, "Test.MyScope");
        var configHost = E2ETestHelper.Instantiate(asm, "Test.ConfigHost");
        var serviceHost = E2ETestHelper.Instantiate(asm, "Test.ServiceHost");
        var user = E2ETestHelper.Instantiate(asm, "Test.MyUser");

        E2ETestHelper.WireParent(configHost, scope);
        E2ETestHelper.WireParent(serviceHost, scope);
        E2ETestHelper.WireParent(user, scope);

        // ConfigHost lifecycle
        E2ETestHelper.Notify(configHost, 10);
        E2ETestHelper.Notify(configHost, 13);

        // ServiceHost lifecycle (has Inject + Provide with WaitFor)
        E2ETestHelper.Notify(serviceHost, 10);
        E2ETestHelper.Notify(serviceHost, 13);

        // User lifecycle
        E2ETestHelper.Notify(user, 10);
        E2ETestHelper.Notify(user, 13);

        // Verify injection succeeded
        var service = E2ETestHelper.GetFieldValue(user, "_service");
        Assert.NotNull(service);

        // Verify the service was built with the injected config
        var configName = service!.GetType().GetProperty("ConfigName")!.GetValue(service);
        Assert.Equal("prod", configName);
    }

    [Fact]
    public void WaitFor_MultipleDeps_AllResolvedBeforeProviding()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IConfig { string Name { get; } }
    public class Config : IConfig { public string Name => ""prod""; }

    public interface ILogger { string Level { get; } }
    public class Logger : ILogger { public string Level => ""info""; }

    public interface IMyService { }
    public class MyService : IMyService
    {
        public MyService(IConfig config, ILogger logger) { }
    }

    [Host]
    public partial class ConfigHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IConfig) })]
        public Config BuildConfig() => new Config();

        public override partial void _Notification(int what);
    }

    [Host]
    public partial class LoggerHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(ILogger) })]
        public Logger CreateLogger() => new Logger();

        public override partial void _Notification(int what);
    }

    [Host]
    public partial class ServiceHost : Godot.Node
    {
        [Inject] private IConfig _config;
        [Inject] private ILogger _logger;

        [Provide(ExposedTypes = new[] { typeof(IMyService) }, WaitFor = new[] { ""_config"", ""_logger"" })]
        public MyService BuildService() => new MyService(_config, _logger);

        public override partial void _Notification(int what);
    }

    [User]
    public partial class MyUser : Godot.Node
    {
        [Inject] private IMyService _service;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(ConfigHost), typeof(LoggerHost), typeof(ServiceHost))]
    public partial class MyScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

        var asm = E2ETestHelper.GenerateAndCompile(source);

        var scope = E2ETestHelper.Instantiate(asm, "Test.MyScope");
        var configHost = E2ETestHelper.Instantiate(asm, "Test.ConfigHost");
        var loggerHost = E2ETestHelper.Instantiate(asm, "Test.LoggerHost");
        var serviceHost = E2ETestHelper.Instantiate(asm, "Test.ServiceHost");
        var user = E2ETestHelper.Instantiate(asm, "Test.MyUser");

        E2ETestHelper.WireParent(configHost, scope);
        E2ETestHelper.WireParent(loggerHost, scope);
        E2ETestHelper.WireParent(serviceHost, scope);
        E2ETestHelper.WireParent(user, scope);

        E2ETestHelper.Notify(configHost, 10);
        E2ETestHelper.Notify(configHost, 13);
        E2ETestHelper.Notify(loggerHost, 10);
        E2ETestHelper.Notify(loggerHost, 13);
        E2ETestHelper.Notify(serviceHost, 10);
        E2ETestHelper.Notify(serviceHost, 13);
        E2ETestHelper.Notify(user, 10);
        E2ETestHelper.Notify(user, 13);

        // Verify injection succeeded
        var service = E2ETestHelper.GetFieldValue(user, "_service");
        Assert.NotNull(service);
    }

    [Fact]
    public void WaitFor_FailedDep_StillProvides()
    {
        var source = @"
using GodotSharpDI.Abstractions;
using Godot;

namespace Test
{
    public interface IDepService { }
    public class DepService : IDepService { }
    public interface IMyService { }
    public class MyService : IMyService { }

    [Host]
    public partial class FailingDepHost : Godot.Node
    {
        [Provide(ExposedTypes = new[] { typeof(IDepService) })]
        public DepService CreateDep() => throw new System.InvalidOperationException(""dep failed"");

        public override partial void _Notification(int what);
    }

    [Host]
    public partial class ServiceHost : Godot.Node
    {
        [Inject] private IDepService _dep;

        [Provide(ExposedTypes = new[] { typeof(IMyService) }, WaitFor = new[] { ""_dep"" })]
        public MyService BuildService() => new MyService();

        public override partial void _Notification(int what);
    }

    [User]
    public partial class MyUser : Godot.Node
    {
        [Inject] private IMyService _service;

        public override partial void _Notification(int what);
    }

    [Modules(typeof(FailingDepHost), typeof(ServiceHost))]
    public partial class MyScope : Godot.Node, IScope
    {
        public override partial void _Notification(int what);
    }
}";

        var asm = E2ETestHelper.GenerateAndCompile(source);

        var scope = E2ETestHelper.Instantiate(asm, "Test.MyScope");
        var failingHost = E2ETestHelper.Instantiate(asm, "Test.FailingDepHost");
        var serviceHost = E2ETestHelper.Instantiate(asm, "Test.ServiceHost");
        var user = E2ETestHelper.Instantiate(asm, "Test.MyUser");

        E2ETestHelper.WireParent(failingHost, scope);
        E2ETestHelper.WireParent(serviceHost, scope);
        E2ETestHelper.WireParent(user, scope);

        E2ETestHelper.Notify(failingHost, 10);
        E2ETestHelper.Notify(failingHost, 13);
        E2ETestHelper.Notify(serviceHost, 10);
        E2ETestHelper.Notify(serviceHost, 13);
        E2ETestHelper.Notify(user, 10);
        E2ETestHelper.Notify(user, 13);

        // WaitForCoordinator fires even when a dep fails.
        // The service should still be provided (with null dep).
        var service = E2ETestHelper.GetFieldValue(user, "_service");
        Assert.NotNull(service);
    }
}
