using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Templates.Validation;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests.Validation;

/// <summary>
/// Coverage for the data-type constraint evaluator in
/// <see cref="OperationalTemplateValidator"/>. Each test pairs a
/// tiny inline OPT2 that exercises exactly one rule with a single
/// Composition leaf and asserts the expected zero/one issue.
/// </summary>
public sealed class DataTypeTests
{
    private static readonly OperationalTemplateValidator s_validator = new();

    // ---- helpers -----------------------------------------------------

    private static DvText Name(string s) => new() { Value = s };

    private static Element NewElement(string nodeId, DataValue value) => new()
    {
        ArchetypeNodeId = nodeId,
        Name = Name(nodeId),
        Value = value,
    };

    private static ItemTree NewItemTree(string nodeId, IList<Item>? items = null) => new()
    {
        ArchetypeNodeId = nodeId,
        Name = Name(nodeId),
        Items = items,
    };

    private static PointEvent NewPointEvent(string nodeId, ItemStructure data) => new()
    {
        ArchetypeNodeId = nodeId,
        Name = Name(nodeId),
        Data = data,
    };

    private static History NewHistory(string nodeId, IList<Event>? events = null) => new()
    {
        ArchetypeNodeId = nodeId,
        Name = Name(nodeId),
        Events = events,
    };

    private static Observation NewObservation(string nodeId, History data) => new()
    {
        ArchetypeNodeId = nodeId,
        Name = Name(nodeId),
        Data = data,
    };

    private static Observation WrapElement(Element element)
    {
        ItemTree tree = NewItemTree("id4", [element]);
        PointEvent ev = NewPointEvent("id3", tree);
        History hist = NewHistory("id2", [ev]);
        return NewObservation("id1", hist);
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

    /// <summary>
    /// Wraps a single <c>value matches { ... }</c> body inside the
    /// boilerplate OBSERVATION → HISTORY → POINT_EVENT → ITEM_TREE →
    /// ELEMENT scaffold so every test can focus on the one primitive
    /// constraint shape it cares about.
    /// </summary>
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

    private static IReadOnlyList<ValidationIssue> Run(string opt2Source, Element element)
    {
        OperationalTemplate opt = Opt2Parser.Parse(opt2Source);
        Observation obs = WrapElement(element);
        return s_validator.Validate(obs, opt, TestContext.Current.CancellationToken);
    }

    private static void AssertSingleIssue(
        IReadOnlyList<ValidationIssue> issues,
        string ruleId,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        ValidationIssue only = Assert.Single(issues, i => i.RuleId == ruleId);
        Assert.Equal(severity, only.Severity);
    }

    private static void AssertNoIssue(IReadOnlyList<ValidationIssue> issues, string ruleId)
    {
        Assert.DoesNotContain(issues, i => i.RuleId == ruleId);
    }

    // ---- STRING_001 (pattern) ---------------------------------------

    [Fact]
    public void STRING_001_pattern_match_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_TEXT[id6] matches {
                                                value matches {/^[A-Z]{3}$/}
                                            }
""");
        DvText text = new() { Value = "ABC" };
        Element el = NewElement("id5", text);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.StringPatternViolation);
    }

    [Fact]
    public void STRING_001_pattern_mismatch_emits_STRING_001()
    {
        string opt = ScaffoldOpt2("""
                                            DV_TEXT[id6] matches {
                                                value matches {/^[A-Z]{3}$/}
                                            }
""");
        DvText text = new() { Value = "abc" };
        Element el = NewElement("id5", text);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.StringPatternViolation);
    }

    // ---- STRING_002 (enumeration) ------------------------------------

    [Fact]
    public void STRING_002_value_in_enum_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_TEXT[id6] matches {
                                                value matches {"red", "green", "blue"}
                                            }
""");
        DvText text = new() { Value = "green" };
        Element el = NewElement("id5", text);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.StringNotInEnumeration);
    }

    [Fact]
    public void STRING_002_value_not_in_enum_emits_STRING_002()
    {
        string opt = ScaffoldOpt2("""
                                            DV_TEXT[id6] matches {
                                                value matches {"red", "green", "blue"}
                                            }
""");
        DvText text = new() { Value = "purple" };
        Element el = NewElement("id5", text);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.StringNotInEnumeration);
    }

    // ---- NUMERIC_001 (integer range, via DV_COUNT) ------------------

    [Fact]
    public void NUMERIC_001_count_in_range_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_COUNT[id6] matches {
                                                magnitude matches {|1..10|}
                                            }
""");
        DvCount count = new(5);
        Element el = NewElement("id5", count);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.NumericOutOfRange);
    }

    [Fact]
    public void NUMERIC_001_count_out_of_range_emits_NUMERIC_001()
    {
        string opt = ScaffoldOpt2("""
                                            DV_COUNT[id6] matches {
                                                magnitude matches {|1..10|}
                                            }
