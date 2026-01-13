using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class ServiceTypeInfo
{
    public INamedTypeSymbol[] ExposedServiceTypes { get; }
    public bool IsSingleton { get; }
    public bool IsTransient { get; }

    public ServiceTypeInfo(INamedTypeSymbol[] exposed, bool isSingleton, bool isTransient)
    {
        ExposedServiceTypes = exposed;
        IsSingleton = isSingleton;
        IsTransient = isTransient;
    }
}
