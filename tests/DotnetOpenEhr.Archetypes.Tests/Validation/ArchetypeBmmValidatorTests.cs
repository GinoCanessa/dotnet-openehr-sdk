using System.IO;
using System.Linq;
using System.Reflection;
using DotnetOpenEhr.Archetypes.Adl2;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Validation;
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Bmm.Rm;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Validation;

/// <summary>
/// Validation tests for <see cref="ArchetypeBmmValidator"/> against the
/// canonical openEHR RM BMM. Mixes positive fixture round-trips, code
/// mutations to provoke each issue code, and a cancellation smoke test.
/// </summary>
public class ArchetypeBmmValidatorTests
{
    private static readonly BmmModel s_rmBmm = OpenEhrRmBmm.LoadDefault();

    private static string ReadFixture(string name)
    {
        Assembly asm = typeof(ArchetypeBmmValidatorTests).Assembly;
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(r => r.EndsWith(name, System.StringComparison.Ordinal));
        Assert.NotNull(resourceName);
        using Stream stream = asm.GetManifestResourceStream(resourceName!)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static Archetype ParseFixture(string name) => Adl2Parser.Parse(ReadFixture(name));

    private static IReadOnlyList<ArchetypeIssue> Validate(Archetype a)
        => new ArchetypeBmmValidator().Validate(a, s_rmBmm, TestContext.Current.CancellationToken);

    private static int CountErrors(IReadOnlyList<ArchetypeIssue> issues)
        => issues.Count(i => i.Severity == ArchetypeIssueSeverity.Error);

    // -- Fixture positive tests -----------------------------------------

    [Fact]
    public void Fixture_blood_pressure_has_zero_errors()
    {
        Archetype a = ParseFixture("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0.adls");
        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        IReadOnlyList<ArchetypeIssue> errors = issues
            .Where(i => i.Severity == ArchetypeIssueSeverity.Error)
            .Where(i => !IsKnownParserLimitation(i))
            .ToList();
        Assert.Empty(errors);
    }

    // Pre-Phase-7f the ADL2 parser does not recognise bare ISO 8601
    // duration literals such as `PT24H` and materialises them as
    // CComplexObject with the literal text as RmTypeName. The validator
    // correctly flags those as BMM_001; filter them here so the cross-
    // check focuses on the validator under test.
    private static bool IsKnownParserLimitation(ArchetypeIssue issue)
    {
        if (issue.Code != ArchetypeIssueCodes.UnknownRmType)
        {
            return false;
        }
        // Issue messages embed the type name in single quotes.
        int s = issue.Message.IndexOf('\'');
        int e = issue.Message.IndexOf('\'', s + 1);
        if (s < 0 || e <= s)
        {
            return false;
        }
        string name = issue.Message.Substring(s + 1, e - s - 1);
        return LooksLikeIso8601DurationLiteral(name);
    }

    private static bool LooksLikeIso8601DurationLiteral(string s)
    {
        if (string.IsNullOrEmpty(s) || (s[0] != 'P' && s[0] != 'p'))
        {
            return false;
        }
        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }
        // Must contain at least one digit to distinguish from a class name.
        for (int i = 1; i < s.Length; i++)
        {
            if (char.IsDigit(s[i]))
            {
                return true;
            }
        }
        return false;
    }

