using System.IO;
using System.Text.Json;
using DotnetOpenEhr.Aql.Ast;
using DotnetOpenEhr.Aql.Evaluation;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Rm.Support;
using Xunit;

namespace DotnetOpenEhr.Aql.Tests.Evaluation.Fixtures.BindingRefactor;

/// <summary>
/// Pins the row output of a CONTAINS-heavy AQL query so the Phase 10
/// linked-list <c>Binding</c> rewrite (H4) can be proven equivalent to
/// the pre-refactor dictionary-backed implementation.
///
/// Phase 0 captured the expected fixture against the dictionary-backed
/// <c>Binding</c>. Phase 10 reruns this same test against the
/// linked-list rewrite and asserts row-for-row equality.
///
/// To regenerate the fixture (must be intentional — drift is the bug
/// this test exists to catch), set the
/// <c>OPENEHR_REGENERATE_BINDING_FIXTURE=1</c> environment variable
/// and rerun the test; the test will overwrite the expected file and
/// then fail with a "regenerated" message so a follow-up clean run is
/// required to confirm.
/// </summary>
public sealed class BindingRefactorEquivalenceTests
{
    private const string RegenEnvVar = "OPENEHR_REGENERATE_BINDING_FIXTURE";

    private const string ContainsHeavyQuery =
        "SELECT c/archetype_node_id, o/archetype_node_id, e/archetype_node_id " +
        "FROM EHR eh CONTAINS COMPOSITION c CONTAINS OBSERVATION o CONTAINS ELEMENT e";

    [Fact]
    public void ContainsHeavyQuery_ProducesIdenticalRows()
    {
        Composition sample = BuildSample();
        AqlEvaluator evaluator = new();
        AqlQuery q = AqlParser.Parse(ContainsHeavyQuery);
        IReadOnlyList<object?[]> rows = evaluator.Evaluate(q, [sample], ct: TestContext.Current.CancellationToken);

        string actualJson = SerializeRows(rows);

        string fixturePath = ResolveFixturePath();

        if (string.Equals(Environment.GetEnvironmentVariable(RegenEnvVar), "1", StringComparison.Ordinal))
        {
            File.WriteAllText(fixturePath, actualJson);
            Assert.Fail($"Regenerated fixture at '{fixturePath}'. Unset {RegenEnvVar} and rerun to verify.");
        }

        Assert.True(File.Exists(fixturePath), $"Expected fixture missing: {fixturePath}");
        string expectedJson = File.ReadAllText(fixturePath);
        Assert.Equal(NormalizeNewlines(expectedJson), NormalizeNewlines(actualJson));
    }

    private static string SerializeRows(IReadOnlyList<object?[]> rows)
    {
        List<object?[]> materialized = [];
        foreach (object?[] row in rows)
        {
            object?[] copy = new object?[row.Length];
            for (int i = 0; i < row.Length; i++)
            {
                copy[i] = row[i] switch
                {
                    null => null,
                    string s => s,
                    _ => row[i]!.ToString(),
                };
            }
            materialized.Add(copy);
        }

        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
        };
        return JsonSerializer.Serialize(materialized, options);
    }

    private static string ResolveFixturePath()
    {
        // The fixture lives next to this test source file in the
        // checked-in repository layout. Resolve it relative to the
        // assembly's known location by walking up from
        // <repo>/tests/DotnetOpenEhr.Aql.Tests/bin/<config>/<tfm>/.
        string assemblyDir = Path.GetDirectoryName(typeof(BindingRefactorEquivalenceTests).Assembly.Location)
            ?? throw new InvalidOperationException("Could not locate test assembly directory.");
        DirectoryInfo? dir = new(assemblyDir);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string candidateDir = Path.Combine(
                dir.FullName,
                "Evaluation", "Fixtures", "BindingRefactor");
            if (Directory.Exists(candidateDir))
            {
                return Path.Combine(candidateDir, "contains_heavy_expected.json");
            }
        }
        throw new InvalidOperationException(
            "Could not locate the BindingRefactor fixture directory under the test project root.");
    }

    private static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n");

    private static Composition BuildSample()
    {
        DvQuantity sbp = new(120, "mm[Hg]");
        Element systolic = new()
        {
            Name = new DvText("Systolic"),
            ArchetypeNodeId = "at0004",
            Value = sbp,
        };
        Element diastolic = new()
        {
            Name = new DvText("Diastolic"),
            ArchetypeNodeId = "at0005",
            Value = new DvQuantity(80, "mm[Hg]"),
        };

        ItemTree tree = new()
        {
            Name = new DvText("blood_pressure_data"),
            ArchetypeNodeId = "at0003",
            Items = [systolic, diastolic],
        };
        PointEvent pt = new()
        {
            Name = new DvText("Any event"),
            ArchetypeNodeId = "at0006",
            Time = new DvDateTime(new IsoDateTime(
                new IsoDate(2024, 5, 27),
                new IsoTime(10, 25, 3))),
            Data = tree,
        };
        History history = new()
        {
            Name = new DvText("history"),
            ArchetypeNodeId = "at0002",
            Origin = new DvDateTime(new IsoDateTime(
                new IsoDate(2024, 5, 27),
                new IsoTime(10, 25, 3))),
            Events = [pt],
        };
        Observation obs = new()
        {
            Name = new DvText("Blood pressure"),
            ArchetypeNodeId = "openEHR-EHR-OBSERVATION.blood_pressure.v2",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Encoding = new CodePhrase(new TerminologyId { Value = "IANA_character-sets" }, "UTF-8"),
            Subject = new PartySelf(),
            Data = history,
        };
        return new Composition
        {
            Name = new DvText("Vitals"),
            ArchetypeNodeId = "openEHR-EHR-COMPOSITION.encounter.v1",
            Language = new CodePhrase(new TerminologyId { Value = "ISO_639-1" }, "en"),
            Territory = new CodePhrase(new TerminologyId { Value = "ISO_3166-1" }, "US"),
            Category = new DvCodedText("event",
                new CodePhrase(new TerminologyId { Value = "openehr" }, "433")),
            Composer = new PartyIdentified { Name = "Dr. Alice Example" },
            Content = [obs],
        };
    }
}
