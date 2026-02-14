using System;

namespace GodotSharpDI.Abstractions;

public interface IScope
{
    void ProvideService<TImpl>(ResolutionResult result)
        where TImpl : class;

    void ResolveDependency<TExposed>(Action<ResolutionResult> onResult, string requestorType)
        where TExposed : class;
}
