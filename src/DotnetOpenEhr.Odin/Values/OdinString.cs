namespace DotnetOpenEhr.Odin.Values;

/// <summary>
/// ODIN string scalar (spec 7.1.2). Carries the decoded string value;
/// escape sequences are resolved at parse time and re-encoded by the
/// writer.
/// </summary>
public sealed class OdinString : OdinValue
{
    public OdinString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    public string Value { get; set; }

    public override OdinKind Kind => OdinKind.String;
}
