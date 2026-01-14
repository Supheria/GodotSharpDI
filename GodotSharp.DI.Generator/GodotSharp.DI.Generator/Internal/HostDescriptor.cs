using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal sealed class HostDescriptor
{
    public bool IsNode { get; }
    public INamedTypeSymbol HostType { get; }
    public List<(string Name, INamedTypeSymbol ServiceType)> SingletonServices { get; }

    public HostDescriptor(
        bool isNode,
        INamedTypeSymbol hostType,
        List<(string, INamedTypeSymbol)> singletonServices
    )
    {
        IsNode = isNode;
        HostType = hostType;
        SingletonServices = singletonServices;
    }
}
