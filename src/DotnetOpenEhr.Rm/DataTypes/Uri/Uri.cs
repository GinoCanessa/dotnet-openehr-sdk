using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.DataTypes.Uri;

// SPEC: Data Types Information Model.html#_dv_uri_class (Section 10.3.1).
/// <summary>
/// A reference to an object that conforms to the RFC 3986 URI/URL standard.
/// </summary>
public class DvUri : DataValue
{
    public DvUri() { }

    public DvUri(string value)
    {
        Value = value;
    }

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public override string ToString() => Value;
}

// SPEC: Data Types Information Model.html#_dv_ehr_uri_class (Section 10.3.2).
/// <summary>URI restricted to the <c>ehr://</c> scheme used inside openEHR.</summary>
public sealed class DvEhrUri : DvUri
{
    public DvEhrUri() { }

    public DvEhrUri(string value) : base(value) { }
}
