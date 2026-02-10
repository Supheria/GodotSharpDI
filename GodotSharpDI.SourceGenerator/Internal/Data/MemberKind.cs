namespace GodotSharpDI.SourceGenerator.Internal.Data;

internal enum MemberKind
{
    None,
    InjectField,
    InjectProperty,
    SingletonField,    // 保留用于向后兼容
    SingletonProperty, // 保留用于向后兼容
    ProvidesProperty,  // 新增：使用 [Provides] 标记的属性
    ProvidesMethod,    // 新增：使用 [Provides] 标记的方法
}
