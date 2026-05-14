// #define SAMPLE

#if SAMPLE

using System;
using System.Collections.Generic;
using System.Text;
using GodotSharpDI.Abstractions;

namespace GodotSharpDI;

//
// --- Example interfaces ---
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
// --- Non-node type services ---
//

[Singleton(typeof(IDataWriter), typeof(IDataReader))]
public partial class DatabaseWriter : IDataWriter, IDataReader, IDisposable
{
    void IDataReader.Read() { }

    public void Dispose() { }
}

// - generated code begin -

// Only generated when marked as Singleton
partial class DatabaseWriter // DatabaseWriter.DI.Service.g.cs
{
    public static void CreateService(IScope scope, Action<object, IScope> onCreated)
    {
        // Only generated when injection constructor parameters equal 0, following the template below

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

// Only generated when marked as Singleton
partial class PathFinderFactory // MovementManager.DI.Singleton.g.cs
{
    public static void CreateService(IScope scope, Action<object, IScope> onCreated)
    {
        // Only generated when injection constructor parameters > 0, following the template below

        // Record total number of dependencies needed
        var remaining = 2;

        // Declare temporary variables for all parameter types in injection constructor
        IDataWriter? p0 = null;
        IDataReader? p1 = null;

        // Resolve all dependency parameters in injection constructor
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
// --- Node type host and user ---
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

// Only generated for Host, User or Scope
partial class CellManager // CellManager.DI.Lifecycle.g.cs
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
        Godot.GD.PushError("CellManager cannot find nearest Service Scope");
        return null;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        switch (what)
        {
            case NotificationEnterTree:
            {
                _parentScope = null;
                break;
            }
            case NotificationReady:
            {
                // Only generated for Host
                ProvideHostServices();
                // Only generated for User
                ResolveUserDependencies();
                break;
            }
            case NotificationExitTree:
            {
                _parentScope = null;
                break;
            }
            case NotificationPredelete:
            {
                break;
            }
        }
    }
}

// Only generated when marked as Host
partial class CellManager // CellManager.DI.Host.g.cs
{
    /// <summary>
    /// Register all fields or properties marked as [Singleton]
    /// </summary>
    private void ProvideHostServices()
    {
        var scope = GetParentScope();
        if (scope is null)
        {
            return;
        }
        // Register as types specified by Singleton attribute
        scope.ProvideService<ICellGetter>(Self);
        scope.ProvideService<ICellEditor>(Self);
    }
}

// Only generated when marked as User
partial class CellManager // CellManager.DI.User.g.cs
{
    // Only generated when implementing IServicesReady
    private readonly HashSet<Type> _unresolvedDependencies = new()
    {
        // List all types marked as [Inject] in fields or properties
        typeof(IDataReader),
        typeof(IDataWriter),
    };

    // Only generated when implementing IServicesReady
    private void OnDependencyResolved<T>()
    {
        _unresolvedDependencies.Remove(typeof(T));
        if (_unresolvedDependencies.Count == 0)
        {
            ((IServicesReady)this).OnServicesReady();
        }
    }

    /// <summary>
    /// Resolve all fields or properties marked as [Inject]
    /// </summary>
    private void ResolveUserDependencies()
    {
        var scope = GetParentScope();
        if (scope is null)
        {
            return;
        }
        scope.ResolveDependency<IDataReader>(dependency =>
        {
            _dataReader = dependency;
            // Only generated when implementing IServicesReady
            OnDependencyResolved<IDataReader>();
        });
        scope.ResolveDependency<IDataWriter>(dependency =>
        {
            _dataWriter = dependency;
            // Only generated when implementing IServicesReady
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

// Only generated for Host, User or Scope
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
                _parentScope = null;
                break;
            }
            case NotificationReady:
            {
                // Only generated for Scope
                InstantiateScopeSingletons();
                CheckWaitList();
                break;
            }
            case NotificationExitTree:
            {
                _parentScope = null;
                break;
            }
            case NotificationPredelete:
            {
                // Only generated for Scope
                DisposeScopeSingletons();
                break;
            }
        }
    }
}

// Only generated when implementing IScope
partial class MyScope // MyContext.DI.Scope.g.cs
{
    private static readonly HashSet<Type> ServiceTypes = new()
    {
        // Register as types specified by Singleton attribute
        // If no service type is specified, register as original type

        // Singleton services provided by DatabaseWriter
        typeof(IDataWriter),
        typeof(IDataReader),
        // Singleton services provided by CellManager
        typeof(ICellGetter),
        typeof(ICellEditor),
        // Singleton services provided by MovementManager
        typeof(IPathProvider),
        typeof(IPathGenerator),
    };

    private readonly Dictionary<Type, object> _services = new();
    private readonly Dictionary<Type, List<Action<object>>> _waiters = new();
    private readonly HashSet<IDisposable> _disposableSingletons = new();

    /// <summary>
    /// Instantiate all Scope-constrained singleton services
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
                // Register singleton service here
                // Register as types specified by Singleton attribute
                // If no service type is specified, register as original type
                scope.ProvideService((IDataWriter)instance);
                scope.ProvideService((IDataReader)instance);
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
                scope.ProvideService((PathFinderFactory)instance);
            }
        );
    }

    /// <summary>
    /// Dispose all Scope-constrained singleton service instances
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
        Godot.GD.PushError($"Service types with incomplete injection exist: {sb}");
        _waiters.Clear();
    }
}

// Only generated when implementing IScope
partial class MyScope // MyContext.DI.IScope.g.cs
{
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
            Godot.GD.PushError($"Cannot find service type from root Service Scope: {type.Name}");
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

    void IScope.ProvideService<T>(T instance)
    {
        var type = typeof(T);
        if (!ServiceTypes.Contains(type))
        {
            var parent = GetParentScope();
            if (parent is not null)
            {
                parent.ProvideService(instance);
                return;
            }
            Godot.GD.PushError($"Cannot register service type from root Service Scope: {type.Name}");
            return;
        }
        if (!_services.TryAdd(type, instance))
        {
            Godot.GD.PushError($"Duplicate registration of type: {type.Name}.");
        }
        if (_waiters.Remove(type, out var waiterList))
        {
            foreach (var callback in waiterList)
            {
                callback.Invoke(instance);
            }
        }
    }
}

// - generated code end -

#endif
