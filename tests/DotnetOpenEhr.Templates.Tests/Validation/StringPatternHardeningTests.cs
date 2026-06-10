using System.Text.RegularExpressions;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Templates.Validation;
using Xunit;
using static DotnetOpenEhr.Templates.Tests.Validation.ValidationAssertions;

namespace DotnetOpenEhr.Templates.Tests.Validation;

/// <summary>
/// H8 — OperationalTemplateValidator regex hardening: malformed
/// patterns emit <see cref="ValidationSeverity.NotValidated"/> instead
/// of silently passing; catastrophic patterns time out per
/// <see cref="OperationalTemplateValidatorOptions.RegexMatchTimeout"/>;
/// and the regex compile cache is not poisoned by repeated bad
/// patterns. Reuses the OPT2 scaffold + helpers from
/// <see cref="DataTypeTests"/>.
/// </summary>
public sealed class StringPatternHardeningTests
{
    // ---- helpers (mirrors DataTypeTests scaffold) ---------------------

    private static DvText Name(string s) => new() { Value = s };

    private static Element NewElement(string nodeId, DataValue value) => new()
    {
        ArchetypeNodeId = nodeId,
        Name = Name(nodeId),
        Value = value,
    };

    private static Observation WrapElement(Element element)
    {
        ItemTree tree = new() { ArchetypeNodeId = "id4", Name = Name("id4"), Items = [element] };
        PointEvent ev = new() { ArchetypeNodeId = "id3", Name = Name("id3"), Data = tree };
        History hist = new() { ArchetypeNodeId = "id2", Name = Name("id2"), Events = [ev] };
        return new Observation { ArchetypeNodeId = "id1", Name = Name("id1"), Data = hist };
    }

    private static string Opt2(string definitionBody, params string[] idCodes)
    {
        System.Text.StringBuilder terms = new();
        foreach (string id in idCodes)
        {
            terms.AppendLine($"            [\"{id}\"] = <text = <\"{id}\"> description = <\"{id}\">>");
        }
        return $$"""
operational_template (adl_version=2.0.6; rm_release=1.1.0; generated)
    openEHR-EHR-OBSERVATION.dtypes.v1.0.0

language
    original_language = <[ISO_639-1::en]>

description
    lifecycle_state = <"unmanaged">
    details = <
        ["en"] = <
            language = <[ISO_639-1::en]>
            purpose = <"test fixture">
        >
    >

definition
{{definitionBody}}

terminology
    term_definitions = <
        ["en"] = <
{{terms}}        >
    >
""";
    }

    private static string ScaffoldOpt2(string valueBody)
    {
        string body = $$"""
    OBSERVATION[id1] matches {
        data matches {
            HISTORY[id2] matches {
                events matches {
                    POINT_EVENT[id3] matches {
                        data matches {
                            ITEM_TREE[id4] matches {
                                items matches {
                                    ELEMENT[id5] matches {
                                        value matches {
{{valueBody}}
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
""";
        return Opt2(body, "id1", "id2", "id3", "id4", "id5", "id6");
    }

    private static IReadOnlyList<ValidationIssue> RunWith(
        OperationalTemplateValidator validator,
        string opt2Source,
        Element element)
    {
        OperationalTemplate opt = Opt2Parser.Parse(opt2Source);
        Observation obs = WrapElement(element);
        return validator.Validate(obs, opt, TestContext.Current.CancellationToken);
    }

    // ---- tests ---------------------------------------------------------

    [Fact]
    public void STRING_001_invalid_pattern_emits_NotValidated_not_silent_pass()
    {
        // /[abc/ is an unterminated character class.
        string opt = ScaffoldOpt2("""
                                            DV_TEXT[id6] matches {
                                                value matches {/[abc/}
                                            }
""");
        OperationalTemplateValidator validator = new();

        IReadOnlyList<ValidationIssue> issues = RunWith(validator, opt, NewElement("id5", new DvText { Value = "xyz" }));

        AssertSingleIssue(
            issues,
            ValidationRuleIds.StringPatternViolation,
            "/data[id2]/events[id3]/data[id4]/items[id5]/value/value",
            ValidationSeverity.NotValidated);
    }

    [Fact]
    public void STRING_001_catastrophic_regex_emits_NotValidated_on_timeout()
    {
        // (a+)+$ over an a* string forces exponential backtracking.
        string opt = ScaffoldOpt2("""
                                            DV_TEXT[id6] matches {
                                                value matches {/(a+)+$/}
                                            }
""");
        OperationalTemplateValidator validator = new(new OperationalTemplateValidatorOptions
        {
            RegexMatchTimeout = TimeSpan.FromMilliseconds(50),
        });

        IReadOnlyList<ValidationIssue> issues = RunWith(validator, opt,
            NewElement("id5", new DvText { Value = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaab" }));

        AssertSingleIssue(
            issues,
            ValidationRuleIds.StringPatternViolation,
            "/data[id2]/events[id3]/data[id4]/items[id5]/value/value",
            ValidationSeverity.NotValidated);
        // Do not assert timing — the RegexMatchTimeoutException is the
        // contract; wall-clock is not.
    }

    [Fact]
    public void Options_RegexMatchTimeout_NegativeTimeSpan_throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OperationalTemplateValidator(new OperationalTemplateValidatorOptions
            {
                RegexMatchTimeout = TimeSpan.FromSeconds(-1),
            }));
    }

    [Fact]
    public void InvalidPattern_DoesNotPoisonRegexCache()
    {
        const string badPattern = "[no_close_unique_flake_marker";
        string opt = ScaffoldOpt2($$"""
                                            DV_TEXT[id6] matches {
                                                value matches {/{{badPattern}}/}
                                            }
""");
        OperationalTemplateValidator validator = new();

        // Sanity: the pattern really is malformed on this runtime.
        // RegexParseException : ArgumentException, so ThrowsAny catches both
        // (xUnit's Throws<T> is exact-type, not subtype).
        Assert.ThrowsAny<ArgumentException>(static () => _ = new Regex(badPattern));

        // Drive the validator twice with the bad pattern.
        IReadOnlyList<ValidationIssue> issues = RunWith(
            validator, opt, NewElement("id5", new DvText { Value = "abc" }));
        RunWith(validator, opt, NewElement("id5", new DvText { Value = "abc" }));

        // Anchor: prove the bad pattern actually reached the catch block,
        // so the cache-membership assertion below is meaningful.
        Assert.Contains(
            issues,
            i => i.RuleId == ValidationRuleIds.StringPatternViolation
                 && i.Severity == ValidationSeverity.NotValidated);

        // Invariant under test: GetOrAdd's factory threw, so no key with
        // this pattern must be in the cache. Race-free because the pattern
        // is unique to this test — concurrent inserts from sibling test
        // classes cannot collide. Pattern-only (ignores Timeout) so the
        // assertion survives any future change to the production default
        // timeout literal without going silently vacuous.
        Assert.False(
            OperationalTemplateValidator.s_defaultRegexCache.Keys
                .Any(k => k.Pattern == badPattern),
            "Malformed pattern must not be inserted into the regex cache.");
    }
}
