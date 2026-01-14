using System.Text;

namespace GodotSharp.DI;

//
// --- 非节点类型服务 ---
//

[SingletonService(typeof(IDataWriter), typeof(IDataReader))]
public partial class DatabaseWriter : IDataWriter, IDataReader, IDisposable
{
    void IDataReader.Read() { }

    public void Dispose() { }
}

// - generated code begin -

partial class DatabaseWriter // DatabaseWriter.ServiceFactory.g.cs
{
    public static void CreateService(IServiceScope scope, Action<object> onCreated)
    {
        // 仅当注入构造函数参数等于0时按照如下模板生成

        var instance = new DatabaseWriter();
        onCreated.Invoke(instance);
    }
}

// - generated code end -

[TransientService(typeof(IPathFinder), typeof(IAStartPathFinder))]
public partial class PathFinder : IPathFinder, IAStartPathFinder
{
    private readonly IDataWriter _dataWriter;
    private readonly IDataReader _dataReader;

    [InjectionConstructor]
    private PathFinder(IDataWriter dataWriter, IDataReader dataReader)
    {
        _dataWriter = dataWriter;
        _dataReader = dataReader;
    }
}

// - generated code begin -

// 标记为 SingletonService 或 TransientService 才生成
partial class PathFinder // MovementManager.ServiceFactory.g.cs
{
    public static void CreateService(IServiceScope scope, Action<object> onCreated)
    {
        // 仅当注入构造函数参数大于0时按照如下模板生成

        // 记录总共需要的依赖数量
        var remaining = 2;

        // 声明所有注入构造函数中的参数类型临时变量
        IDataWriter? iDataWriter0 = null;
        IDataReader? iDataReader1 = null;

        // 解析所有注入构造函数中的依赖参数
        scope.ResolveDependency<IDataWriter>(dependency =>
        {
            iDataWriter0 = dependency;
            if (Interlocked.Decrement(ref remaining) == 0)
            {
                Create();
            }
        });
        scope.ResolveDependency<IDataReader>(dependency =>
        {
            iDataReader1 = dependency;
            if (Interlocked.Decrement(ref remaining) == 0)
            {
                Create();
            }
        });

        return;

        void Create()
        {
            var instance = new PathFinder(iDataWriter0!, iDataReader1!);
            onCreated.Invoke(instance);
        }
    }
}

// - generated code end -

//
// --- 非节点类型 host 和 user ---
//

[ServiceHost]
[ServiceUser]
public partial class MovementManager : IPathProvider, IPathGenerator, IServiceAware
{
    [SingletonService(typeof(IPathProvider), typeof(IPathGenerator))]
    private MovementManager Self => this;

    [Dependency]
    private IPathFinder _pathFinder;

    [Dependency]
    private IAStartPathFinder _aStartPathFinder;

    void IServiceAware.OnServicesReady() { }
}

// - generated code begin -

// 标记为 ServiceHost 或 ServiceUser 才生成
partial class MovementManager // MovementManager.ServiceUtils.g.cs
{
    // 非节点类型 ServiceHost 或 ServiceUser 才生成
    public void AttachToScope(IServiceScope scope)
    {
        // 标记为 ServiceHost 才生成
        AttachHostServices(scope);
        // 标记为 ServiceUser 才生成
        ResolveUserDependencies(scope);
    }

    // 非节点类型 ServiceHost 或 ServiceUser 才生成
    public void UnattachToScope(IServiceScope scope)
    {
        // 标记为 ServiceHost 才生成
        UnattachHostServices(scope);
    }
}

// 标记为 ServiceHost 才生成
partial class MovementManager // MovementManager.ServiceHost.g.cs
{
    /// <summary>
    /// 注册所有标记为 [SingletonService] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void AttachHostServices(IServiceScope scope)
    {
        // 注册为 SingletonService 特性指定的类型
        scope.RegisterService<IPathProvider>(Self);
        scope.RegisterService<IPathGenerator>(Self);
    }

    /// <summary>
    /// 取消注册所有标记为 [SingletonService] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private static void UnattachHostServices(IServiceScope scope)
    {
        // 取消注册 SingletonService 特性指定的类型
        scope.UnregisterService<IPathProvider>();
        scope.UnregisterService<IPathGenerator>();
    }
}

// 标记为 ServiceUser 才生成
partial class MovementManager // MovementManager.ServiceUser.g.cs
{
    // 实现了IInjectionAware才生成
    private readonly HashSet<Type> _unresolvedDependencies = new()
    {
        // 列举字段或属性中所有标记为 [Dependency] 的类型
        typeof(IPathFinder),
        typeof(IAStartPathFinder),
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

    /// <summary>
    /// 解析所有标记为 [Dependency] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void ResolveUserDependencies(IServiceScope scope)
    {
        scope.ResolveDependency<IPathFinder>(dependency =>
        {
            _pathFinder = dependency;
            // 实现了IInjectionAware才生成
            OnDependencyResolved(typeof(IPathFinder));
        });
        scope.ResolveDependency<IAStartPathFinder>(dependency =>
        {
            _aStartPathFinder = dependency;
            // 实现了IInjectionAware才生成
            OnDependencyResolved(typeof(IAStartPathFinder));
        });
    }
}

// - generated code end -

//
// --- 节点类型 host 和 user ---
//

[ServiceHost]
[ServiceUser]
public partial class CellManager : Godot.Node, ICellGetter, ICellEditor, IServiceAware
{
    [SingletonService(typeof(ICellGetter), typeof(ICellEditor))]
    private CellManager Self => this;

