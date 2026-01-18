using GodotSharp.DI.Generator.Internal.Data;

namespace GodotSharp.DI.Generator.Internal.Extensions;

internal static class ClassTypeInfoExtension
{
    public static ServiceInfo GetServiceInfo(this ClassTypeInfo info)
    {
        return new ServiceInfo(info.Symbol, info.Namespace, info.ServiceConstructor!);
    }

    public static HostUserInfo GetHostUserInfo(this ClassTypeInfo info)
    {
        return new HostUserInfo(
            info.Symbol,
            info.Namespace,
            info.IsHost,
            info.IsUser,
            info.IsServicesReady,
            info.IsNode,
            info.HostSingletonServices,
            info.UserInjectMembers
        );
    }
}
