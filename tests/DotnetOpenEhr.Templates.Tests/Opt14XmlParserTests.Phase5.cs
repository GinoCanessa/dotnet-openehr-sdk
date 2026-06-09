using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// Phase-5 coverage: strict default throws cleanly on schema
/// violations; <see cref="ParseOptions.Lenient"/> opens the door to
/// vendor-extension and namespace drift without ever silently dropping
/// terminology data.
/// </summary>
public sealed partial class Opt14XmlParserTests
{
    [Fact]
    public void Malformed_fixture_throws_in_strict_mode()
    {
        Opt14ParseException ex = Assert.Throws<Opt14ParseException>(
            () => Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus_malformed.opt")));
        Assert.Contains("VENDOR_UNKNOWN_TYPE", ex.Message, System.StringComparison.Ordinal);
        Assert.True(ex.LineNumber > 0,
            "Opt14ParseException should carry IXmlLineInfo from the offending element.");
    }

    [Fact]
    public void Malformed_fixture_loads_in_lenient_mode()
    {
        OperationalTemplate lenientOpt = Opt14XmlParser.Parse(
            ReadFixture("KDS_Vitalstatus_malformed.opt"),
            new ParseOptions { Lenient = true });
        // Lenient mode must produce the same identity + structure as
        // the strict-mode parse of the canonical fixture (modulo the
        // dropped vendor child).
        OperationalTemplate strictOpt = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        Assert.Equal(strictOpt.ArchetypeId.ToString(), lenientOpt.ArchetypeId.ToString());
        Assert.Equal(strictOpt.TemplateId, lenientOpt.TemplateId);
        Assert.Equal(strictOpt.Definition.RmTypeName, lenientOpt.Definition.RmTypeName);
        Assert.True(System.Math.Abs(lenientOpt.Nodes.Count - strictOpt.Nodes.Count) <= 1,
            $"Lenient mode should drop only the vendor node; expected ~{strictOpt.Nodes.Count} nodes, got {lenientOpt.Nodes.Count}.");
    }

    [Fact]
    public void Lenient_mode_does_not_drop_terminology_entries()
    {
        // Same canonical fixture parsed strict and lenient should
        // produce identical Terminology + ComponentTerminologies maps:
        // lenient never loses data on a valid input.
        OperationalTemplate strictOpt = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        OperationalTemplate lenientOpt = Opt14XmlParser.Parse(
            ReadFixture("KDS_Vitalstatus.opt"),
            new ParseOptions { Lenient = true });

        AssertSameTerminology(strictOpt.Terminology, lenientOpt.Terminology, "root");
        Assert.Equal(strictOpt.ComponentTerminologies.Count, lenientOpt.ComponentTerminologies.Count);
        foreach (System.Collections.Generic.KeyValuePair<ArchetypeHRID, ArchetypeTerminology> kvp in strictOpt.ComponentTerminologies)
        {
            Assert.True(lenientOpt.ComponentTerminologies.TryGetValue(kvp.Key, out ArchetypeTerminology? other),
                $"Lenient mode lost composed terminology entry '{kvp.Key}'.");
            AssertSameTerminology(kvp.Value, other!, kvp.Key.ToString());
        }
    }

    [Fact]
    public void Malformed_fixture_lenient_does_not_drop_terminology_entries()
    {
        // The contract criterion (e) was added to enforce. The
        // malformed fixture differs from canonical only in one extra
        // vendor-typed <children> sibling — its terminology is
        // untouched, so lenient-parse of the malformed file must
        // produce the exact same terminology as strict-parse of the
        // canonical file.
        OperationalTemplate strictOpt = Opt14XmlParser.Parse(ReadFixture("KDS_Vitalstatus.opt"));
        OperationalTemplate lenientMalformed = Opt14XmlParser.Parse(
            ReadFixture("KDS_Vitalstatus_malformed.opt"),
            new ParseOptions { Lenient = true });

        AssertSameTerminology(strictOpt.Terminology, lenientMalformed.Terminology, "root");
        Assert.Equal(strictOpt.ComponentTerminologies.Count, lenientMalformed.ComponentTerminologies.Count);
    }

    [Fact]
    public void Missing_namespace_throws_in_strict_mode_loads_in_lenient_mode()
    {
        // Synthesise an in-memory variant of the canonical fixture
        // with the openEHR namespace stripped. Strict must throw at
        // root validation; lenient must succeed via local-name
        // fallback and still satisfy criterion (e).
        string canonical = ReadFixture("KDS_Vitalstatus.opt");
        string stripped = canonical.Replace(
            " xmlns=\"http://schemas.openehr.org/v1\"",
            string.Empty,
            System.StringComparison.Ordinal);
        Assert.NotEqual(canonical, stripped);

        Opt14ParseException strictEx = Assert.Throws<Opt14ParseException>(
            () => Opt14XmlParser.Parse(stripped));
        Assert.Contains("namespace", strictEx.Message, System.StringComparison.OrdinalIgnoreCase);

        OperationalTemplate lenientOpt = Opt14XmlParser.Parse(
            stripped, new ParseOptions { Lenient = true });
        Assert.NotNull(lenientOpt.ArchetypeId);
        Assert.Equal("openEHR-EHR-COMPOSITION.report.v1", lenientOpt.ArchetypeId.ToString());
        Assert.NotEmpty(lenientOpt.Terminology.TermDefinitions);
    }

    [Fact]
    public void Opt14ParseException_extends_InvalidOperationException()
    {
        // Callers that wrap Opt2Parser + Opt14XmlParser invocations in
        // a single catch (InvalidOperationException) should keep
        // working uniformly across both formats.
        Assert.True(typeof(System.InvalidOperationException).IsAssignableFrom(typeof(Opt14ParseException)));
    }

    private static void AssertSameTerminology(ArchetypeTerminology expected, ArchetypeTerminology actual, string scope)
    {
        Assert.Equal(expected.TermDefinitions.Count, actual.TermDefinitions.Count);
        foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.Dictionary<string, ArchetypeTerm>> lang
                 in expected.TermDefinitions)
        {
            Assert.True(actual.TermDefinitions.TryGetValue(lang.Key, out System.Collections.Generic.Dictionary<string, ArchetypeTerm>? other),
                $"[{scope}] missing language '{lang.Key}' in actual terminology.");
            Assert.Equal(lang.Value.Count, other!.Count);
            foreach (System.Collections.Generic.KeyValuePair<string, ArchetypeTerm> entry in lang.Value)
            {
                Assert.True(other.TryGetValue(entry.Key, out ArchetypeTerm? otherTerm),
                    $"[{scope}] missing at-code '{entry.Key}' in language '{lang.Key}'.");
                Assert.Equal(entry.Value.Text, otherTerm!.Text);
                Assert.Equal(entry.Value.Description, otherTerm.Description);
                Assert.Equal(entry.Value.Comment, otherTerm.Comment);
            }
        }
    }
}
