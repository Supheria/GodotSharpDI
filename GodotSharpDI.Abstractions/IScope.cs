using System;

namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void ProvideService<T>(T? instance, string? errorMessage = null)
        where T : class;

    void ResolveDependency<T>(
        Action<T> onResolved,
        Action<string> onFailed,
        string requestorType,
        string? scopeChain = null,
        string? dependencyChain = null
    )
        where T : class;
}
