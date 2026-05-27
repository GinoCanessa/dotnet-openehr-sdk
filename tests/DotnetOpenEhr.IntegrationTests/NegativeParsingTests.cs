using System.Text.Json;
using DotnetOpenEhr.Serialization.Json;
using Xunit;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Negative-path coverage. The gating expectation is that pathological
/// input fails with a <see cref="JsonException"/> carrying enough
/// information (path, byte offset, or message text) for a developer to
/// pinpoint the offending fragment.
/// </summary>
/// <remarks>
/// STJ does not raise an exception when a non-<c>required</c> property
/// is omitted — every property in the RM falls back to its property
/// initializer. Enforcing presence would require marking dozens of
/// properties <c>required</c> across the RM, which is out of scope for
/// Phase 3. The negative coverage here therefore concentrates on the
/// failure modes the round-trip integration can actually hit:
/// malformed JSON, unknown polymorphic discriminators, and wrong root
/// type.
/// </remarks>
public sealed class NegativeParsingTests
{
    [Fact]
    public void Malformed_Json_RaisesJsonException_WithLineAndPosition()
    {
        const string truncated = """{"_type":"COMPOSITION","name":{"_type":"DV_TEXT","value":"X"""; // missing closing }
        JsonException ex = Assert.Throws<JsonException>(
            () => OpenEhrJson.ParseComposition(truncated));
        Assert.True(ex.LineNumber.HasValue || ex.BytePositionInLine.HasValue || ex.Path is not null,
            "JsonException for malformed JSON should carry at least one of: line number, byte position, or path.");
    }

    [Fact]
    public void Unknown_Type_Discriminator_RaisesJsonException()
    {
        const string composition = """
        {
          "_type": "COMPOSITION",
          "name": {"_type": "DV_TEXT", "value": "Bogus"},
          "archetype_node_id": "openEHR-EHR-COMPOSITION.encounter.v1",
          "language": {"terminology_id": {"value": "ISO_639-1"}, "code_string": "en"},
          "territory": {"terminology_id": {"value": "ISO_3166-1"}, "code_string": "US"},
          "category": {"_type": "DV_CODED_TEXT", "value": "event",
                       "defining_code": {"terminology_id": {"value": "openehr"}, "code_string": "433"}},
          "composer": {"_type": "PARTY_BANANA"}
        }
        """;
        Assert.Throws<JsonException>(() => OpenEhrJson.ParseComposition(composition));
    }

    [Fact]
    public void Wrong_Root_Type_RaisesJsonException_NamingTheActualType()
    {
        const string observationAsRoot = """
        {
          "_type": "OBSERVATION",
          "name": {"_type": "DV_TEXT", "value": "Standalone"},
          "archetype_node_id": "openEHR-EHR-OBSERVATION.something.v1",
          "language": {"terminology_id": {"value": "ISO_639-1"}, "code_string": "en"},
          "encoding": {"terminology_id": {"value": "IANA_character-sets"}, "code_string": "UTF-8"},
          "subject": {"_type": "PARTY_SELF"},
          "data": {"_type": "HISTORY",
                   "name": {"_type": "DV_TEXT", "value": "h"},
                   "archetype_node_id": "at0002",
                   "origin": {"_type": "DV_DATE_TIME", "value": "2024-05-27T10:25:03"}}
        }
        """;
        JsonException ex = Assert.Throws<JsonException>(
            () => OpenEhrJson.ParseComposition(observationAsRoot));
        Assert.Contains("Composition", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Observation", ex.Message, StringComparison.Ordinal);
    }
}
