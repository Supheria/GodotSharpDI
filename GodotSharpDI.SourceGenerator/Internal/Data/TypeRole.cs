namespace GodotSharpDI.SourceGenerator.Internal.Data;

/// <summary>
/// 类型角色
/// </summary>
internal enum TypeRole
{
    None,
    Service, // 纯服务（Singleton/Transient）
    Host, // 仅 Host
    User, // 仅 User
    HostAndUser, // Host + User
    Scope, // Scope
}
