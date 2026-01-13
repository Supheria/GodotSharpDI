using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class ServiceTypeCollector
{
    public static ServiceTypeInfo? Analyze(INamedTypeSymbol type, CachedSymbol cachedSymbol)
    {
        var isSingleton = SymbolHelper.HasAttribute(type, cachedSymbol.SingletonServiceAttribute);
        var isTransient = SymbolHelper.HasAttribute(type, cachedSymbol.TransientServiceAttribute);
        if (isSingleton && isTransient)
        {
            return null;
        }
        if (!isSingleton && !isTransient)
        {
            return null;
        }
        var attr = isSingleton
            ? cachedSymbol.SingletonServiceAttribute
            : cachedSymbol.TransientServiceAttribute;
        var exposedTypes = SymbolHelper.GetServiceTypesFromAttribute(type, attr);
        return new ServiceTypeInfo(exposedTypes, isSingleton, isTransient);
    }
}
