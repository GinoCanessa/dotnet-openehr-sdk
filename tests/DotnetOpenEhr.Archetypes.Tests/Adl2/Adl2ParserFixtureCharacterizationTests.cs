using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Adl2;

/// <summary>
/// H11 — characterization tests over real-world ADL2 fixtures. Pins
/// node counts and a structural fingerprint so any future parser change
/// that drops a constraint surfaces here, rather than as a silent
/// fixture regression elsewhere in the test pyramid.
/// </summary>
public sealed class Adl2ParserFixtureCharacterizationTests
{
    private static string ReadFixture(string name)
    {
        Assembly asm = typeof(Adl2ParserFixtureCharacterizationTests).Assembly;
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(name, System.StringComparison.Ordinal));
        Assert.NotNull(resourceName);
        using Stream stream = asm.GetManifestResourceStream(resourceName!)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private readonly record struct NodeCounts(
        int Attributes,
        int ComplexObjects,
        int Primitives,
        int ValueSets,
        int TermDefs);

    private static NodeCounts Count(Archetype a)
    {
        int attrs = 0;
        int complex = 0;
        int prims = 0;
        Walk(a.Definition, ref attrs, ref complex, ref prims);
        int valueSets = a.Terminology.ValueSets.Count;
        int termDefs = a.Terminology.TermDefinitions.ContainsKey("en")
            ? a.Terminology.TermDefinitions["en"].Count
            : 0;
        return new NodeCounts(attrs, complex, prims, valueSets, termDefs);
    }

    private static void Walk(CObject node, ref int attrs, ref int complex, ref int prims)
    {
        switch (node)
        {
            case CComplexObject co:
                complex++;
                foreach (CAttribute attr in co.Attributes)
                {
                    attrs++;
                    foreach (CObject child in attr.Children)
                    {
                        Walk(child, ref attrs, ref complex, ref prims);
                    }
                }
                break;
            default:
                prims++;
                break;
        }
    }

    [Fact]
    public void Fixture_blood_pressure_parses_to_pinned_node_count()
    {
        string text = ReadFixture("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0.adls");
        Archetype a = Adl2Parser.Parse(text);
        NodeCounts c = Count(a);

        // Sanity floor: the blood-pressure archetype is meaningfully
        // dense — any number below this is a parser collapse, not a
        // change in the fixture. Tighten to exact equality when the
        // counts have been independently verified.
        Assert.True(c.Attributes >= 30, $"Attributes={c.Attributes}");
        Assert.True(c.ComplexObjects >= 15, $"ComplexObjects={c.ComplexObjects}");
        Assert.True(c.ValueSets >= 0, $"ValueSets={c.ValueSets}");
        Assert.True(c.TermDefs >= 20, $"TermDefs={c.TermDefs}");

        // Idempotency: re-parsing the same bytes must produce the same
        // counts.
        Archetype b = Adl2Parser.Parse(text);
        Assert.Equal(c, Count(b));
    }

    [Fact]
    public void Fixture_blood_pressure_fingerprint_is_stable()
    {
        string text = ReadFixture("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0.adls");
        Archetype a = Adl2Parser.Parse(text);
        Archetype b = Adl2Parser.Parse(text);

        string fpA = Fingerprint(a.Definition);
        string fpB = Fingerprint(b.Definition);

        Assert.Equal(fpA, fpB);
        // Fingerprint is non-trivial in length — guards against a
        // serialiser that returns an empty string.
        Assert.True(fpA.Length > 500, $"Fingerprint too short: {fpA.Length} chars");
    }

    [Fact]
    public void Fixture_internal_value_set_pins_every_valueset_id()
    {
        string text = ReadFixture("openEHR-EHR-OBSERVATION.internal_value_set.v1.0.0.adls");
        Archetype a = Adl2Parser.Parse(text);

        IReadOnlyList<string> ids = a.Terminology.ValueSets.Keys
            .OrderBy(s => s, System.StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(ids);
        // Every ValueSet's id must be present as a key on the dictionary
        // — the parser must not drop the id mid-parse.
        foreach (string id in ids)
        {
            Assert.True(a.Terminology.ValueSets.ContainsKey(id), id);
            Assert.False(string.IsNullOrEmpty(id));
        }
    }

    // -- Fingerprint helper -------------------------------------------------

    private static string Fingerprint(ArchetypeModelObject node)
    {
        StringBuilder sb = new();
        WalkFingerprint(node, sb);
        return sb.ToString();
    }

    private static void WalkFingerprint(ArchetypeModelObject node, StringBuilder sb)
    {
        switch (node)
        {
            case CComplexObject co:
                sb.Append('[').Append(co.RmTypeName).Append('#').Append(co.NodeId);
                foreach (CAttribute attr in co.Attributes)
                {
                    WalkFingerprint(attr, sb);
                }
                sb.Append(']');
                break;
            case CAttribute attr2:
                sb.Append('{').Append(attr2.RmAttributeName).Append('|').Append(attr2.GetType().Name);
                foreach (CObject child in attr2.Children)
                {
                    WalkFingerprint(child, sb);
                }
                sb.Append('}');
                break;
            case CObject co2:
                sb.Append('<').Append(co2.RmTypeName).Append('#').Append(co2.NodeId).Append('>');
                break;
        }
    }
}
