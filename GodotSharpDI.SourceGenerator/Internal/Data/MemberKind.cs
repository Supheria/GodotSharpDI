namespace GodotSharpDI.SourceGenerator.Internal.Data;

internal enum MemberKind
{
    None,
    InjectField,
    InjectProperty,
    ProvideProperty,
    ProvideMethod,
}
