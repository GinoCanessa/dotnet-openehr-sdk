using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// M2 — OpenEhrFlatJson.TrySetComposerAttr stops silently casting a
/// non-PartyIdentified Composer to PartyIdentified. The reachable
/// public-API path through ParseComposition starts with a fresh
/// Composition (null Composer per M21), so the "non-Identified
/// composer" throw branch is defensive coding for chained-parse
/// scenarios; the public-API contract we can pin from here is that
/// a composer-name FLAT key materialises a PartyIdentified on a
/// freshly-parsed Composition without throwing.
/// </summary>
public sealed class OpenEhrFlatJsonComposerTests
{
    [Fact]
    public void ParseComposition_WithComposerNameKey_MaterializesPartyIdentified()
    {
        string flat =
            "{"
            + "\"encounter/_archetype_node_id\": \"openEHR-EHR-COMPOSITION.encounter.v1\","
            + "\"encounter/category|code\": \"433\","
            + "\"encounter/category|terminology\": \"openehr\","
            + "\"encounter/category|value\": \"event\","
            + "\"encounter/composer|name\": \"Dr. Alice\","
            + "\"encounter/context/start_time\": \"2024-05-27T10:25:03Z\","
            + "\"encounter/context/setting|code\": \"228\","
            + "\"encounter/context/setting|terminology\": \"openehr\","
            + "\"encounter/context/setting|value\": \"primary\","
            + "\"encounter/language|code\": \"en\","
            + "\"encounter/language|terminology\": \"ISO_639-1\","
            + "\"encounter/territory|code\": \"US\","
            + "\"encounter/territory|terminology\": \"ISO_3166-1\""
            + "}";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(flat);

        Composition? c = OpenEhrFlatJson.ParseComposition(bytes);
        Assert.NotNull(c);
        PartyIdentified composer = Assert.IsType<PartyIdentified>(c!.Composer);
        Assert.Equal("Dr. Alice", composer.Name);
    }
}