""");
        DvCount count = new(42);
        Element el = NewElement("id5", count);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.NumericOutOfRange);
    }

    // ---- NUMERIC_002 (integer enumeration, via DV_COUNT) ------------

    [Fact]
    public void NUMERIC_002_count_in_enum_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_COUNT[id6] matches {
                                                magnitude matches {1, 2, 3}
                                            }
""");
        DvCount count = new(2);
        Element el = NewElement("id5", count);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.NumericNotInEnumeration);
    }

    [Fact]
    public void NUMERIC_002_count_not_in_enum_emits_NUMERIC_002()
    {
        string opt = ScaffoldOpt2("""
                                            DV_COUNT[id6] matches {
                                                magnitude matches {1, 2, 3}
                                            }
""");
        DvCount count = new(7);
        Element el = NewElement("id5", count);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.NumericNotInEnumeration);
    }

    // ---- DATETIME_001 (partial pattern on DV_DATE_TIME) -------------

    [Fact]
    public void DATETIME_001_value_matches_pattern_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_DATE_TIME[id6] matches {
                                                value matches {/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/}
                                            }
""");
        DvDateTime dt = new(new IsoDateTime(new IsoDate(2024, 1, 5), new IsoTime(12, 34, 56)));
        Element el = NewElement("id5", dt);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.DateTimePatternViolation);
    }

    [Fact]
    public void DATETIME_001_value_violates_pattern_emits_DATETIME_001()
    {
        string opt = ScaffoldOpt2("""
                                            DV_DATE_TIME[id6] matches {
                                                value matches {/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}$/}
                                            }
""");
        DvDateTime dt = new(new IsoDateTime(new IsoDate(2024, 1, 5), new IsoTime(12, 34)));
        Element el = NewElement("id5", dt);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.DateTimePatternViolation);
    }

    // ---- QUANTITY_001 (wrong units) ----------------------------------

    [Fact]
    public void QUANTITY_001_correct_units_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_QUANTITY[id6] matches {
                                                magnitude matches {|0.0..300.0|}
                                                units matches {"mm[Hg]"}
                                            }
""");
        DvQuantity q = new(120.0, "mm[Hg]");
        Element el = NewElement("id5", q);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.QuantityWrongUnits);
    }

    [Fact]
    public void QUANTITY_001_wrong_units_emits_QUANTITY_001()
    {
        string opt = ScaffoldOpt2("""
                                            DV_QUANTITY[id6] matches {
                                                magnitude matches {|0.0..300.0|}
                                                units matches {"mm[Hg]"}
                                            }
""");
        DvQuantity q = new(120.0, "kPa");
        Element el = NewElement("id5", q);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.QuantityWrongUnits);
    }

    // ---- QUANTITY_002 (magnitude out of range) ----------------------

    [Fact]
    public void QUANTITY_002_magnitude_in_range_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_QUANTITY[id6] matches {
                                                magnitude matches {|0.0..300.0|}
                                                units matches {"mm[Hg]"}
                                            }
""");
        DvQuantity q = new(120.0, "mm[Hg]");
        Element el = NewElement("id5", q);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.QuantityMagnitudeOutOfRange);
    }

    [Fact]
    public void QUANTITY_002_magnitude_out_of_range_emits_QUANTITY_002()
    {
        string opt = ScaffoldOpt2("""
                                            DV_QUANTITY[id6] matches {
                                                magnitude matches {|0.0..300.0|}
                                                units matches {"mm[Hg]"}
                                            }