    [Dependency]
    private IDataReader _dataReader;

    [Dependency]
    private IDataWriter _dataWriter;

    private readonly MovementManager _movementManager = new();

    public void OnServicesReady() { }
}

// - generated code begin -

// 标记为 ServiceHost 或 ServiceUser 才生成
partial class CellManager // CellManager.ServiceUtils.g.cs
{
    // 节点类型的 ServiceHost 或 ServiceUser 才生成
    private IServiceScope? _serviceScope;

    // 节点类型的 ServiceHost 或 ServiceUser 才生成
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
        Godot.GD.PushError("CellManager 没有最近的 Service Scope");
        return null;
    }

    // 节点类型的 ServiceHost 或 ServiceUser 才生成
    private void AttachToScope()
    {
        var scope = GetServiceScope();
        if (scope is null)
        {
            return;
        }
        // 标记为 ServiceHost 才生成
        AttachHostServices(scope);
        // 标记为 ServiceUser 才生成
        ResolveUserDependencies(scope);
        // 处理任何标记为 [ServiceHost] 或 [ServiceUser] 的类型成员
        _movementManager.AttachToScope(scope);
    }

    // 节点类型的 ServiceHost 或 ServiceUser 才生成
    private void UnattachToScope()
    {
        var scope = GetServiceScope();
        if (scope is null)
        {
            return;
        }
        UnattachHostServices(scope);
        // 处理任何标记为 [ServiceHost] 或 [ServiceUser] 的类型成员
        _movementManager.UnattachToScope(scope);
    }

    // 节点类型的 ServiceHost 或 ServiceUser 才生成
    public override void _Notification(int what)
    {
        base._Notification(what);

        switch ((long)what)
        {
            case NotificationEnterTree:
            {
                AttachToScope();
                break;
            }
            case NotificationExitTree:
            {
                UnattachToScope();
                _serviceScope = null;
                break;
            }
        }
    }
}

// CellManager.IServiceHost.g.cs
// 标记为 ServiceHost 才生成
partial class CellManager
{
    /// <summary>
    /// 注册所有标记为 [SingletonService] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void AttachHostServices(IServiceScope scope)
    {
        // 注册为 SingletonService 特性指定的类型
        scope.RegisterService<ICellGetter>(Self);
        scope.RegisterService<ICellEditor>(Self);
    }

    /// <summary>
    /// 取消注册所有标记为 [SingletonService] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void UnattachHostServices(IServiceScope scope)
    {
        // 取消注册 SingletonService 特性指定的类型
        scope.UnregisterService<ICellGetter>();
        scope.UnregisterService<ICellEditor>();
    }
}

// CellManager.ServiceUser.g.cs
// 标记为 ServiceUser 才生成
partial class CellManager
{
    // 实现了IInjectionAware才生成
    private readonly HashSet<Type> _unresolvedDependencies = new()
    {
        // 列举字段或属性中所有标记为 [Dependency] 的类型
        typeof(IDataReader),
        typeof(IDataWriter),
    };

    // 实现了IInjectionAware才生成
    private void OnDependencyResolved(Type type)
    {
        lock (_unresolvedDependencies)
        {
            _unresolvedDependencies.Remove(type);
            if (_unresolvedDependencies.Count == 0)
            {
                ((IServiceAware)this).OnServicesReady();
            }
        }
    }

    /// <summary>
    /// 解析所有标记为 [Dependency] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void ResolveUserDependencies(IServiceScope scope)
    {
        scope.ResolveDependency<IDataReader>(dependency =>
        {
            _dataReader = dependency;
            // 实现了IInjectionAware才生成
            OnDependencyResolved(typeof(IDataReader));
        });
        scope.ResolveDependency<IDataWriter>(dependency =>
        {
            _dataWriter = dependency;
            // 实现了IInjectionAware才生成
            OnDependencyResolved(typeof(IDataWriter));
        });
    }
}

// - generated code end -

[ServiceModules(
    Instantiate = [typeof(DatabaseWriter), typeof(PathFinder)],
    Expect = [typeof(CellManager), typeof(MovementManager)]
)]
public partial class MyServiceScope : Godot.Node, IServiceScope { }

// - generated code begin -

// 实现了 IServiceScope 才生成
partial class MyServiceScope // ChunkManager.ServiceUtils.g.cs
{
    private IServiceScope? _parentScope;

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

