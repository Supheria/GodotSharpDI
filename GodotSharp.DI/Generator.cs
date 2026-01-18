using System.Text;

namespace GodotSharp.DI;

//
// --- 非节点类型服务 ---
//

[Singleton(typeof(IDataWriter), typeof(IDataReader))]
public partial class DatabaseWriter : IDataWriter, IDataReader, IDisposable
{
    void IDataReader.Read() { }

    public void Dispose() { }
}

// - generated code begin -

partial class DatabaseWriter // DatabaseWriter.DI.Factory.g.cs
{
    // 仅 Singleton 生成此函数名
    public static void CreateService(IScope scope, Action<object, IScope> onCreated)
    {
        // 仅当注入构造函数参数等于0时按照如下模板生成

        var instance = new DatabaseWriter();
        onCreated.Invoke(instance, scope);
    }
}

// - generated code end -

[Transient(typeof(IPathFinder), typeof(IAStartPathFinder))]
public partial class PathFinder : IPathFinder, IAStartPathFinder
{
    private readonly IDataWriter _dataWriter;
    private readonly IDataReader _dataReader;

    [InjectConstructor]
    private PathFinder(IDataWriter dataWriter, IDataReader dataReader)
    {
        _dataWriter = dataWriter;
        _dataReader = dataReader;
    }
}

// - generated code begin -

// 标记为 Singleton 或 Transient 才生成
partial class PathFinder // MovementManager.DI.Factory.g.cs
{
    // 仅 Transient 生成此函数名
    public static void CreateService(IScope scope, Action<object> onCreated)
    {
        // 仅当注入构造函数参数大于0时按照如下模板生成

        // 记录总共需要的依赖数量
        var remaining = 2;

        // 声明所有注入构造函数中的参数类型临时变量
        IDataWriter? p0 = null;
        IDataReader? p1 = null;

        // 解析所有注入构造函数中的依赖参数
        scope.ResolveDependency<IDataWriter>(dependency =>
        {
            p0 = dependency;
            TryCreate();
        });
        scope.ResolveDependency<IDataReader>(dependency =>
        {
            p1 = dependency;
            TryCreate();
        });

        return;

        void TryCreate()
        {
            if (--remaining == 0)
            {
                var instance = new PathFinder(p0!, p1!);
                onCreated.Invoke(instance);
            }
        }
    }
}

// - generated code end -

//
// --- 非节点类型 host 和 user ---
//

[Host]
[User]
public partial class MovementManager : IPathProvider, IPathGenerator, IServicesReady
{
    [Singleton(typeof(IPathProvider), typeof(IPathGenerator))]
    private MovementManager Self => this;

    [Inject]
    private IPathFinder _pathFinder;

    [Inject]
    private IAStartPathFinder _aStartPathFinder;

    void IServicesReady.OnServicesReady() { }
}

// - generated code begin -

// 标记为 Host 或 User 才生成
partial class MovementManager // MovementManager.DI.g.cs
{
    // 非节点类型 Host 或 User 才生成
    public void AttachToScope(IScope scope)
    {
        // 标记为 Host 才生成
        AttachHostServices(scope);
        // 标记为 User 才生成
        ResolveUserDependencies(scope);
    }

    // 非节点类型 Host 或 User 才生成
    public void UnattachToScope(IScope scope)
    {
        // 标记为 Host 才生成
        UnattachHostServices(scope);
    }
}

// 标记为 Host 才生成
partial class MovementManager // MovementManager.DI.Host.g.cs
{
    /// <summary>
    /// 注册所有标记为 [Singleton] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void AttachHostServices(IScope scope)
    {
        // 注册为 Singleton 特性指定的类型
        scope.RegisterService<IPathProvider>(Self);
        scope.RegisterService<IPathGenerator>(Self);
    }

    /// <summary>
    /// 取消注册所有标记为 [Singleton] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private static void UnattachHostServices(IScope scope)
    {
        // 取消注册 Singleton 特性指定的类型
        scope.UnregisterService<IPathProvider>();
        scope.UnregisterService<IPathGenerator>();
    }
}

// 标记为 User 才生成
partial class MovementManager // MovementManager.DI.User.g.cs
{
    // 实现了 IServicesReady 才生成
    private readonly object _dependencyLock = new();
    private readonly HashSet<Type> _unresolvedDependencies = new()
    {
        // 列举字段或属性中所有标记为 [Inject] 的类型
        typeof(IPathFinder),
        typeof(IAStartPathFinder),
    };

    // 实现了 IServicesReady 才生成
    private void OnDependencyResolved<T>()
    {
        lock (_dependencyLock)
        {
            _unresolvedDependencies.Remove(typeof(T));
            if (_unresolvedDependencies.Count == 0)
            {
                ((IServicesReady)this).OnServicesReady();
            }
        }
    }

