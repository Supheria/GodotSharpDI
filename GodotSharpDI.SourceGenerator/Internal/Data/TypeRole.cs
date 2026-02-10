namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 类型角色
/// </summary>
internal enum TypeRole
{
    None,
    Service, // 纯服务（Singleton/Transient）- 保留用于向后兼容
    Provider, // 新增：非 Node 服务提供者（[Provider]）
    Host, // 仅 Host
    User, // 仅 User
    HostAndUser, // Host + User
    Scope, // Scope
}
