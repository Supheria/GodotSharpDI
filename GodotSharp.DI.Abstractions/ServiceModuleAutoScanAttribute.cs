namespace GodotSharp.DI.Abstractions;

// TODO: 自动扫描命名空间及子命名空间下的所有 SingletonService 和 TransientService。SingletonService 如果标记在类名上由该 Scope 自行创建实例，如果标记在属性上则不创建
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ServiceModuleAutoScanAttribute : Attribute { }
