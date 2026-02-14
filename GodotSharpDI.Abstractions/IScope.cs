using System;

namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void ProvideService<T>(ResolutionResult<T> result)
        where T : class;

    void ResolveDependency<T>(Action<ResolutionResult<T>> onResult, string requestorType)
        where T : class;
}
