/*

using System.Text;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI;

//
// --- 示例接口 ---
//

public interface IDataWriter { }

public interface IDataReader
{
    void Read();
}

public interface IPathFinder { }

public interface IAStartPathFinder { }

public interface IPathProvider { }

public interface IPathGenerator { }

public interface ICellGetter { }

public interface ICellEditor { }

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

// 标记为 Singleton 才生成
partial class DatabaseWriter // DatabaseWriter.DI.Service.g.cs
{
    public static void CreateService(IScope scope, Action<object, IScope> onCreated)
    {
        // 仅当注入构造函数参数等于0时按照如下模板生成

        var instance = new DatabaseWriter();
        onCreated.Invoke(instance, scope);
    }
}

public sealed class PathFinder
{
    private readonly IDataWriter _dataWriter;
    private readonly IDataReader _dataReader;

    public PathFinder(IDataWriter dataWriter, IDataReader dataReader)
    {
        _dataWriter = dataWriter;
        _dataReader = dataReader;
    }
}

// - generated code end -

[Singleton]
public partial class PathFinderFactory : IPathFinder, IAStartPathFinder
{
    private readonly IDataWriter _dataWriter;
    private readonly IDataReader _dataReader;

    [InjectConstructor]
    private PathFinderFactory(IDataWriter dataWriter, IDataReader dataReader)
    {
        _dataWriter = dataWriter;
        _dataReader = dataReader;
    }

    public PathFinder GetPathFinder()
    {
        return new PathFinder(_dataWriter, _dataReader);
    }
}

// - generated code begin -

// 标记为 Singleton 才生成
partial class PathFinderFactory // MovementManager.DI.Singleton.g.cs
{
    public static void CreateService(IScope scope, Action<object, IScope> onCreated)
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
                var instance = new PathFinderFactory(p0!, p1!);
                onCreated.Invoke(instance, scope);
            }
        }
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

    public void OnServicesReady() { }
}

// - generated code begin -

// Host, User 或 Scope 才生成
partial class CellManager // CellManager.DI.Lifecycle.g.cs
{
    private IScope? _parentScope;

    private IScope? GetServiceScope()
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
        Godot.GD.PushError("CellManager 没有最近的 Service Scope");
        return null;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        switch (what)
        {
            case NotificationEnterTree:
            {
                // Host 才生成
                AttachHostServices();
                break;
            }
            case NotificationReady:
            {
                // User 才生成
                ResolveUserDependencies();
                break;
            }
            case NotificationExitTree:
            {
                // Host 才生成
                UnattachHostServices();

                _parentScope = null;
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
    private void AttachHostServices()
    {
        var scope = GetServiceScope();
        if (scope is null)
        {
            return;
        }
        // 注册为 Singleton 特性指定的类型
        scope.RegisterService<ICellGetter>(Self);
        scope.RegisterService<ICellEditor>(Self);
    }

    /// <summary>
    /// 取消注册所有标记为 [Singleton] 的字段或属性
    /// </summary>
    private void UnattachHostServices()
    {
        var scope = GetServiceScope();
        if (scope is null)
        {
            return;
        }
        // 取消注册 Singleton 特性指定的类型
        scope.UnregisterService<ICellGetter>();
        scope.UnregisterService<ICellEditor>();
    }
}

// 标记为 User 才生成
partial class CellManager // CellManager.DI.User.g.cs
{
    // 实现了 IServicesReady 才生成
    private readonly HashSet<Type> _unresolvedDependencies = new()
    {
        // 列举字段或属性中所有标记为 [Inject] 的类型
        typeof(IDataReader),
        typeof(IDataWriter),
    };

    // 实现了 IServicesReady 才生成
    private void OnDependencyResolved<T>()
    {
        _unresolvedDependencies.Remove(typeof(T));
        if (_unresolvedDependencies.Count == 0)
        {
            ((IServicesReady)this).OnServicesReady();
        }
    }

    /// <summary>
    /// 解析所有标记为 [Inject] 的字段或属性
    /// </summary>
    private void ResolveUserDependencies()
    {
        var scope = GetServiceScope();
        if (scope is null)
        {
            return;
        }
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
    Services = [typeof(DatabaseWriter), typeof(PathFinderFactory)],
    Hosts = [typeof(CellManager)]
)]
public partial class MyScope : Godot.Node, IScope { }

// - generated code begin -

// Host, User 或 Scope 才生成
partial class MyScope // MyScope.DI.Lifecycle.g.cs
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

    public override void _Notification(int what)
    {
        base._Notification(what);

        switch (what)
        {
            case NotificationEnterTree:
            {
                // Scope 才生成
                InstantiateScopeSingletons();
                break;
            }
            case NotificationReady:
            {
                // Scope 才生成
                CheckWaitList();
                break;
            }
            case NotificationExitTree:
            {
                // Scope 才生成
                DisposeScopeSingletons();

                _parentScope = null;
                break;
            }
        }
    }
}

// 实现了 IScope 才生成
partial class MyScope // MyContext.DI.Scope.g.cs
{
    /// <summary>
    /// 实例化所有 Scope 约束的单例服务
    /// </summary>
    private void InstantiateScopeSingletons()
    {
        DatabaseWriter.CreateService(
            this,
            (instance, scope) =>
            {
                if (instance is IDisposable disposable)
                {
                    _disposableSingletons.Add(disposable);
                }
                // 在此注册 Singleton 单例服务
                // 注册为 Singleton 特性指定的类型
                // 如果没有指定服务类型则注册为原类型
                scope.RegisterService((IDataWriter)instance);
                scope.RegisterService((IDataReader)instance);
            }
        );
        PathFinderFactory.CreateService(
            this,
            (instance, scope) =>
            {
                if (instance is IDisposable disposable)
                {
                    _disposableSingletons.Add(disposable);
                }
                scope.RegisterService((PathFinderFactory)instance);
            }
        );
    }

    /// <summary>
    /// 释放所有 Scope 约束的单例服务实例
    /// </summary>
    private void DisposeScopeSingletons()
    {
        foreach (var disposable in _disposableSingletons)
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
        _disposableSingletons.Clear();
        _services.Clear();
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
}

// 实现了 IScope 才生成
partial class MyScope // MyContext.DI.IScope.g.cs
{
    private static readonly HashSet<Type> ServiceTypes = new()
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

    private readonly Dictionary<Type, object> _services = new();
    private readonly Dictionary<Type, List<Action<object>>> _waiters = new();
    private readonly HashSet<IDisposable> _disposableSingletons = new();

    void IScope.ResolveDependency<T>(Action<T> onResolved)
    {
        var type = typeof(T);
        if (!ServiceTypes.Contains(type))
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
        if (_services.TryGetValue(type, out var singleton))
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
        if (!ServiceTypes.Contains(type))
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
        if (!_services.TryAdd(type, instance))
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
        if (!ServiceTypes.Contains(type))
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
        _services.Remove(type);
    }
}

// - generated code end -

*/