    /// <summary>
    /// 解析所有标记为 [Inject] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void ResolveUserDependencies(IScope scope)
    {
        scope.ResolveDependency<IPathFinder>(dependency =>
        {
            _pathFinder = dependency;
            // 实现了 IServicesReady 才生成
            OnDependencyResolved<IPathFinder>();
        });
        scope.ResolveDependency<IAStartPathFinder>(dependency =>
        {
            _aStartPathFinder = dependency;
            // 实现了 IServicesReady 才生成
            OnDependencyResolved<IAStartPathFinder>();
        });
    }
}

// - generated code end -

//
// --- 节点类型 host 和 user ---
//

[Host]
[User]
public partial class CellManager : Godot.Node, ICellGetter, ICellEditor, IServicesReady
{
    [Singleton(typeof(ICellGetter), typeof(ICellEditor))]
    private CellManager Self => this;

    [Inject]
    private IDataReader _dataReader;

    [Inject]
    private IDataWriter _dataWriter;

    private readonly MovementManager _movementManager = new();

    public void OnServicesReady() { }
}

// - generated code begin -

// 标记为 Host 或 User 才生成
partial class CellManager // CellManager.DI.g.cs
{
    // 节点类型的 Host 或 User 才生成
    private IScope? _serviceScope;

    // 节点类型的 Host 或 User 才生成
    private IScope? GetServiceScope()
    {
        if (_serviceScope is not null)
        {
            return _serviceScope;
        }
        var parent = GetParent();
        while (parent is not null)
        {
            if (parent is IScope scope)
            {
                _serviceScope = scope;
                return _serviceScope;
            }
            parent = parent.GetParent();
        }
        Godot.GD.PushError("CellManager 没有最近的 Service Scope");
        return null;
    }

    // 节点类型的 Host 或 User 才生成
    private void AttachToScope()
    {
        var scope = GetServiceScope();
        if (scope is null)
        {
            return;
        }
        // 标记为 Host 才生成
        AttachHostServices(scope);
        // 标记为 User 才生成
        ResolveUserDependencies(scope);
        // 处理任何标记为 [Host] 或 [User] 的类型成员
        _movementManager.AttachToScope(scope);
    }

    // 节点类型的 Host 或 User 才生成
    private void UnattachToScope()
    {
        var scope = GetServiceScope();
        if (scope is null)
        {
            return;
        }
        UnattachHostServices(scope);
        // 处理任何标记为 [Host] 或 [User] 的类型成员
        _movementManager.UnattachToScope(scope);
    }

    // 节点类型的 Host 或 User 才生成
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

// 标记为 Host 才生成
partial class CellManager // CellManager.DI.Host.g.cs
{
    /// <summary>
    /// 注册所有标记为 [Singleton] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void AttachHostServices(IScope scope)
    {
        // 注册为 Singleton 特性指定的类型
        scope.RegisterService<ICellGetter>(Self);
        scope.RegisterService<ICellEditor>(Self);
    }

    /// <summary>
    /// 取消注册所有标记为 [Singleton] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void UnattachHostServices(IScope scope)
    {
        // 取消注册 Singleton 特性指定的类型
        scope.UnregisterService<ICellGetter>();
        scope.UnregisterService<ICellEditor>();
    }
}

// 标记为 User 才生成
partial class CellManager // CellManager.DI.User.g.cs
{
    // 实现了 IServicesReady 才生成
    private readonly object _dependencyLock = new();
    private readonly HashSet<Type> _unresolvedDependencies = new()
    {
        // 列举字段或属性中所有标记为 [Inject] 的类型
        typeof(IDataReader),
        typeof(IDataWriter),
    };

    // 实现了 IServicesReady 才生成
    private void OnDependencyResolved<T>()
    {
        lock (_dependencyLock)
        {
            _unresolvedDependencies.Remove(typeof(T));
            if (_unresolvedDependencies.Count == 0)
            {
                ((IServicesReady)this).OnServicesReady();
            }
        }
    }

    /// <summary>
    /// 解析所有标记为 [Inject] 的字段或属性
    /// </summary>
    /// <param name="scope"></param>
    private void ResolveUserDependencies(IScope scope)
    {
        scope.ResolveDependency<IDataReader>(dependency =>
        {
            _dataReader = dependency;
            // 实现了 IServicesReady 才生成
            OnDependencyResolved<IDataReader>();
        });
        scope.ResolveDependency<IDataWriter>(dependency =>
        {
            _dataWriter = dependency;
            // 实现了 IServicesReady 才生成
            OnDependencyResolved<IDataWriter>();
        });
    }
}

// - generated code end -

[Modules(
    Instantiate = [typeof(DatabaseWriter), typeof(PathFinder)],
    Expect = [typeof(CellManager), typeof(MovementManager)]
)]
public partial class MyScope : Godot.Node, IScope { }

// - generated code begin -

// 实现了 IScope 才生成
partial class MyScope // MyScope.DI.g.cs
{
    private IScope? _parentScope;

