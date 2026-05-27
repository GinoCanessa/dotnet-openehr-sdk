using System.Text.Json;
using DotnetOpenEhr.Rm.Composition;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// Canonical ↔ FLAT crossover: take the smallest schemaless-roundtrip
/// fixture, FLAT-parse to RM, canonical-serialise, canonical-parse, then
/// FLAT-serialise again. The final FLAT bytes must equal the original
/// FLAT bytes after canonical key ordering.
/// </summary>
public sealed class CanonicalFlatCrossoverTests
{
    [Fact]
    public void Flat_To_Canonical_To_Flat_Is_ByteEquivalent()
    {
        const string fixture = "minimal_metadata_flat.json";
        const string templateId = "minimal";

        byte[] flatOriginal = FixtureLoader.Load(fixture);
        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> originalEntries = FlatJsonReader.Read(flatOriginal);
        byte[] originalCanonical = FlatJsonWriter.WriteCanonical(originalEntries);

        // FLAT → RM
        Composition? composition = OpenEhrFlatJson.ParseComposition(flatOriginal);
        Assert.NotNull(composition);

        // RM → canonical JSON → RM
        byte[] canonicalBytes = OpenEhrJson.Serialize(composition!);
        Composition? roundtripped = OpenEhrJson.ParseComposition(canonicalBytes);
        Assert.NotNull(roundtripped);

        // RM → FLAT (after canonical detour)
        byte[] flatFinal = OpenEhrFlatJson.Serialize(roundtripped!, templateId);

        IReadOnlyList<KeyValuePair<FlatPath, JsonElement>> finalEntries = FlatJsonReader.Read(flatFinal);
        byte[] finalCanonical = FlatJsonWriter.WriteCanonical(finalEntries);

        Assert.Equal(originalCanonical, finalCanonical);
    }
}