    [Fact]
    public void Fixture_body_weight_has_zero_errors()
    {
        Archetype a = ParseFixture("openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls");
        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        IReadOnlyList<ArchetypeIssue> errors = issues
            .Where(i => i.Severity == ArchetypeIssueSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Fixture_internal_value_set_has_zero_errors()
    {
        Archetype a = ParseFixture("openEHR-EHR-OBSERVATION.internal_value_set.v1.0.0.adls");
        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        IReadOnlyList<ArchetypeIssue> errors = issues
            .Where(i => i.Severity == ArchetypeIssueSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    // -- Negative mutation tests ----------------------------------------

    [Fact]
    public void Unknown_rm_type_is_reported_with_BMM_001()
    {
        Archetype a = ParseFixture("openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls");

        // Locate the HISTORY child under /data and rename to a bogus type.
        CAttribute dataAttr = a.Definition.Attributes.First(x => x.RmAttributeName == "data");
        CComplexObject history = (CComplexObject)dataAttr.Children[0];
        Assert.Equal("HISTORY", history.RmTypeName);
        history.RmTypeName = "BOGUS";

        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        IReadOnlyList<ArchetypeIssue> bmm001 = issues
            .Where(i => i.Code == ArchetypeIssueCodes.UnknownRmType)
            .ToList();
        Assert.Single(bmm001);
        Assert.Equal(ArchetypeIssueSeverity.Error, bmm001[0].Severity);
        Assert.Contains("BOGUS", bmm001[0].Message);
        Assert.StartsWith("/data[", bmm001[0].Path);
    }

    [Fact]
    public void Unknown_attribute_is_reported_with_BMM_002()
    {
        Archetype a = ParseFixture("openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls");

        // Add a bogus attribute directly to the root OBSERVATION.
        a.Definition.Attributes.Add(new CSingleAttribute
        {
            RmAttributeName = "bogus_attr",
            Children = [],
        });

        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        IReadOnlyList<ArchetypeIssue> bmm002 = issues
            .Where(i => i.Code == ArchetypeIssueCodes.UnknownAttribute)
            .ToList();
        Assert.Single(bmm002);
        Assert.Equal(ArchetypeIssueSeverity.Error, bmm002[0].Severity);
        Assert.Equal("/bogus_attr", bmm002[0].Path);
        Assert.Contains("OBSERVATION", bmm002[0].Message);
    }

    [Fact]
    public void Primitive_type_mismatch_is_reported_with_BMM_003()
    {
        // ELEMENT.value is BMM-declared as DATA_VALUE. Replacing a
        // DV_QUANTITY constraint with a CString must trip BMM_003.
        Archetype a = ParseFixture("openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls");

        CAttribute dataAttr = a.Definition.Attributes.First(x => x.RmAttributeName == "data");
        CComplexObject history = (CComplexObject)dataAttr.Children[0];
        CAttribute eventsAttr = history.Attributes.First(x => x.RmAttributeName == "events");
        CComplexObject ev = (CComplexObject)eventsAttr.Children[0];
        CAttribute eventDataAttr = ev.Attributes.First(x => x.RmAttributeName == "data");
        CComplexObject itemTree = (CComplexObject)eventDataAttr.Children[0];
        CAttribute itemsAttr = itemTree.Attributes.First(x => x.RmAttributeName == "items");
        CComplexObject element = (CComplexObject)itemsAttr.Children[0];
        CAttribute valueAttr = element.Attributes.First(x => x.RmAttributeName == "value");
        Assert.IsType<CComplexObject>(valueAttr.Children[0]);

        valueAttr.Children[0] = new CString { RmTypeName = "String", NodeId = "id99" };

        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        IReadOnlyList<ArchetypeIssue> bmm003 = issues
            .Where(i => i.Code == ArchetypeIssueCodes.TypeMismatch)
            .ToList();
        Assert.Single(bmm003);
        Assert.Equal(ArchetypeIssueSeverity.Error, bmm003[0].Severity);
        Assert.Contains("CString", bmm003[0].Message);
        Assert.Contains("DATA_VALUE", bmm003[0].Message);
        Assert.EndsWith("/value[id99]", bmm003[0].Path);
    }

    [Fact]
    public void Multiple_issues_are_each_reported_with_correct_path_and_code()
    {
        Archetype a = ParseFixture("openEHR-EHR-OBSERVATION.body_weight.v1.0.0.adls");

        // Mutation 1: bogus RM type on the HISTORY child.
        CAttribute dataAttr = a.Definition.Attributes.First(x => x.RmAttributeName == "data");
        CComplexObject history = (CComplexObject)dataAttr.Children[0];
        history.RmTypeName = "BOGUS";

        // Mutation 2: bogus attribute on the root.
        a.Definition.Attributes.Add(new CSingleAttribute
        {
            RmAttributeName = "no_such_attr",
            Children = [],
        });

        // Mutation 3: bogus attribute on the protocol's ITEM_TREE.
        CAttribute protoAttr = a.Definition.Attributes.First(x => x.RmAttributeName == "protocol");
        CComplexObject protoTree = (CComplexObject)protoAttr.Children[0];
        protoTree.Attributes.Add(new CSingleAttribute
        {
            RmAttributeName = "another_bogus",
            Children = [],
        });

        IReadOnlyList<ArchetypeIssue> issues = Validate(a);

        ArchetypeIssue bmm001 = Assert.Single(issues, i => i.Code == ArchetypeIssueCodes.UnknownRmType);
        Assert.StartsWith("/data[", bmm001.Path);

        IReadOnlyList<ArchetypeIssue> bmm002 = issues
            .Where(i => i.Code == ArchetypeIssueCodes.UnknownAttribute)
            .ToList();
        Assert.Equal(2, bmm002.Count);
        Assert.Contains(bmm002, i => i.Path == "/no_such_attr");
        Assert.Contains(bmm002, i => i.Path.EndsWith("/another_bogus", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Empty_archetype_has_zero_issues()
    {
        // Synthesise an archetype whose definition is just an OBSERVATION
        // root with no child attributes — must be entirely clean.
        AuthoredArchetype a = new()
        {
            Definition = new CComplexObject
            {
                RmTypeName = "OBSERVATION",
                NodeId = "id1",
            },
        };

        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        Assert.Empty(issues);
    }

    // -- Cancellation ---------------------------------------------------

    [Fact]
    public void Cancellation_token_throws_promptly_on_large_input()
    {
        // Build a synthetic archetype with many top-level attributes so the
        // cancellation check between iterations actually fires.
        AuthoredArchetype a = new()
        {
            Definition = new CComplexObject
            {
                RmTypeName = "OBSERVATION",
                NodeId = "id1",
            },
        };
        for (int i = 0; i < 5_000; i++)
        {
            a.Definition.Attributes.Add(new CSingleAttribute
            {
                RmAttributeName = "synthetic_" + i,
                Children = [],
            });
        }

        using CancellationTokenSource cts = new();
        cts.Cancel();

        ArchetypeBmmValidator validator = new();
        Assert.Throws<OperationCanceledException>(() => validator.Validate(a, s_rmBmm, cts.Token));
    }

    // -- Misc -----------------------------------------------------------

    [Fact]
    public void Unknown_root_rm_type_emits_single_BMM_001_at_root_path()
    {
        AuthoredArchetype a = new()
        {
            Definition = new CComplexObject
            {
                RmTypeName = "NOPE",
                NodeId = "id1",
            },
        };

        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        ArchetypeIssue only = Assert.Single(issues);
        Assert.Equal(ArchetypeIssueCodes.UnknownRmType, only.Code);
        Assert.Equal(ArchetypeIssueSeverity.Error, only.Severity);
        Assert.Equal("/", only.Path);
    }

    [Fact]
    public void Inherited_attribute_resolves_through_ancestor_chain()
    {
        // OBSERVATION inherits 'archetype_node_id' (or 'name', 'language',
        // …) from far up the chain. Add a constraint on 'name' and confirm
        // no BMM_002 fires for it.
        AuthoredArchetype a = new()
        {
            Definition = new CComplexObject
            {
                RmTypeName = "OBSERVATION",
                NodeId = "id1",
                Attributes =
                [
                    new CSingleAttribute
                    {
                        RmAttributeName = "name",
                        Children = [],
                    },
                ],
            },
        };

        IReadOnlyList<ArchetypeIssue> issues = Validate(a);
        Assert.DoesNotContain(issues, i => i.Code == ArchetypeIssueCodes.UnknownAttribute);
        Assert.Equal(0, CountErrors(issues));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        ArchetypeBmmValidator validator = new();
        CancellationToken ct = TestContext.Current.CancellationToken;
        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, s_rmBmm, ct));
        AuthoredArchetype a = new()
        {
            Definition = new CComplexObject { RmTypeName = "OBSERVATION", NodeId = "id1" },
        };
        Assert.Throws<ArgumentNullException>(() => validator.Validate(a, null!, ct));
    }

    [Fact]
    public void Issue_codes_are_stable_constants()
    {
        Assert.Equal("BMM_001_UNKNOWN_RM_TYPE", ArchetypeIssueCodes.UnknownRmType);
        Assert.Equal("BMM_002_UNKNOWN_ATTRIBUTE", ArchetypeIssueCodes.UnknownAttribute);
        Assert.Equal("BMM_003_TYPE_MISMATCH", ArchetypeIssueCodes.TypeMismatch);
        Assert.Equal("BMM_004_GENERIC_PARAM_MISMATCH", ArchetypeIssueCodes.GenericParamMismatch);
    }
}
