using System;

namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void ProvideService<T>(T instance)
        where T : notnull;
    void ResolveDependency<T>(Action<T> onResolved)
        where T : notnull;
}