    private void RegisterScopeSingleton<T>(T instance)
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

    /// <summary>
    /// 实例化所有 Scope 约束的单例服务
    /// </summary>
    private void InstantiateScopeSingletons()
    {
        DatabaseWriter.CreateService(
            this,
            instance =>
            {
                _singletonInstances.Add(instance);
                // 在此注册 SingletonService 单例服务
                // 注册为 SingletonService 特性指定的类型
                // 如果没有指定服务类型则注册为原类型
                RegisterScopeSingleton((IDataWriter)instance);
                RegisterScopeSingleton((IDataReader)instance);
            }
        );
    }

    /// <summary>
    /// 释放所有 Scope 约束的单例服务实例
    /// </summary>
    private void DisposeScopeSingletons()
    {
        foreach (var instance in _singletonInstances)
        {
            if (instance is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Godot.GD.PushError(ex);
                }
            }
        }
        _singletonInstances.Clear();
        _singletons.Clear();
    }

    private void CheckWaitList()
    {
        if (_waiters.Count == 0)
        {
            return;
        }
        var waitTypes = new StringBuilder().AppendJoin(',', _waiters.Keys);
        Godot.GD.PushError($"存在未完成注入的服务类型：{waitTypes}");
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        switch ((long)what)
        {
            case NotificationEnterTree:
            case NotificationExitTree:
            {
                _parentScope = null;
                break;
            }
            case NotificationReady:
            {
                InstantiateScopeSingletons();
                CheckWaitList();
                break;
            }
            case NotificationPredelete:
            {
                DisposeScopeSingletons();
                break;
            }
        }
    }
}

// MyContext.ServiceScope.g.cs
// 实现了IServiceScope才生成
partial class MyServiceScope
{
    private static readonly HashSet<Type> SingletonTypes = new()
    {
        // 注册为 SingletonService 特性指定的类型
        // 如果没有指定服务类型则注册为原类型

        // DatabaseWriter 提供的单例服务
        typeof(IDataWriter),
        typeof(IDataWriter),
        // CellManager 提供的单例服务
        typeof(ICellGetter),
        typeof(ICellEditor),
        // MovementManager 提供的单例服务
        typeof(IPathProvider),
        typeof(IPathGenerator),
    };

    private static readonly Dictionary<
        Type,
        Action<IServiceScope, Action<object>>
    > TransientFactories = new()
    {
        // 在此创建 TransientService 瞬态服务
        // 创建为 SingletonService 特性指定的类型
        // 如果没有指定服务类型则注册为原类型
        [typeof(PathFinder)] = PathFinder.CreateService,
    };

    private readonly Dictionary<Type, object> _singletons = new();
    private readonly HashSet<object> _singletonInstances = new();
    private readonly Dictionary<Type, List<Action<object>>> _waiters = new();

    void IServiceScope.ResolveDependency<T>(Action<T> onResolved)
    {
        var type = typeof(T);
        if (TransientFactories.TryGetValue(type, out var factory))
        {
            factory.Invoke(this, instance => onResolved.Invoke((T)instance));
            return;
        }
        if (!SingletonTypes.Contains(type))
        {
            var parent = GetParentScope();
            if (parent is not null)
            {
                parent.ResolveDependency(onResolved);
                return;
            }
            Godot.GD.PushError($"直到根 Service Scope 都无法找到服务类型：{type.Name}");
            return;
        }
        if (_singletons.TryGetValue(type, out var singleton))
        {
            onResolved.Invoke((T)singleton);
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
        if (!SingletonTypes.Contains(type))
        {
            var parent = GetParentScope();
            if (parent is not null)
            {
                parent.RegisterService(instance);
                return;
            }
            Godot.GD.PushError($"直到根 Service Scope 都无法注册服务类型：{type.Name}");
            return;
        }
        if (!_singletons.TryAdd(type, instance))
        {
            Godot.GD.PushError($"重复注册类型: {type.Name}。");
        }
        if (_waiters.Remove(type, out var waiterList))
        {
            foreach (var callback in waiterList)
            {
                callback.Invoke(instance);
            }
        }
    }

    void IServiceScope.UnregisterService<T>()
    {
        var type = typeof(T);
        if (!SingletonTypes.Contains(type))
        {
            var parent = GetParentScope();
            if (parent is not null)
            {
                parent.UnregisterService<T>();
                return;
            }
            Godot.GD.PushError($"直到根 Service Scope 都无法注册服务类型：{type.Name}");
            return;
        }
        _singletons.Remove(type);
    }
}

// - generated code end -

// 生成器中自动扫描，将同一命名空间及其子命名空间下的所有 SingletonService、TransientService 和 ServiceHost 与 ServiceScope 归纳在同一类别。
// 生成器根据该类别自动补全 ServiceScope 的 InstantiateScopeSingletons()、SingletonTypes、TransientFactories
[AutoScanServiceModules]
public partial class AutoScanServiceScope : Godot.Node, IServiceScope { }
