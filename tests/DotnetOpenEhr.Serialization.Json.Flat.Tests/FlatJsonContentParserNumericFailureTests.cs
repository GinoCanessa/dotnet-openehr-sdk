using System.IO;
using System.Text;
using System.Text.Json;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Serialization.Json;
using DotnetOpenEhr.Templates;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// H5 — FlatJsonContentParser.ReadDouble / ReadInt / ReadInt64 throw
/// JsonException on malformed numeric input rather than silently
/// returning 0/0d. The exception names the offending FLAT path.
/// </summary>
public sealed class FlatJsonContentParserNumericFailureTests
{
    private static OperationalTemplate LoadMinimalObservationTemplate()
    {
        string opt2 = SchemaDrivenFixtureLoader.LoadText("minimal_observation", "template.opt2");
        return Opt2Parser.Parse(opt2);
    }

    private static Composition LoadMinimalObservationComposition()
    {
        byte[] canonicalBytes = SchemaDrivenFixtureLoader.Load("minimal_observation", "composition.json");
        Composition? composition = OpenEhrJson.ParseComposition(canonicalBytes);
        Assert.NotNull(composition);
        return composition!;
    }

    [Fact]
    public void ReadDouble_OnNonNumericValueAtMagnitudePath_ThrowsJsonException_NamingPath()
    {
        OperationalTemplate template = LoadMinimalObservationTemplate();
        Composition source = LoadMinimalObservationComposition();
        byte[] flatBytes = OpenEhrFlatJson.Serialize(source, template);
        string flatText = Encoding.UTF8.GetString(flatBytes);

        // Find the magnitude key and corrupt its numeric value.
        string corrupted = CorruptFirstKeyMatching(flatText, "|magnitude", "not-a-number");

        JsonException ex = Assert.Throws<JsonException>(
            () => OpenEhrFlatJson.ParseComposition(Encoding.UTF8.GetBytes(corrupted), template));

        Assert.Contains("|magnitude", ex.Message, StringComparison.Ordinal);
        Assert.Contains("not-a-number", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Locates the first JSON property whose key ends with
    /// <paramref name="keySuffix"/> and replaces its (numeric) value
    /// with a JSON-encoded string. Returns the corrupted JSON text.
    /// </summary>
    private static string CorruptFirstKeyMatching(string json, string keySuffix, string replacementString)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        using MemoryStream ms = new();
        using (Utf8JsonWriter writer = new(ms))
        {
            writer.WriteStartObject();
            bool replaced = false;
            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
            {
                if (!replaced && p.Name.EndsWith(keySuffix, StringComparison.Ordinal))
                {
                    writer.WriteString(p.Name, replacementString);
                    replaced = true;
                }
                else
                {
                    p.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
            Assert.True(replaced, $"No FLAT key ending in '{keySuffix}' was found to corrupt.");
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
