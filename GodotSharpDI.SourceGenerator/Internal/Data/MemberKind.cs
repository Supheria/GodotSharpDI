namespace GodotSharpDI.SourceGenerator.Internal.Data;

internal enum MemberKind
{
    None,
    InjectField,
    InjectProperty,
    ProvideField,
    ProvideProperty,
    ProvideMethod,
}
