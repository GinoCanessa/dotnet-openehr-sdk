namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN boolean scalar (spec 7.1.5). The literal forms <c>True</c> and
/// <c>False</c> are accepted case-insensitively; the writer emits
/// canonical <c>True</c> / <c>False</c>.
/// </summary>
public sealed class OdinBoolean : OdinValue
{
    public OdinBoolean(bool value)
    {
        Value = value;
    }

    public bool Value { get; set; }

    public override OdinKind Kind => OdinKind.Boolean;
}
