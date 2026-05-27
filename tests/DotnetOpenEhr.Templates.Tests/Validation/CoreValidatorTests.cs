using System.Diagnostics;
using DotnetOpenEhr.Rm.Common;
using DotnetOpenEhr.Rm.Composition;
using DotnetOpenEhr.Rm.DataStructures;
using DotnetOpenEhr.Rm.DataTypes.Quantity;
using DotnetOpenEhr.Rm.DataTypes.Text;
using DotnetOpenEhr.Templates.Validation;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests.Validation;

/// <summary>
/// Coverage for <see cref="OperationalTemplateValidator"/>: structural,
/// cardinality, and occurrences rules + cancellation behaviour.
/// </summary>
public sealed class CoreValidatorTests
{
    private static readonly OperationalTemplateValidator s_validator = new();

    // ---- helpers -----------------------------------------------------

    private static OperationalTemplate ParseOpt2(string body)
        => Opt2Parser.Parse(body);

    private static DvText Name(string s) => new() { Value = s };

    private static Composition NewComposition(string nodeId, IList<ContentItem>? content = null)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name("root"),
            Content = content,
        };

    private static Section NewSection(string nodeId, IList<ContentItem>? items = null)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name(nodeId),
            Items = items,
        };

    private static Observation NewObservation(string nodeId, History data)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name(nodeId),
            Data = data,
        };

    private static History NewHistory(string nodeId, IList<Event>? events = null)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name(nodeId),
            Events = events,
        };

    private static PointEvent NewPointEvent(string nodeId, ItemStructure data)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name(nodeId),
            Data = data,
        };

    private static ItemTree NewItemTree(string nodeId, IList<Item>? items = null)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name(nodeId),
            Items = items,
        };

    private static Element NewElement(string nodeId, DvQuantity? value = null)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name(nodeId),
            Value = value,
        };

    private static Cluster NewCluster(string nodeId, IList<Item>? items = null)
        => new()
        {
            ArchetypeNodeId = nodeId,
            Name = Name(nodeId),
            Items = items ?? [],
        };

    // ---- inline OPT2 templates ---------------------------------------

    /// <summary>Template wrapper boilerplate (description + terminology stub).</summary>
    private static string Opt2(string archetypeId, string definitionBody, params string[] idCodes)
    {
        System.Text.StringBuilder terms = new();
        foreach (string id in idCodes)
        {
            terms.AppendLine($"            [\"{id}\"] = <text = <\"{id}\"> description = <\"{id}\">>");
        }
        return $$"""
operational_template (adl_version=2.0.6; rm_release=1.1.0; generated)
    {{archetypeId}}

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

    private static string MinimalObsTemplate => Opt2(
        "openEHR-EHR-OBSERVATION.minimal_vitals.v1.0.0",
        """
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
                                                    DV_QUANTITY[id6] matches {
                                                        magnitude matches {|0.0..1000.0|}
                                                        units matches {"mm[Hg]"}
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
        """,
        "id1", "id2", "id3", "id4", "id5", "id6");

    private static string ReportCompositionTemplate => Opt2(
        "openEHR-EHR-COMPOSITION.report.v1.0.0",
        """
            COMPOSITION[id1] matches {
                content matches {
                    OBSERVATION[id2] occurrences matches {0..*} matches {
                        data matches {
                            HISTORY[id3] matches {
                                events matches {
                                    POINT_EVENT[id4] occurrences matches {0..*} matches {
                                        data matches {
                                            ITEM_TREE[id5] matches {
                                                items matches {
                                                    ELEMENT[id6] matches { }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    SECTION[id8] occurrences matches {0..1} matches { }
                }
            }
        """,
        "id1", "id2", "id3", "id4", "id5", "id6", "id8");

    // Builds a valid OBSERVATION matching MinimalObsTemplate.
    private static Observation BuildValidMinimalVitalsObservation()
    {
        DvQuantity quantity = new() { Magnitude = 120.0, Units = "mm[Hg]" };
        Element el = NewElement("id5", quantity);
        ItemTree tree = NewItemTree("id4", [el]);
        PointEvent ev = NewPointEvent("id3", tree);
        History hist = NewHistory("id2", [ev]);
        return NewObservation("id1", hist);
    }

    // Builds a valid Composition matching ReportCompositionTemplate.
    private static Composition BuildValidReportComposition()
    {
        Element el = NewElement("id6");
        ItemTree tree = NewItemTree("id5", [el]);
        PointEvent ev = NewPointEvent("id4", tree);
        History hist = NewHistory("id3", [ev]);
        Observation obs = NewObservation("id2", hist);
        Section sec = NewSection("id8");
        return NewComposition("id1", [obs, sec]);
    }

    // ---- happy-path --------------------------------------------------

    [Fact]
    public void HappyPath_minimal_vitals_observation_produces_no_errors()
    {
        // Deviation: MinimalObsTemplate is OBSERVATION-rooted, so we drive
        // the Locatable overload instead of the Composition one.
        OperationalTemplate opt = ParseOpt2(MinimalObsTemplate);
        Observation obs = BuildValidMinimalVitalsObservation();

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(
            obs, opt, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void HappyPath_report_composition_produces_no_errors()
    {
        OperationalTemplate opt = ParseOpt2(ReportCompositionTemplate);
        Composition comp = BuildValidReportComposition();

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(
            comp, opt, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    // ---- STRUCT_001 --------------------------------------------------

    [Fact]
    public void Structural_unknown_node_id_emits_single_STRUCT_001()
    {
        OperationalTemplate opt = ParseOpt2(ReportCompositionTemplate);
        // Insert a section with an unknown node id at /content.
        Section bogus = NewSection("id99");
        Composition comp = NewComposition("id1", [bogus]);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        ValidationIssue only = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.NodeNotInTemplate);
        Assert.Equal(ValidationSeverity.Error, only.Severity);
        Assert.Equal("/content[id99]", only.Path);
    }

    // ---- CARD_001 ----------------------------------------------------

    [Fact]
    public void Cardinality_lower_bound_violation_emits_single_CARD_001()
    {
        // content cardinality {1..*}; supply zero items.
        string body = """
            COMPOSITION[id1] matches {
                content cardinality matches {1..*} matches {
                    SECTION[id2] matches { }
                }
            }
        """;
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-COMPOSITION.t.v1.0.0", body, "id1", "id2"));
        Composition comp = NewComposition("id1", []);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        ValidationIssue only = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.CardinalityViolation);
        Assert.Equal(ValidationSeverity.Error, only.Severity);
        Assert.Equal("/content", only.Path);
        Assert.Contains("1..*", only.Message);
        Assert.Contains("found 0", only.Message);
    }

    [Fact]
    public void Cardinality_upper_bound_violation_emits_single_CARD_001()
    {
        // content cardinality {0..1}; supply two items.
        string body = """
            COMPOSITION[id1] matches {
                content cardinality matches {0..1} matches {
                    SECTION[id2] occurrences matches {0..*} matches { }
                }
            }
        """;
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-COMPOSITION.t.v1.0.0", body, "id1", "id2"));
        Composition comp = NewComposition("id1", [
            NewSection("id2"),
            NewSection("id2"),
        ]);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        ValidationIssue only = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.CardinalityViolation);
        Assert.Equal("/content", only.Path);
        Assert.Contains("0..1", only.Message);
        Assert.Contains("found 2", only.Message);
    }

    // ---- OCC_001 -----------------------------------------------------

    [Fact]
    public void Occurrences_lower_bound_violation_emits_single_OCC_001()
    {
        // SECTION[id2] occurrences {1..1}; supply zero sections.
        string body = """
            COMPOSITION[id1] matches {
                content matches {
                    SECTION[id2] occurrences matches {1..1} matches { }
                }
            }
        """;
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-COMPOSITION.t.v1.0.0", body, "id1", "id2"));
        Composition comp = NewComposition("id1", []);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        ValidationIssue only = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.OccurrencesViolation);
        Assert.Equal(ValidationSeverity.Error, only.Severity);
        Assert.Equal("/content[id2]", only.Path);
        Assert.Contains("found 0", only.Message);
    }

    [Fact]
    public void Occurrences_upper_bound_violation_emits_single_OCC_001()
    {
        // SECTION[id2] occurrences {0..1}; supply two sections.
        string body = """
            COMPOSITION[id1] matches {
                content matches {
                    SECTION[id2] occurrences matches {0..1} matches { }
                }
            }
        """;
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-COMPOSITION.t.v1.0.0", body, "id1", "id2"));
        Composition comp = NewComposition("id1", [
            NewSection("id2"),
            NewSection("id2"),
        ]);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        ValidationIssue only = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.OccurrencesViolation);
        Assert.Equal("/content[id2]", only.Path);
        Assert.Contains("found 2", only.Message);
    }

    // ---- Multiple issues --------------------------------------------

    [Fact]
    public void Multiple_distinct_violations_all_surface_with_correct_paths()
    {
        // Template: content cardinality {1..*}; SECTION[id2] occurrences {1..1}.
        // Composition: zero content children → CARD_001 at /content
        //                                     + OCC_001 at /content[id2].
        string body = """
            COMPOSITION[id1] matches {
                content cardinality matches {1..*} matches {
                    SECTION[id2] occurrences matches {1..1} matches { }
                }
            }
        """;
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-COMPOSITION.t.v1.0.0", body, "id1", "id2"));
        Composition comp = NewComposition("id1", []);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        ValidationIssue card = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.CardinalityViolation);
        ValidationIssue occ = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.OccurrencesViolation);
        Assert.Equal("/content", card.Path);
        Assert.Equal("/content[id2]", occ.Path);
    }

    // ---- Empty composition ------------------------------------------

    [Fact]
    public void Empty_composition_against_required_content_emits_violations()
    {
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-COMPOSITION.t.v1.0.0",
            """
            COMPOSITION[id1] matches {
                content cardinality matches {1..*} matches {
                    SECTION[id2] occurrences matches {1..*} matches { }
                }
            }
            """,
            "id1", "id2"));
        Composition comp = NewComposition("id1");  // Content = null

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        Assert.Contains(issues, i => i.RuleId == ValidationRuleIds.CardinalityViolation);
        Assert.Contains(issues, i => i.RuleId == ValidationRuleIds.OccurrencesViolation);
    }

    // ---- Cancellation -----------------------------------------------

    [Fact]
    public void Cancellation_pre_cancelled_token_throws_immediately()
    {
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-COMPOSITION.t.v1.0.0",
            """
            COMPOSITION[id1] matches {
                content matches {
                    SECTION[id2] occurrences matches {0..*} matches { }
                }
            }
            """,
            "id1", "id2"));

        List<ContentItem> children = [];
        for (int i = 0; i < 200; i++)
        {
            children.Add(NewSection("id2"));
        }
        Composition comp = NewComposition("id1", children);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => s_validator.Validate(comp, opt, cts.Token));
    }

    [Fact]
    public void Cancellation_mid_walk_returns_within_budget()
    {
        // Build a single ItemTree containing ~200K Cluster children, all
        // matching the same template node, so validation is a long
        // bounded loop with per-node cancellation checks.
        OperationalTemplate opt = ParseOpt2(Opt2(
            "openEHR-EHR-OBSERVATION.bulk.v1.0.0",
            """
            OBSERVATION[id1] matches {
                data matches {
                    HISTORY[id2] matches {
                        events matches {
                            POINT_EVENT[id3] matches {
                                data matches {
                                    ITEM_TREE[id4] matches {
                                        items matches {
                                            CLUSTER[id5] occurrences matches {0..*} matches { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            """,
            "id1", "id2", "id3", "id4", "id5"));

        List<Item> bulk = [];
        for (int i = 0; i < 200_000; i++)
        {
            bulk.Add(NewCluster("id5"));
        }
        Observation obs = NewObservation(
            "id1",
            NewHistory("id2", [NewPointEvent("id3", NewItemTree("id4", bulk))]));

        using CancellationTokenSource cts = new();
        // Cancel after 10ms from a separate task.
        Task.Run(async () =>
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            cts.Cancel();
        }, TestContext.Current.CancellationToken);

        Stopwatch sw = Stopwatch.StartNew();
        OperationCanceledException? caught = null;
        try
        {
            s_validator.Validate(obs, opt, cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            caught = ex;
        }
        sw.Stop();

        Assert.NotNull(caught);
        // Generous budget: cancel after ~10ms, validator should observe
        // the token at the next per-node check and unwind well within
        // the next 100ms.
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"Validator took {sw.ElapsedMilliseconds}ms to unwind after cancellation.");
    }

    // ---- Path reporting ---------------------------------------------

    [Fact]
    public void Path_reporting_for_nested_STRUCT_001_matches_expected_AQL()
    {
        OperationalTemplate opt = ParseOpt2(ReportCompositionTemplate);

        // Build a valid scaffold but slip an unexpected element node id
        // (id77) under /content[id2]/data/events[id4]/data/items.
        Element bogus = NewElement("id77");
        ItemTree tree = NewItemTree("id5", [bogus]);
        PointEvent ev = NewPointEvent("id4", tree);
        History hist = NewHistory("id3", [ev]);
        Observation obs = NewObservation("id2", hist);
        Composition comp = NewComposition("id1", [obs]);

        IReadOnlyList<ValidationIssue> issues = s_validator.Validate(comp, opt, TestContext.Current.CancellationToken);

        ValidationIssue only = Assert.Single(
            issues, i => i.RuleId == ValidationRuleIds.NodeNotInTemplate);
        Assert.Equal(
            "/content[id2]/data[id3]/events[id4]/data[id5]/items[id77]",
            only.Path);
    }
}
