namespace GodotSharpDI.Abstractions;

public interface IDependenciesResolved
{
    void OnDependenciesResolved(bool isAllDependenciesReady);
}
