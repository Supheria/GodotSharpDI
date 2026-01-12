using System.Diagnostics.CodeAnalysis;
using System.Text;
using Godot;

namespace GodotSharp.DI;

//
// --- GodotSharp.DI.Abstractions ---
//

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property,
    Inherited = false
)]
public sealed class SingletonService : Attribute
{
    public Type[] ServiceTypes { get; }

    public SingletonService(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TransientService : Attribute
{
    public Type[] ServiceTypes { get; }

    public TransientService(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class DependencyAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceModuleAttribute : Attribute
{
    public Type[] Instantiate { get; set; } = [];
    public Type[] Expect { get; set; } = [];
}

public interface IServiceHost;

public interface IServiceUser;

public interface IServiceScope
{
    void RegisterService<T>(T instance)
        where T : notnull;
    void ResolveService<T>(Action<T> onResolved)
        where T : notnull;
}

public interface IServiceAware
{
    void OnServicesReady();
}

//
// --- GodotSharp.UI.Generator --- (samples)
//

// Godot Node 基类样例
public class Node
{
    public virtual void _Ready() { }

    public virtual void _Notification(int what) { }

    public Node? GetParent()
    {
        // get parent node
    }

    public void AddChild(Node child)
    {
        // add child node
    }
}

public interface IDataWriter;

public interface IDataReader;

// 非节点类型服务
[SingletonService(typeof(IDataWriter), typeof(IDataReader))] // 诊断 002：DatabaseWriter必须实现了或继承了[SingletonService]指定的所有类型或接口，否则报错
public class DatabaseWriter : IDataWriter, IDataReader { } // --- 诊断 001：标记为[SingletonService]的纯服务类型必须具有无参构造函数，否则报错

[TransientService]
public class PathFinder { } // --- 诊断 001：标记为[TransientService]的纯服务类型必须具有无参构造函数，否则报错

// 以下为用户代码
public interface ICellGetter;

public interface ICellEditor;

public interface IChunkGetter;

// --- 诊断 003：类CellManager必须为partial才能自动生成代码，否则报错
// [Service] // 诊断 004: 类CellManager实现了IServiceHost / IServiceUser / IServiceScope不能标记为[Service]，否则报错。（依赖其他服务的类型应该考虑作为节点类型使用）
// --- 诊断 005：类CellManager必须继承Node基类或任何Node派生类才能实现IServiceHost / IServiceUser / IServiceScope，否则报错
public partial class CellManager
    : Node,
        ICellGetter,
        ICellEditor,
        IServiceHost, // 诊断 006 提示：类CellManager实现了IServiceHost但未在任何属性中使用[Service]标记
        IServiceUser // 诊断 006 提示：类CellManager实现了IServiceUser但未在任何属性中使用[Dependency]标记
{
    // --- 诊断 007：类CellManager必须实现IServiceHost才能将字段或属性标记为[SingletonService]，否则报错
    // 诊断 002：CellManager必须实现了或继承了[SingletonService]指定的所有类型或接口，否则报错
    [SingletonService(typeof(ICellGetter), typeof(ICellEditor))]
    private CellManager Self => this;

    [Dependency] // 诊断 007：类CellManager必须实现IServiceUser才能将字段或属性标记为[Dependency]，否则报错
    private IChunkGetter _chunkGetter;

    public override void _Ready()
    {
        base._Ready();

        // _chunkGetter; // 诊断 007 警告：避免在_Ready中使用标记了Dependency的字段或属性。应该实现IServiceAware在OnServicesReady()中使用以确保其已经完成初始化
    }
}

// 以下为自动生成的代码
// CellManager.ServiceUtils.g.cs
partial class CellManager
{
    // 实现了 IServiceHost 或 IServiceUser 才生成
    private IServiceScope? _serviceScope; // TODO: 在 parent 更改后重新查找

    // 实现了 IServiceHost 或 IServiceUser 才生成
    private IServiceScope? GetServiceScope()
    {
        if (_serviceScope is not null)
        {
            return _serviceScope;
        }
        var parent = GetParent();
        while (parent is not null)
        {
            if (parent is IServiceScope scope)
            {
                _serviceScope = scope;
                return _serviceScope;
            }
            parent = parent.GetParent();
        }
        GD.PushError("CellManager 没有最近的 Service Scope");
        return null;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        switch (what)
        {
            // GodotObject.NotificationReady
            case 13:
                // 实现了 IServiceHost 才生成
                RegisterToServiceScopeAsHost();
                // 实现了 IServiceUser 才生成
                RegisterToServiceScopeAsUser();
                break;
            // Godot.Node.NotificationEnterTree
            case 10:
            // Godot.Node.NotificationExitTree
            case 11:
            // Godot.Node.NotificationParented
            case 18:
            //Godot.Node.NotificationUnparented
            case 19:
                // 实现了 IServiceHost 或 IServiceUser 才生成
                _serviceScope = null;
                break;
        }
    }
}

// CellManager.IServiceHost.g.cs
// 实现了 IServiceHost 才生成
partial class CellManager
{
    private void RegisterServices(IServiceScope scope)
    {
        // 注册为 SingletonService 特性指定的类型
        scope.RegisterService<ICellGetter>(Self);
        scope.RegisterService<ICellEditor>(Self);
    }

    private void RegisterToServiceScopeAsHost()
    {
        var scope = GetServiceScope();
        if (scope is not null)
        {
            RegisterServices(scope);
        }
    }
}

// CellManager.ServiceUser.g.cs
// 实现了 IServiceUser 才生成
partial class CellManager
{
    private void ResolveServices(IServiceScope scope)
    {
        scope.ResolveService<IChunkGetter>(service =>
        {
            _chunkGetter = service;
        });
    }

    private void RegisterToServiceScopeAsUser()
    {
        var scope = GetServiceScope();
        if (scope is not null)
        {
            ResolveServices(scope);
        }
    }
}

// 以下为用户代码
public partial class ChunkManager : Node, IChunkGetter, IServiceHost, IServiceUser, IServiceAware
{
    [SingletonService(typeof(IChunkGetter))]
    private ChunkManager Self => this;

    [Dependency]
    private ICellGetter _cellGetter;

    [Dependency]
    private IDataWriter _dataWriter;

    void IServiceAware.OnServicesReady() { }
}

// ChunkManager.ServiceUtils.g.cs
partial class ChunkManager
{
    // 实现了 IServiceHost 或 IServiceUser 才生成
    private IServiceScope? _serviceScope;

    // 实现了 IServiceHost 或 IServiceUser 才生成
    private IServiceScope? GetServiceScope()
    {
        if (_serviceScope is not null)
        {
            return _serviceScope;
        }
        var parent = GetParent();
        while (parent is not null)
        {
            if (parent is IServiceScope scope)
            {
                _serviceScope = scope;
                return _serviceScope;
            }
            parent = parent.GetParent();
        }
        GD.PushError("ChunkManager 没有最近的 Service Scope");
        return null;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        switch (what)
        {
            // Godot.Node.NotificationReady
            case 13:
                // 实现了 IServiceHost 才生成
                RegisterToServiceScopeAsHost();
                // 实现了 IServiceUser 才生成
                RegisterToServiceScopeAsUser();
                break;
            // Godot.Node.NotificationEnterTree
            case 10:
            // Godot.Node.NotificationExitTree
            case 11:
            // Godot.Node.NotificationParented
            case 18:
            //Godot.Node.NotificationUnparented
            case 19:
                // 实现了 IServiceHost 或 IServiceUser 才生成
                _serviceScope = null;
                break;
        }
    }
}

// 以下为自动生成的代码
// ChunkManager.ServiceHost.g.cs
// 实现了 IServiceHost 才生成
partial class ChunkManager
{
    private void RegisterServices(IServiceScope scope)
    {
        // 注册为 SingletonService 特性指定的类型
        scope.RegisterService<IChunkGetter>(Self);
    }

    private void RegisterToServiceScopeAsHost()
    {
        var scope = GetServiceScope();
        if (scope is not null)
        {
            RegisterServices(scope);
        }
    }
}

// ChunkManager.ServiceUser.g.cs
// 实现了 IServiceUser 才生成
partial class ChunkManager
{
    // 实现了IInjectionAware才生成
    private readonly HashSet<Type> _unresolvedDependencies = new()
    {
        typeof(ICellGetter),
        typeof(IDataWriter),
    };

    // 实现了IInjectionAware才生成
    private void OnDependencyResolved(Type type)
    {
        _unresolvedDependencies.Remove(type);
        if (_unresolvedDependencies.Count == 0)
        {
            ((IServiceAware)this).OnServicesReady();
        }
    }

    private void ResolveServices(IServiceScope scope)
    {
        scope.ResolveService<ICellGetter>(service =>
        {
            _cellGetter = service;
            // 实现了IInjectionAware才生成
            OnDependencyResolved(typeof(ICellGetter));
        });

        scope.ResolveService<IDataWriter>(service =>
        {
            _dataWriter = service;
            // 实现了IInjectionAware才生成
            OnDependencyResolved(typeof(IDataWriter));
        });
    }

    private void RegisterToServiceScopeAsUser()
    {
        var scope = GetServiceScope();
        if (scope is not null)
        {
            ResolveServices(scope);
        }
    }
}

// TODO: 自动扫描命名空间及子命名空间下的所有 SingletonService 和 TransientService。SingletonService 如果标记在类名上由该 Scope 自行创建实例，如果标记在属性上则不创建
// 以下为用户代码
[ServiceModule(
    Instantiate = [typeof(DatabaseWriter), typeof(PathFinder)],
    Expect = [typeof(CellManager), typeof(ChunkManager)]
)] // --- 诊断008：MyServiceScope实现IServiceScope必须指定ServiceModule标签中的服务类型约束，否则报错
public partial class MyServiceScope
    : Node,
        IServiceScope // --- 诊断009：IServiceScope不能与IServiceHost或IServiceUser同时实现
{ }

// 以下为自动生成的代码
// ChunkManager.ServiceUtils.g.cs
partial class MyServiceScope
{
    // 实现了IServiceScope才生成
    private IServiceScope? _parentScope;

    // 实现了IServiceScope才生成
    private IServiceScope? GetParentScope()
    {
        if (_parentScope is not null)
        {
            return _parentScope;
        }
        var parent = GetParent();
        while (parent is not null)
        {
            if (parent is IServiceScope scope)
            {
                _parentScope = scope;
                return _parentScope;
            }
            parent = parent.GetParent();
        }
        return null;
    }

    // 实现了IServiceScope才生成
    private void RegisterServiceInstance<T>(T instance)
        where T : notnull
    {
        var type = typeof(T);
        _singletons.Add(type, instance);
        if (!_waiters.TryGetValue(type, out var waiterList))
        {
            return;
        }
        foreach (var callback in waiterList)
        {
            callback.Invoke(instance);
        }
        _waiters.Remove(type);
    }

    // 实现了IServiceScope才生成
    private void RegisterServiceInstances()
    {
        var databaseWriter = new DatabaseWriter();

        // 在此注册 SingletonService 单例服务
        // 注册为 SingletonService 特性指定的类型
        // 如果没有指定服务类型则注册为原类型
        RegisterServiceInstance<IDataWriter>(databaseWriter);
        RegisterServiceInstance<IDataReader>(databaseWriter);
    }

    // 实现了IServiceScope才生成
    private void CheckWaitList()
    {
        if (_waiters.Count == 0)
        {
            return;
        }
        var waitTypes = new StringBuilder().AppendJoin(',', _waiters.Select(w => w.Key));
        throw new Exception($"存在未完成注入的服务类型：{waitTypes}");
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        switch (what)
        {
            // Godot.Node.NotificationReady
            case 13:
                // 实现了IServiceScope才生成
                RegisterServiceInstances();
                // 实现了IServiceScope才生成
                CheckWaitList();
                break;
            // Godot.Node.NotificationEnterTree
            case 10:
            // Godot.Node.NotificationExitTree
            case 11:
            // Godot.Node.NotificationParented
            case 18:
            //Godot.Node.NotificationUnparented
            case 19:
                // 实现了IServiceScope才生成
                _parentScope = null;
                break;
        }
    }
}

// MyContext.ServiceScope.g.cs
// 实现了IServiceScope才生成
partial class MyServiceScope
{
    private static readonly HashSet<Type> ExpectTypes = new()
    {
        // 注册为 SingletonService 特性指定的类型
        // 如果没有指定服务类型则注册为原类型

        // DatabaseWriter 提供的单例服务
        typeof(IDataWriter),
        typeof(IDataWriter),
        // CellManager 提供的单例服务
        typeof(ICellGetter),
        typeof(ICellEditor),
        // ChunkManager 提供的单例服务
        typeof(IChunkGetter),
    };

    private static readonly Dictionary<Type, Func<object>> TransientFactories = new()
    {
        // 在此创建 TransientService 瞬态服务
        // 创建为 SingletonService 特性指定的类型
        // 如果没有指定服务类型则注册为原类型
        [typeof(PathFinder)] = () => new PathFinder(),
    };

    private readonly Dictionary<Type, object> _singletons = new();
    private readonly Dictionary<Type, List<Action<object>>> _waiters = new();

    void IServiceScope.ResolveService<T>(Action<T> onResolved)
    {
        var type = typeof(T);
        if (TransientFactories.TryGetValue(type, out var factory))
        {
            onResolved.Invoke((T)factory.Invoke());
            return;
        }
        if (_singletons.TryGetValue(type, out var singleton))
        {
            onResolved.Invoke((T)singleton);
            return;
        }
        if (!ExpectTypes.Contains(type))
        {
            var parent = GetParentScope();
            if (parent is not null)
            {
                parent.ResolveService(onResolved);
                return;
            }
            GD.PushError($"直到根 Service Scope 都无法找到服务类型：{type.Name}");
            return;
        }
        if (!_waiters.TryGetValue(type, out var waiters))
        {
            waiters = [];
            _waiters[type] = waiters;
        }
        waiters.Add(obj => onResolved.Invoke((T)obj));
    }

    void IServiceScope.RegisterService<T>(T instance)
    {
        var type = typeof(T);
        if (!ExpectTypes.Contains(type))
        {
            var parent = GetParentScope();
            if (parent is not null)
            {
                parent.RegisterService(instance);
                return;
            }
            GD.PushError($"直到根 Service Scope 都无法注册服务类型：{type.Name}");
            return;
        }
        if (!_singletons.TryAdd(type, instance))
        {
            throw new Exception($"重复注册类型: {type.Name}。");
        }
        if (!_waiters.TryGetValue(type, out var waiterList))
        {
            return;
        }
        foreach (var callback in waiterList)
        {
            callback.Invoke(instance);
        }
        _waiters.Remove(type);
    }
}
