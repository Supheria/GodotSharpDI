namespace GodotSharp.DI.Generator.Internal.Data;

internal sealed record ClassTypeRoles(
    bool IsSingleton,
    bool IsTransient,
    bool IsHost,
    bool IsUser,
    bool IsScope,
    bool IsNode,
    bool IsServicesReady
)
{
    public bool IsService => IsSingleton || IsTransient;
}
