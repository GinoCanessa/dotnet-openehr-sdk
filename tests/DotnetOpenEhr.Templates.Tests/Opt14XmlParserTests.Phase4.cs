using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// Phase-4 coverage: <see cref="Opt14XmlParser"/> harvests OPT1.4
/// terminology from every <c>C_ARCHETYPE_ROOT</c> (incl. the root
/// <c>&lt;definition&gt;</c>) and any top-level
/// <c>&lt;component_ontologies&gt;</c> / <c>&lt;component_terminologies&gt;</c>
/// block. Asserts featurerequest acceptance criteria (c), (e), (f).
/// </summary>
public sealed partial class Opt14XmlParserTests
{
    // (fixture, expectedRootHrid, rootAtCode, rootAtCodeText)
    public static readonly System.Collections.Generic.IEnumerable<object[]> RootTerminologyRows =
    [
        ["KDS_Vitalstatus.opt", "openEHR-EHR-COMPOSITION.report.v1",          "at0005", "Status",         "de"],
        ["KDS_Diagnose.opt",    "openEHR-EHR-COMPOSITION.report.v1",          "at0001", "Tree",           "de"],
        ["KDS_Person.opt",      "openEHR-EHR-COMPOSITION.person.v0",          "at0000", "Person",         "de"],
        ["Blood Pressure.opt",  "openEHR-EHR-COMPOSITION.blood_pressure.v0",  "at0000", "Blood Pressure", "en"],
    ];