    private IScope? GetParentScope()
    {
        if (_parentScope is not null)
        {
            return _parentScope;
        }
        var parent = GetParent();
        while (parent is not null)
        {
            if (parent is IScope scope)
            {
                _parentScope = scope;
                return _parentScope;
            }
            parent = parent.GetParent();
        }
        return null;
    }

    /// <summary>
    /// 实例化所有 Scope 约束的单例服务
    /// </summary>
    private void InstantiateScopeSingletons()
    {
        DatabaseWriter.CreateService(
            this,
            (instance, scope) =>
            {
                _scopeSingletonInstances.Add(instance);
                // 在此注册 Singleton 单例服务
                // 注册为 Singleton 特性指定的类型
                // 如果没有指定服务类型则注册为原类型
                scope.RegisterService((IDataWriter)instance);
                scope.RegisterService((IDataReader)instance);
            }
        );
    }

    /// <summary>
    /// 释放所有 Scope 约束的单例服务实例
    /// </summary>
    private void DisposeScopeSingletons()
    {
        foreach (var instance in _scopeSingletonInstances)
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
        _scopeSingletonInstances.Clear();
        _singletonServices.Clear();
    }

    private void CheckWaitList()
    {
        if (_waiters.Count == 0)
        {
            return;
        }
        var sb = new StringBuilder();
        var first = true;
        foreach (var type in _waiters.Keys)
        {
            if (!first)
            {
                sb.Append(',');
            }
            sb.Append(type.Name);
            first = false;
        }
        Godot.GD.PushError($"存在未完成注入的服务类型：{sb}");
        _waiters.Clear();
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

// 实现了IServiceScope才生成
partial class MyScope // MyContext.DI.Scope.g.cs
{
    private static readonly HashSet<Type> SingletonServiceTypes = new()
    {
        // 注册为 Singleton 特性指定的类型
        // 如果没有指定服务类型则注册为原类型

        // DatabaseWriter 提供的单例服务
        typeof(IDataWriter),
        typeof(IDataReader),
        // CellManager 提供的单例服务
        typeof(ICellGetter),
        typeof(ICellEditor),
        // MovementManager 提供的单例服务
        typeof(IPathProvider),
        typeof(IPathGenerator),
    };

    private static readonly Dictionary<Type, Action<IScope, Action<object>>> TransientFactories =
        new()
        {
            // 在此创建 Transient 瞬态服务
            // 创建为 Singleton 特性指定的类型
            // 如果没有指定服务类型则注册为原类型
            [typeof(PathFinder)] = PathFinder.CreateService,
        };

    private readonly Dictionary<Type, object> _singletonServices = new();
    private readonly HashSet<object> _scopeSingletonInstances = new();
    private readonly Dictionary<Type, List<Action<object>>> _waiters = new();

    void IScope.ResolveDependency<T>(Action<T> onResolved)
    {
        var type = typeof(T);
        if (TransientFactories.TryGetValue(type, out var factory))
        {
            factory.Invoke(this, instance => onResolved.Invoke((T)instance));
            return;
        }
        if (!SingletonServiceTypes.Contains(type))
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
        if (_singletonServices.TryGetValue(type, out var singleton))
        {
            onResolved.Invoke((T)singleton);
            return;
        }
        if (!_waiters.TryGetValue(type, out var waiterList))
        {
            waiterList = new List<Action<object>>();
            _waiters[type] = waiterList;
        }
        waiterList.Add(obj => onResolved.Invoke((T)obj));
    }

    void IScope.RegisterService<T>(T instance)
    {
        var type = typeof(T);
        if (!SingletonServiceTypes.Contains(type))
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
        if (!_singletonServices.TryAdd(type, instance))
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

    void IScope.UnregisterService<T>()
    {
        var type = typeof(T);
        if (!SingletonServiceTypes.Contains(type))
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
        _singletonServices.Remove(type);
    }
}

// - generated code end -

// 生成器中自动扫描，将同一命名空间及其子命名空间下的所有 Singleton、Transient 和 Host 与 Scope 归纳在同一类别。
// 生成器根据该类别自动补全 Scope 的 InstantiateScopeSingletons()、SingletonTypes、TransientFactories

[AutoModules]
public partial class AutoScanScope : Godot.Node, IScope { }