""");
        DvQuantity q = new(999.0, "mm[Hg]");
        Element el = NewElement("id5", q);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.QuantityMagnitudeOutOfRange);
    }

    // ---- QUANTITY_003 (precision out of range) ----------------------

    [Fact]
    public void QUANTITY_003_precision_in_range_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_QUANTITY[id6] matches {
                                                magnitude matches {|0.0..300.0|}
                                                precision matches {|0..2|}
                                                units matches {"mm[Hg]"}
                                            }
""");
        DvQuantity q = new(120.0, "mm[Hg]", 1);
        Element el = NewElement("id5", q);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.QuantityPrecisionOutOfRange);
    }

    [Fact]
    public void QUANTITY_003_precision_out_of_range_emits_QUANTITY_003()
    {
        string opt = ScaffoldOpt2("""
                                            DV_QUANTITY[id6] matches {
                                                magnitude matches {|0.0..300.0|}
                                                precision matches {|0..2|}
                                                units matches {"mm[Hg]"}
                                            }
""");
        DvQuantity q = new(120.0, "mm[Hg]", 5);
        Element el = NewElement("id5", q);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.QuantityPrecisionOutOfRange);
    }

    // ---- ORDINAL_001 (value not in set, via DV_ORDINAL) -------------

    [Fact]
    public void ORDINAL_001_value_in_set_emits_no_issue()
    {
        string opt = ScaffoldOpt2("""
                                            DV_ORDINAL[id6] matches {
                                                value matches {0, 1, 2}
                                            }
""");
        DvOrdinal o = new() { Value = 1, Symbol = new DvCodedText("mild", new CodePhrase()) };
        Element el = NewElement("id5", o);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.OrdinalNotInSet);
    }

    [Fact]
    public void ORDINAL_001_value_not_in_set_emits_ORDINAL_001()
    {
        string opt = ScaffoldOpt2("""
                                            DV_ORDINAL[id6] matches {
                                                value matches {0, 1, 2}
                                            }
""");
        DvOrdinal o = new() { Value = 9, Symbol = new DvCodedText("severe", new CodePhrase()) };
        Element el = NewElement("id5", o);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.OrdinalNotInSet);
    }

    // ---- TERM_001 (code not in local value set) ---------------------

    private const string PositionValueSetBody = """
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
                                            DV_CODED_TEXT[id6] matches {
                                                defining_code matches {[ac1]}
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
    }
""";

    private static string Opt2WithValueSet()
    {
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
{{PositionValueSetBody}}

terminology
    term_definitions = <
        ["en"] = <
            ["id1"] = <text = <"id1"> description = <"id1">>
            ["id2"] = <text = <"id2"> description = <"id2">>
            ["id3"] = <text = <"id3"> description = <"id3">>
            ["id4"] = <text = <"id4"> description = <"id4">>
            ["id5"] = <text = <"id5"> description = <"id5">>
            ["id6"] = <text = <"id6"> description = <"id6">>
            ["at1"] = <text = <"standing"> description = <"standing">>
            ["at2"] = <text = <"sitting"> description = <"sitting">>
            ["at3"] = <text = <"lying"> description = <"lying">>
        >
    >
    value_sets = <
        ["ac1"] = <
            id = <"ac1">
            members = <"at1", "at2", "at3">
        >
    >
""";
    }

    [Fact]
    public void TERM_001_code_in_value_set_emits_no_issue()
    {
        string opt = Opt2WithValueSet();
        DvCodedText ct = new("sitting", new CodePhrase(new Rm.Support.TerminologyId { Value = "local" }, "at2"));
        Element el = NewElement("id5", ct);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertNoIssue(issues, ValidationRuleIds.CodeNotInValueSet);
    }

    [Fact]
    public void TERM_001_code_not_in_value_set_emits_TERM_001()
    {
        string opt = Opt2WithValueSet();
        DvCodedText ct = new("kneeling", new CodePhrase(new Rm.Support.TerminologyId { Value = "local" }, "at99"));
        Element el = NewElement("id5", ct);

        IReadOnlyList<ValidationIssue> issues = Run(opt, el);

        AssertSingleIssue(issues, ValidationRuleIds.CodeNotInValueSet);
    }

    // ---- TERM_002 (external binding always NotValidated) -------------

    [Fact]
    public void TERM_002_external_binding_emits_single_NotValidated_issue()
    {
        // Parse minimal template structure, then graft a constraint
        // binding into the terminology so the validator detects the
        // external binding regardless of the actual code value.
        string opt2 = Opt2WithValueSet();
        OperationalTemplate opt = Opt2Parser.Parse(opt2);
        opt.Terminology ??= new ArchetypeTerminology();
        opt.Terminology.ConstraintBindings["snomed_ct"] = new Dictionary<string, string>
        {
            ["ac1"] = "http://snomed.info/sct/?fhir_vs=ecl/123456",
        };

        DvCodedText ct = new("anything", new CodePhrase(new Rm.Support.TerminologyId { Value = "local" }, "at1"));
        Element el = NewElement("id5", ct);
        Observation obs = WrapElement(el);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(obs, opt, TestContext.Current.CancellationToken);

        ValidationIssue only = Assert.Single(issues, i => i.RuleId == ValidationRuleIds.BindingNotResolved);
        Assert.Equal(ValidationSeverity.NotValidated, only.Severity);
        // Critical: even though the code IS in the local value set, the external
        // binding short-circuits to NotValidated rather than passing or failing.
        AssertNoIssue(issues, ValidationRuleIds.CodeNotInValueSet);
    }

    // ---- Regression: null Value short-circuits without error --------

    [Fact]
    public void Regression_element_with_null_value_does_not_crash_or_error()
    {
        string opt = ScaffoldOpt2("""
                                            DV_QUANTITY[id6] matches {
                                                magnitude matches {|0.0..300.0|}
                                                units matches {"mm[Hg]"}
                                            }
""");
        Element el = new()
        {
            ArchetypeNodeId = "id5",
            Name = Name("id5"),
            Value = null,
        };

        OperationalTemplate parsed = Opt2Parser.Parse(opt);
        Observation obs = WrapElement(el);
        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(obs, parsed, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(issues, i => i.RuleId.StartsWith("QUANTITY_"));
    }
}