    [Theory]
    [MemberData(nameof(RootTerminologyRows))]
    public void Fixture_root_terminology_has_original_language_entries(
        string fixture, string expectedRootHrid, string atCode, string atText, string lang)
    {
        _ = expectedRootHrid;
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(fixture));
        Assert.True(opt.Terminology.TermDefinitions.TryGetValue(lang, out System.Collections.Generic.Dictionary<string, ArchetypeTerm>? perLang),
            $"{fixture}: root Terminology must have a non-empty per-language map for '{lang}'.");
        Assert.NotEmpty(perLang!);
        Assert.True(perLang!.TryGetValue(atCode, out ArchetypeTerm? term),
            $"{fixture}: well-known root at-code '{atCode}' must be present in Terminology['{lang}'].");
        Assert.Equal(atText, term!.Text);
    }

    // (fixture, composedHrid, atCode, atText, lang)
    public static readonly System.Collections.Generic.IEnumerable<object[]> ComposedTerminologyRows =
    [
        ["KDS_Vitalstatus.opt", "openEHR-EHR-EVALUATION.vital_status.v1",              "at0006", "Vitalstatus",          "de"],
        ["KDS_Diagnose.opt",    "openEHR-EHR-CLUSTER.case_identification.v0",          "at0000", "Fallidentifikation",   "de"],
        ["KDS_Person.opt",      "openEHR-EHR-ADMIN_ENTRY.versicherungsinformationen.v0", "at0000", "Versicherungsinformationen", "de"],
        ["Blood Pressure.opt",  "openEHR-EHR-OBSERVATION.blood_pressure.v2",           "at0004", "Systolic",             "en"],
    ];

    [Theory]
    [MemberData(nameof(ComposedTerminologyRows))]
    public void Fixture_well_known_at_code_text_matches_expected_literal(
        string fixture, string composedHrid, string atCode, string atText, string lang)
    {
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture(fixture));
        ArchetypeHRID hrid = ArchetypeHRID.Parse(composedHrid);
        ArchetypeTerminology? composed = opt.ComponentTerminologies
            .FirstOrDefault(kvp => string.Equals(kvp.Key.ToString(), hrid.ToString(), System.StringComparison.Ordinal)).Value;
        Assert.NotNull(composed);
        Assert.True(composed!.TermDefinitions.TryGetValue(lang, out System.Collections.Generic.Dictionary<string, ArchetypeTerm>? perLang),
            $"{fixture}: composed '{composedHrid}' must have terminology for language '{lang}'.");
        Assert.True(perLang!.TryGetValue(atCode, out ArchetypeTerm? term),
            $"{fixture}: composed '{composedHrid}' must contain at-code '{atCode}'.");
        Assert.Equal(atText, term!.Text);
    }

    [Fact]
    public void BloodPressure_component_terminologies_resolves_composed_at_code()
    {
        // Acceptance criterion (f): the Blood Pressure template
        // composes openEHR-EHR-OBSERVATION.blood_pressure.v2 (the
        // archetype with the actual blood-pressure shape). Confirm
        // ComponentTerminologies surfaces it with at0004 = "Systolic".
        OperationalTemplate opt = Opt14XmlParser.Parse(ReadFixture("Blood Pressure.opt"));
        Assert.NotEmpty(opt.ComponentTerminologies);
        ArchetypeHRID bp = ArchetypeHRID.Parse("openEHR-EHR-OBSERVATION.blood_pressure.v2");
        bool ok = opt.ComponentTerminologies.Any(kvp =>
            string.Equals(kvp.Key.ToString(), bp.ToString(), System.StringComparison.Ordinal));
        Assert.True(ok,
            $"ComponentTerminologies must include 'openEHR-EHR-OBSERVATION.blood_pressure.v2'; " +
            $"got [{string.Join(", ", opt.ComponentTerminologies.Keys.Select(k => k.ToString()))}].");
    }

    [Fact]
    public void Terminology_value_preservation_invariant_holds_for_all_fixtures()
    {
        // For each fixture, walk the source XML, group every
        // <term_definitions code="atXXXX">…<items id="text">value</items>
        // child by its surrounding archetype HRID (the `<archetype_id>`
        // sibling that precedes/follows the block on the same
        // C_ARCHETYPE_ROOT). Build the set of distinct
        // (hridOrRoot, atCode) pairs from the source. Then walk the
        // parsed Terminology + ComponentTerminologies and assert every
        // pair is present with a non-empty Text. (Counts don't have to
        // match — KDS_Person re-uses the same composed archetype HRID
        // in multiple <children> slots; the parser correctly merges.)
        foreach (string name in s_fixtureNames)
        {
            System.Xml.Linq.XDocument doc;
            using (System.IO.Stream s = OpenFixture(name))
            {
                doc = System.Xml.Linq.XDocument.Load(s);
            }
            System.Xml.Linq.XNamespace ns = "http://schemas.openehr.org/v1";
            System.Collections.Generic.HashSet<string> sourcePairs = new(System.StringComparer.Ordinal);
            foreach (System.Xml.Linq.XElement td in doc.Descendants(ns + "term_definitions"))
            {
                string? code = td.Attribute("code")?.Value;
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }
                bool hasText = td.Elements(ns + "items")
                    .Any(it => it.Attribute("id")?.Value == "text"
                        && !string.IsNullOrWhiteSpace(it.Value));
                if (!hasText)
                {
                    continue;
                }
                // Climb to the enclosing C_ARCHETYPE_ROOT (the parent
                // of the <term_definitions> sibling, identified by its
                // <archetype_id>/<value>) — or to the root <definition>
                // when the term lives there.
                string? hrid = null;
                System.Xml.Linq.XElement? scope = td.Parent;
                while (scope is not null)
                {
                    System.Xml.Linq.XElement? archId = scope.Element(ns + "archetype_id");
                    if (archId is not null)
                    {
                        hrid = archId.Element(ns + "value")?.Value;
                        break;
                    }
                    scope = scope.Parent;
                }
                sourcePairs.Add($"{hrid ?? "<root>"}::{code}");
            }

            OperationalTemplate opt = Opt14XmlParser.Parse(System.Text.Encoding.UTF8.GetString(ReadFixtureBytes(name)));
            string rootHrid = opt.ArchetypeId.ToString();

            System.Collections.Generic.HashSet<string> producedPairs = new(System.StringComparer.Ordinal);
            foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.Dictionary<string, ArchetypeTerm>> lang
                     in opt.Terminology.TermDefinitions)
            {
                foreach (System.Collections.Generic.KeyValuePair<string, ArchetypeTerm> e in lang.Value)
                {
                    if (!string.IsNullOrEmpty(e.Value.Text))
                    {
                        producedPairs.Add($"{rootHrid}::{e.Key}");
                    }
                }
            }
            foreach (System.Collections.Generic.KeyValuePair<ArchetypeHRID, ArchetypeTerminology> kvp
                     in opt.ComponentTerminologies)
            {
                string h = kvp.Key.ToString();
                foreach (System.Collections.Generic.KeyValuePair<string, System.Collections.Generic.Dictionary<string, ArchetypeTerm>> lang
                         in kvp.Value.TermDefinitions)
                {
                    foreach (System.Collections.Generic.KeyValuePair<string, ArchetypeTerm> e in lang.Value)
                    {
                        if (!string.IsNullOrEmpty(e.Value.Text))
                        {
                            producedPairs.Add($"{h}::{e.Key}");
                        }
                    }
                }
            }

            // Build the "<root>::atXXXX" alias-to-rootHrid map: every
            // source pair that mentions <root> should match the
            // production pair under rootHrid.
            System.Collections.Generic.HashSet<string> normalisedSource = new(System.StringComparer.Ordinal);
            foreach (string pair in sourcePairs)
            {
                normalisedSource.Add(pair.Replace("<root>::", $"{rootHrid}::"));
            }

            System.Collections.Generic.List<string> missing = [];
            foreach (string srcPair in normalisedSource)
            {
                if (!producedPairs.Contains(srcPair))
                {
                    missing.Add(srcPair);
                }
            }

            Assert.True(missing.Count == 0,
                $"{name}: terminology entries silently dropped: {string.Join(", ", missing.Take(10))}{(missing.Count > 10 ? " (+more)" : "")}.");
        }
    }

    private static byte[] ReadFixtureBytes(string name)
    {
        using System.IO.Stream s = OpenFixture(name);
        using System.IO.MemoryStream ms = new();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Opt2Parser_remains_untouched_after_Opt14_implementation()
    {
        // Smoke that the Opt2Parser path still works end-to-end and the
        // shared OperationalTemplate type still round-trips against the
        // bundled BMM after the additions in Phases 1-4.
        OperationalTemplate opt = Opt2Parser.Parse(System.IO.File.ReadAllText(
            FindOpt2Fixture("minimal_vitals.opt2")));
        Assert.Equal("openEHR-EHR-OBSERVATION.minimal_vitals.v1.0.0", opt.ArchetypeId.ToString());
        Assert.NotEmpty(opt.Nodes);
    }

    private static string FindOpt2Fixture(string name)
    {
        // Walk up from the test binary to the repo root and find the
        // physical opt2 file (it's also an EmbeddedResource but easier
        // to read by path here).
        string dir = System.AppContext.BaseDirectory;
        for (int i = 0; i < 12; i++)
        {
            string candidate = System.IO.Path.Combine(dir, "tests", "DotnetOpenEhr.Templates.Tests", "Fixtures", "Opt2", name);
            if (System.IO.File.Exists(candidate)) return candidate;
            dir = System.IO.Path.GetDirectoryName(dir)!;
            if (dir is null) break;
        }
        throw new System.IO.FileNotFoundException($"Opt2 fixture '{name}' not located via repo walk.");
    }
}
