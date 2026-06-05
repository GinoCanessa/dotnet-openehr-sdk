using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using DotnetOpenEhr.Serialization.Json;
using Xunit;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Pins M21 — the default value of <c>Composition.Composer</c> flips
/// from a freshly-constructed <c>PartyIdentified()</c> to <c>null</c>.
/// Default-constructed Compositions must not silently materialise a
/// composer; canonical JSON must omit the property entirely.
/// </summary>
public sealed class CompositionDefaultsTests
{
    [Fact]
    public void NewComposition_ComposerIsNull()
    {
        Composition c = new();
        Assert.Null(c.Composer);
    }

    [Fact]
    public void Serialize_DefaultComposition_OmitsComposer()
    {
        // Build a minimal composition that omits Composer to confirm
        // the wire form does not carry an empty composer object.
        Composition c = new()
        {
            Name = new DvText("comp"),
            ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Territory = new CodePhrase(new TerminologyId { Value = "ISO_3166-1" }, "US"),
            Category = new DvCodedText("event",
                new CodePhrase(new TerminologyId { Value = "openehr" }, "433")),
        };

        byte[] bytes = OpenEhrJson.Serialize(c);
        string text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("\"composer\"", text, StringComparison.Ordinal);
    }
}
