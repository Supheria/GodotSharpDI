using System;

namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void RegisterService<T>(T instance)
        where T : notnull;
    void UnregisterService<T>()
        where T : notnull;
    void ResolveDependency<T>(Action<T> onResolved)
        where T : notnull;
}
